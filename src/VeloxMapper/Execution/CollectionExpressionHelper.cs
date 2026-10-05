using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace VeloxMapper.Execution;

/// <summary>
/// Koleksiyon türü algılama ve koleksiyon eşleştirme ifadeleri.
/// Diziler, <c>List&lt;T&gt;</c>, <c>HashSet&lt;T&gt;</c>, arayüzler (<c>IEnumerable&lt;T&gt;</c>, <c>ICollection&lt;T&gt;</c>,
/// <c>IList&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c>, <c>ISet&lt;T&gt;</c>...) ve parametresiz kurucusu olan özel
/// koleksiyonlar desteklenir.
/// </summary>
internal static class CollectionExpressionHelper
{
    private static readonly MethodInfo SelectMethod = typeof(Enumerable).GetMethods()
        .First(m => m.Name == nameof(Enumerable.Select) && m.GetParameters().Length == 2 &&
                    m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2);

    private static readonly MethodInfo ToListMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!;
    private static readonly MethodInfo ToArrayMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray))!;
    private static readonly MethodInfo FillMethod = typeof(CollectionExpressionHelper).GetMethod(nameof(Fill), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo FillDictionaryMethod = typeof(CollectionExpressionHelper).GetMethod(nameof(FillDictionary), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Türün bir koleksiyon olup olmadığını döndürür (<c>string</c> hariç).</summary>
    public static bool IsCollectionType(Type type) => type != typeof(string) && GetCollectionElementType(type) != null;

    /// <summary>Koleksiyonun eleman türünü döndürür; koleksiyon değilse <c>null</c>.</summary>
    public static Type? GetCollectionElementType(Type type)
    {
        if (type == typeof(string)) return null;
        if (type.IsArray) return type.GetElementType();

        if (type.IsGenericType && type.IsInterface && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];

        var enumerable = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }

    /// <summary>Tür bir sözlük ise anahtar ve değer türlerini döndürür.</summary>
    public static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
    {
        keyType = valueType = null!;
        var candidates = type.IsInterface ? new[] { type }.Concat(type.GetInterfaces()) : type.GetInterfaces();
        foreach (var candidate in candidates)
        {
            if (!candidate.IsGenericType) continue;
            var definition = candidate.GetGenericTypeDefinition();
            if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
            {
                var args = candidate.GetGenericArguments();
                keyType = args[0];
                valueType = args[1];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>source.Select(x =&gt; map(x))</c> ifadesini hedef koleksiyon türüne uygun şekilde (ToList/ToArray/HashSet...) üretir.
    /// </summary>
    public static Expression? BuildNewCollection(Expression source, Type sourceElement, Type destinationElement, Type destinationType,
        LambdaExpression elementMap, bool forProjection)
    {
        var select = Expression.Call(SelectMethod.MakeGenericMethod(sourceElement, destinationElement), source, elementMap);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(destinationElement);
        var listType = typeof(List<>).MakeGenericType(destinationElement);

        if (destinationType.IsArray)
            return Expression.Call(ToArrayMethod.MakeGenericMethod(destinationElement), select);

        if (destinationType == enumerableType && forProjection)
            return select;

        if (destinationType.IsAssignableFrom(listType))
            return Expression.Convert(Expression.Call(ToListMethod.MakeGenericMethod(destinationElement), select), destinationType);

        var hashSetType = typeof(HashSet<>).MakeGenericType(destinationElement);
        if (destinationType.IsAssignableFrom(hashSetType))
        {
            var ctor = hashSetType.GetConstructor(new[] { enumerableType })!;
            return Expression.Convert(Expression.New(ctor, select), destinationType);
        }

        if (!destinationType.IsAbstract && !destinationType.IsInterface)
        {
            // Kurucusu IEnumerable<T>, IList<T> veya List<T> alan koleksiyonlar (ReadOnlyCollection, ImmutableArray hariç)
            var enumerableCtor = destinationType.GetConstructor(new[] { enumerableType });
            if (enumerableCtor != null) return Expression.New(enumerableCtor, select);

            var listCtor = destinationType.GetConstructor(new[] { typeof(IList<>).MakeGenericType(destinationElement) })
                           ?? destinationType.GetConstructor(new[] { listType });
            if (listCtor != null) return Expression.New(listCtor, Expression.Call(ToListMethod.MakeGenericMethod(destinationElement), select));

            var collectionType = typeof(ICollection<>).MakeGenericType(destinationElement);
            var parameterless = destinationType.GetConstructor(Type.EmptyTypes);
            if (parameterless != null && collectionType.IsAssignableFrom(destinationType) && !forProjection)
            {
                return Expression.Convert(
                    Expression.Call(FillMethod.MakeGenericMethod(destinationElement), Expression.New(parameterless), select),
                    destinationType);
            }
        }

        return null;
    }

    /// <summary>
    /// Yeni bir hedef koleksiyonu, kapanış (closure) ve LINQ ara nesneleri üretmeden doğrudan döngüyle dolduran ifade.
    /// Diziler ve <c>List&lt;T&gt;</c> kaynakları indeksle, diğerleri numaralandırıcıyla gezilir. Desteklenmeyen hedef türlerde <c>null</c> döner.
    /// </summary>
    public static Expression? BuildLoopCollection(Expression source, Type sourceElement, Type destinationElement, Type destinationType,
        Func<Expression, Expression> mapElement)
    {
        var listType = typeof(List<>).MakeGenericType(destinationElement);
        var hashSetType = typeof(HashSet<>).MakeGenericType(destinationElement);
        var collectionType = typeof(ICollection<>).MakeGenericType(destinationElement);

        Type targetType;
        Expression create;
        Func<Expression, Expression> finish;
        var count = CountExpression(source, sourceElement);

        if (destinationType.IsArray || destinationType.IsAssignableFrom(listType))
        {
            targetType = listType;
            var capacityCtor = listType.GetConstructor(new[] { typeof(int) })!;
            create = count != null ? Expression.New(capacityCtor, count) : Expression.New(listType);
            finish = destinationType.IsArray
                ? target => Expression.Call(target, listType.GetMethod(nameof(List<int>.ToArray))!)
                : target => ExpressionUtil.Coerce(target, destinationType);
        }
        else if (destinationType.IsAssignableFrom(hashSetType))
        {
            targetType = hashSetType;
            create = Expression.New(hashSetType);
            finish = target => ExpressionUtil.Coerce(target, destinationType);
        }
        else if (!destinationType.IsAbstract && !destinationType.IsInterface && collectionType.IsAssignableFrom(destinationType) &&
                 destinationType.GetConstructor(Type.EmptyTypes) != null)
        {
            targetType = destinationType;
            create = Expression.New(destinationType);
            finish = target => target;
        }
        else
        {
            return null;
        }

        var target = Expression.Variable(targetType, "target");
        var item = Expression.Variable(sourceElement, "item");
        var addMethod = targetType.GetMethod("Add", new[] { destinationElement }) ?? collectionType.GetMethod("Add")!;
        var addTarget = addMethod.DeclaringType == collectionType && targetType != collectionType ? (Expression)Expression.Convert(target, collectionType) : target;
        var add = Expression.Call(addTarget, addMethod, ExpressionUtil.Coerce(mapElement(item), destinationElement));
        var loop = BuildForEach(source, sourceElement, item, add);

        return Expression.Block(destinationType, new[] { target, item },
            Expression.Assign(target, create),
            loop,
            ExpressionUtil.Coerce(finish(target), destinationType));
    }

    private static Expression? CountExpression(Expression source, Type sourceElement)
    {
        if (source.Type.IsArray) return Expression.ArrayLength(source);
        var collection = typeof(ICollection<>).MakeGenericType(sourceElement);
        if (collection.IsAssignableFrom(source.Type))
        {
            var countProperty = source.Type.GetProperty("Count", Type.EmptyTypes) ?? collection.GetProperty("Count")!;
            var instance = countProperty.DeclaringType == collection ? Expression.Convert(source, collection) : source;
            return Expression.Property(instance, countProperty);
        }

        return null;
    }

    private static Expression BuildForEach(Expression source, Type sourceElement, ParameterExpression item, Expression body)
    {
        var breakLabel = Expression.Label("loopEnd");
        var listType = typeof(List<>).MakeGenericType(sourceElement);

        if (source.Type.IsArray || source.Type == listType)
        {
            var index = Expression.Variable(typeof(int), "i");
            var length = source.Type.IsArray ? (Expression)Expression.ArrayLength(source) : Expression.Property(source, "Count");
            var element = source.Type.IsArray ? (Expression)Expression.ArrayIndex(source, index) : Expression.Property(source, "Item", index);
            return Expression.Block(new[] { index },
                Expression.Assign(index, Expression.Constant(0)),
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.LessThan(index, length),
                        Expression.Block(Expression.Assign(item, element), body, Expression.PostIncrementAssign(index)),
                        Expression.Break(breakLabel)),
                    breakLabel));
        }

        var enumerableType = typeof(IEnumerable<>).MakeGenericType(sourceElement);
        var enumeratorType = typeof(IEnumerator<>).MakeGenericType(sourceElement);
        var enumerator = Expression.Variable(enumeratorType, "enumerator");
        return Expression.Block(new[] { enumerator },
            Expression.Assign(enumerator, Expression.Call(Expression.Convert(source, enumerableType), enumerableType.GetMethod(nameof(IEnumerable<int>.GetEnumerator))!)),
            Expression.TryFinally(
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Call(enumerator, typeof(System.Collections.IEnumerator).GetMethod(nameof(System.Collections.IEnumerator.MoveNext))!),
                        Expression.Block(Expression.Assign(item, Expression.Property(enumerator, "Current")), body),
                        Expression.Break(breakLabel)),
                    breakLabel),
                Expression.Call(enumerator, typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))!)));
    }

    /// <summary>
    /// Eşlenmiş elemanları mevcut koleksiyon örneğine (Clear + Add) yazan ifade üretir. Koleksiyon salt-okunursa yeni koleksiyon döndürür.
    /// </summary>
    public static Expression BuildFillExisting(Expression existing, Expression source, Type sourceElement, Type destinationElement,
        LambdaExpression elementMap, Expression newCollection)
    {
        var collectionType = typeof(ICollection<>).MakeGenericType(destinationElement);
        var select = Expression.Call(SelectMethod.MakeGenericMethod(sourceElement, destinationElement), source, elementMap);
        var asCollection = Expression.TypeAs(existing, collectionType);
        var isWritable = Expression.AndAlso(
            Expression.NotEqual(asCollection, Expression.Constant(null, collectionType)),
            Expression.Not(Expression.Property(asCollection, collectionType.GetProperty(nameof(ICollection<int>.IsReadOnly))!)));

        return Expression.Condition(
            isWritable,
            ExpressionUtil.Coerce(Expression.Call(FillMethod.MakeGenericMethod(destinationElement), asCollection, select), existing.Type),
            newCollection);
    }

    /// <summary>
    /// Sözlükten sözlüğe eşleştirme ifadesi (anahtar ve değerler ayrı ayrı eşlenir). Mevcut yazılabilir sözlük varsa yerinde doldurulur.
    /// </summary>
    public static Expression? BuildDictionary(Expression source, Type sourceKey, Type sourceValue, Type destinationType,
        Type destinationKey, Type destinationValue, LambdaExpression keyMap, LambdaExpression valueMap, Expression? existing)
    {
        var destinationPair = typeof(KeyValuePair<,>).MakeGenericType(destinationKey, destinationValue);
        var sourcePair = typeof(KeyValuePair<,>).MakeGenericType(sourceKey, sourceValue);
        var idictionary = typeof(IDictionary<,>).MakeGenericType(destinationKey, destinationValue);
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(destinationKey, destinationValue);

        Expression create;
        if (destinationType.IsAssignableFrom(dictionaryType))
        {
            create = Expression.New(dictionaryType);
        }
        else
        {
            var ctor = destinationType.GetConstructor(Type.EmptyTypes);
            if (ctor == null || !idictionary.IsAssignableFrom(destinationType)) return null;
            create = Expression.New(ctor);
        }

        Expression target = Expression.Convert(create, idictionary);
        if (existing != null)
        {
            var asDictionary = Expression.TypeAs(existing, idictionary);
            var isWritable = Expression.AndAlso(
                Expression.NotEqual(asDictionary, Expression.Constant(null, idictionary)),
                Expression.Not(Expression.Property(Expression.Convert(asDictionary, typeof(ICollection<>).MakeGenericType(destinationPair)), "IsReadOnly")));
            target = Expression.Condition(isWritable, asDictionary, target);
        }

        var pair = Expression.Parameter(sourcePair, "kv");
        var mappedPair = Expression.New(
            destinationPair.GetConstructor(new[] { destinationKey, destinationValue })!,
            ExpressionUtil.ReplaceParameter(keyMap, Expression.Property(pair, "Key")),
            ExpressionUtil.ReplaceParameter(valueMap, Expression.Property(pair, "Value")));
        var pairs = Expression.Call(
            SelectMethod.MakeGenericMethod(sourcePair, destinationPair),
            Expression.Convert(source, typeof(IEnumerable<>).MakeGenericType(sourcePair)),
            Expression.Lambda(mappedPair, pair));

        var call = Expression.Call(FillDictionaryMethod.MakeGenericMethod(destinationKey, destinationValue), target, pairs);
        return ExpressionUtil.Coerce(call, destinationType);
    }

    /// <summary>Belirtilen koleksiyon türü için boş bir örnek oluşturur.</summary>
    public static object? CreateEmptyCollection(Type collectionType)
    {
        var elementType = GetCollectionElementType(collectionType) ?? typeof(object);
        if (collectionType.IsArray) return Array.CreateInstance(elementType, 0);

        if (TryGetDictionaryTypes(collectionType, out var key, out var value))
        {
            var dictionaryType = typeof(Dictionary<,>).MakeGenericType(key, value);
            if (collectionType.IsAssignableFrom(dictionaryType)) return Activator.CreateInstance(dictionaryType);
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        if (collectionType.IsAssignableFrom(listType)) return Activator.CreateInstance(listType);

        var hashSetType = typeof(HashSet<>).MakeGenericType(elementType);
        if (collectionType.IsAssignableFrom(hashSetType)) return Activator.CreateInstance(hashSetType);

        return collectionType.IsAbstract || collectionType.IsInterface || collectionType.GetConstructor(Type.EmptyTypes) == null
            ? null
            : Activator.CreateInstance(collectionType);
    }

    private static ICollection<T> Fill<T>(ICollection<T> destination, IEnumerable<T> items)
    {
        // Önce eşle, sonra temizle: kaynak hedefin kendisi olsa bile veri kaybolmaz
        var buffer = items.ToList();
        destination.Clear();
        foreach (var item in buffer) destination.Add(item);
        return destination;
    }

    private static IDictionary<TKey, TValue> FillDictionary<TKey, TValue>(IDictionary<TKey, TValue> destination, IEnumerable<KeyValuePair<TKey, TValue>> pairs)
    {
        var buffer = pairs.ToList();
        destination.Clear();
        foreach (var pair in buffer) destination[pair.Key] = pair.Value;
        return destination;
    }
}

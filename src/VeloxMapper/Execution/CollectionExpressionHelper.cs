using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using VeloxMapper.Caching;
using VeloxMapper.Abstractions;

namespace VeloxMapper.Execution;

/// <summary>
/// Koleksiyon tür algılama ve dönüşüm expression üretimi için yardımcı sınıf.
/// List&lt;T&gt;, T[], IEnumerable&lt;T&gt;, ICollection&lt;T&gt;, IList&lt;T&gt;, IReadOnlyList&lt;T&gt; destekler.
/// </summary>
internal static class CollectionExpressionHelper
{
    /// <summary>
    /// Verilen türün bir koleksiyon türü olup olmadığını kontrol eder.
    /// string hariç tutulur (IEnumerable&lt;char&gt; olarak algılanmamalı).
    /// </summary>
    public static bool IsCollectionType(Type type)
    {
        if (type == typeof(string)) return false;

        return type.IsArray || GetCollectionElementType(type) != null;
    }

    /// <summary>
    /// Koleksiyonun eleman türünü döndürür. Koleksiyon değilse null döner.
    /// </summary>
    public static Type? GetCollectionElementType(Type type)
    {
        if (type == typeof(string)) return null;

        // T[] durumu
        if (type.IsArray)
            return type.GetElementType();

        // IEnumerable<T> generic arayüzü
        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();
            if (genDef == typeof(IEnumerable<>) ||
                genDef == typeof(ICollection<>) ||
                genDef == typeof(IList<>) ||
                genDef == typeof(IReadOnlyList<>) ||
                genDef == typeof(IReadOnlyCollection<>) ||
                genDef == typeof(List<>) ||
                genDef == typeof(HashSet<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        // IEnumerable<T> implement eden tür
        var enumerableInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerableInterface?.GetGenericArguments()[0];
    }

    /// <summary>
    /// Koleksiyon eleman dönüşümü için Select + ToList/ToArray expression üretir.
    /// <c>source.Select(x => MapElement(x)).ToList()</c> veya <c>.ToArray()</c>
    /// </summary>
    /// <param name="sourceExpr">Kaynak koleksiyon expression</param>
    /// <param name="sourceElementType">Kaynak eleman türü</param>
    /// <param name="destElementType">Hedef eleman türü</param>
    /// <param name="destCollectionType">Hedef koleksiyon türü (List, Array, IEnumerable)</param>
    /// <param name="elementMapFunc">Her eleman için mapping body üreten fonksiyon</param>
    /// <param name="allowNullCollections">Null koleksiyonların boş koleksiyon yerine null olarak map edilmesini sağlar</param>
    /// <param name="mode">Eşleme modu (Map, Patch veya ProjectTo). ProjectTo modunda IQueryable uyumlu ifade üretilir.</param>
    public static Expression BuildCollectionMapping(
        Expression sourceExpr,
        Type sourceElementType,
        Type destElementType,
        Type destCollectionType,
        Func<Expression, Expression> elementMapFunc,
        bool allowNullCollections,
        MappingMode mode = MappingMode.Map)
    {
        // 1. Temel koleksiyon eşleme (Select çağrısı)
        var elementParam = Expression.Parameter(sourceElementType, "x");
        var elementBody = elementMapFunc(elementParam);

        // x => MapElement(x) lambda'sı
        var selectLambda = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(sourceElementType, destElementType),
            elementBody,
            elementParam);

        // Enumerable.Select(source, x => ...)
        var selectMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Select" &&
                        m.GetParameters().Length == 2 &&
                        m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2)
            .MakeGenericMethod(sourceElementType, destElementType);

        var selectCall = Expression.Call(null, selectMethod, sourceExpr, selectLambda);

        if (mode == MappingMode.ProjectTo)
        {
            if (destCollectionType.IsAssignableFrom(selectCall.Type))
            {
                return selectCall;
            }
            return Expression.Convert(selectCall, destCollectionType);
        }

        Expression mappedExpr;
        Expression fallbackExpr;

        var isHashSet = destCollectionType.IsGenericType && destCollectionType.GetGenericTypeDefinition() == typeof(HashSet<>);

        // Hedef türüne göre eşleme ve varsayılan (fallback) değer üretimi
        if (destCollectionType.IsArray)
        {
            var toArrayMethod = typeof(Enumerable)
                .GetMethod("ToArray")!
                .MakeGenericMethod(destElementType);
            mappedExpr = Expression.Call(null, toArrayMethod, selectCall);

            fallbackExpr = Expression.NewArrayBounds(destElementType, Expression.Constant(0));
        }
        else if (isHashSet)
        {
            var hashSetType = typeof(HashSet<>).MakeGenericType(destElementType);
            var hashSetCtorWithEnum = hashSetType.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(destElementType) })!;
            mappedExpr = Expression.New(hashSetCtorWithEnum, selectCall);

            var hashSetDefaultCtor = hashSetType.GetConstructor(Type.EmptyTypes)!;
            fallbackExpr = Expression.New(hashSetDefaultCtor);
        }
        else
        {
            var toListMethod = typeof(Enumerable)
                .GetMethod("ToList")!
                .MakeGenericMethod(destElementType);
            mappedExpr = Expression.Call(null, toListMethod, selectCall);

            var listCtor = typeof(List<>).MakeGenericType(destElementType).GetConstructor(Type.EmptyTypes)!;
            fallbackExpr = Expression.New(listCtor);
        }

        // 2. Null kontrolü sarmalaması
        var isNullCondition = Expression.Equal(sourceExpr, Expression.Constant(null, sourceExpr.Type));

        Expression finalFallback;
        if (allowNullCollections)
        {
            finalFallback = Expression.Constant(null, destCollectionType);
        }
        else
        {
            // Fallback değerini hedef tipe dönüştür
            finalFallback = destCollectionType.IsAssignableFrom(fallbackExpr.Type)
                ? fallbackExpr
                : Expression.Convert(fallbackExpr, destCollectionType);
        }

        // Eşlenmiş değeri de gerekirse hedef tipe dönüştür
        var finalMapped = destCollectionType.IsAssignableFrom(mappedExpr.Type)
            ? mappedExpr
            : Expression.Convert(mappedExpr, destCollectionType);

        return Expression.Condition(isNullCondition, finalFallback, finalMapped);
    }

    /// <summary>
    /// Eşleştirme sırasında hedef koleksiyon referansını koruyarak kaynak koleksiyondaki elemanları
    /// hedef koleksiyona entegre eder (Clear ve Add mantığı).
    /// </summary>
    public static void MergeCollections<TSourceElement, TDestElement>(
        IEnumerable<TSourceElement>? source,
        ICollection<TDestElement>? destination,
        VeloxResolutionContext context)
    {
        if (source == null || destination == null) return;
        destination.Clear();
        foreach (var item in source)
        {
            var mapped = context.Mapper.Map<TSourceElement, TDestElement>(item, context);
            destination.Add(mapped);
        }
    }

    /// <summary>
    /// Belirtilen koleksiyon türü için boş bir koleksiyon örneği oluşturur.
    /// </summary>
    /// <param name="collectionType">Oluşturulacak koleksiyonun türü</param>
    /// <returns>Boş koleksiyon nesnesi</returns>
    public static object CreateEmptyCollection(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            var elementType = collectionType.GetElementType() ?? typeof(object);
            return Array.CreateInstance(elementType, 0);
        }

        var elementTypeGeneric = GetCollectionElementType(collectionType);
        if (elementTypeGeneric != null)
        {
            var isHashSet = collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(HashSet<>);
            if (isHashSet)
            {
                var hashSetType = typeof(HashSet<>).MakeGenericType(elementTypeGeneric);
                return Activator.CreateInstance(hashSetType)!;
            }

            var listType = typeof(List<>).MakeGenericType(elementTypeGeneric);
            return Activator.CreateInstance(listType)!;
        }

        try
        {
            return Activator.CreateInstance(collectionType)!;
        }
        catch
        {
            return null!;
        }
    }
}

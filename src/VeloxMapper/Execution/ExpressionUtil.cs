using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace VeloxMapper.Execution;

/// <summary>
/// İfade ağacı yardımcıları.
/// </summary>
internal static class ExpressionUtil
{
    /// <summary>
    /// Lambda'nın ilk parametresini verilen ifadeyle değiştirerek gövdeyi döndürür. Tür uyuşmazsa dönüşüm eklenir.
    /// </summary>
    internal static Expression ReplaceParameter(LambdaExpression lambda, Expression replacement, int index = 0)
    {
        var parameter = lambda.Parameters[index];
        var value = parameter.Type.IsAssignableFrom(replacement.Type) ? replacement : Expression.Convert(replacement, parameter.Type);
        return new ParameterReplacer(parameter, value).Visit(lambda.Body)!;
    }

    /// <summary>Lambda'nın parametrelerini sırayla verilen ifadelerle değiştirir.</summary>
    internal static Expression ReplaceParameters(LambdaExpression lambda, params Expression[] replacements)
    {
        var body = lambda.Body;
        for (var i = 0; i < replacements.Length && i < lambda.Parameters.Count; i++)
        {
            var parameter = lambda.Parameters[i];
            var value = parameter.Type.IsAssignableFrom(replacements[i].Type) ? replacements[i] : Expression.Convert(replacements[i], parameter.Type);
            body = new ParameterReplacer(parameter, value).Visit(body)!;
        }

        return body;
    }

    /// <summary>Dıştaki <c>Convert</c> düğümlerini soyar.</summary>
    internal static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            expression = unary.Operand;
        return expression;
    }

    /// <summary>Tür farklıysa <c>Convert</c> ekler.</summary>
    internal static Expression Coerce(Expression expression, Type type)
        => expression.Type == type ? expression : Expression.Convert(expression, type);

    /// <summary>Türün <c>null</c> alabilip alamayacağını döndürür.</summary>
    internal static bool CanBeNull(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

    /// <summary><c>expression == null</c> (veya Nullable için <c>!HasValue</c>) ifadesi.</summary>
    internal static Expression IsNull(Expression expression)
        => Nullable.GetUnderlyingType(expression.Type) != null
            ? Expression.Not(Expression.Property(expression, "HasValue"))
            : Expression.ReferenceEqual(expression, Expression.Constant(null, expression.Type));

    /// <summary>Bir delegeyi parametre türlerine göre güvenli şekilde çağıran ifade üretir.</summary>
    internal static Expression InvokeDelegate(Delegate @delegate, params Expression[] arguments)
    {
        var parameters = @delegate.GetType().GetMethod("Invoke")!.GetParameters();
        var args = new Expression[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            args[i] = Coerce(arguments[i], parameters[i].ParameterType);
        }

        return Expression.Invoke(Expression.Constant(@delegate), args);
    }

    /// <summary>Delegenin parametre sayısı.</summary>
    internal static int Arity(Delegate @delegate) => @delegate.GetType().GetMethod("Invoke")!.GetParameters().Length;

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly Expression _replacement;

        public ParameterReplacer(ParameterExpression parameter, Expression replacement)
        {
            _parameter = parameter;
            _replacement = replacement;
        }

        protected override Expression VisitParameter(ParameterExpression node) => node == _parameter ? _replacement : node;
    }
}

/// <summary>
/// <c>MapFrom</c> ifadelerindeki üye zincirlerini null-güvenli hale getirir:
/// <c>s.Customer.Address.City</c> → <c>(s.Customer == null || s.Customer.Address == null) ? null : s.Customer.Address.City</c>.
/// Üretilen ifade EF Core tarafından SQL'e çevrilebilir.
/// </summary>
internal sealed class NullSafetyVisitor : ExpressionVisitor
{
    private readonly bool _guardExtensionMethods;

    private NullSafetyVisitor(bool guardExtensionMethods)
    {
        _guardExtensionMethods = guardExtensionMethods;
    }

    /// <summary>İfadeyi null-güvenli hale getirir. Projeksiyonlarda extension metot argümanları korunmaz (EF Core çevirisi için).</summary>
    internal static Expression Apply(Expression expression, bool forProjection = false)
        => new NullSafetyVisitor(!forProjection).Visit(expression)!;

    protected override Expression VisitMember(MemberExpression node) => node.Expression == null ? node : GuardChain(node);

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Object != null) return GuardChain(node);

        var arguments = node.Arguments.Select(a => Visit(a)!).ToList();
        var rebuilt = node.Update(null, arguments);

        // Extension metot: ilk argüman null olabilen bir üye zinciriyse (ör. s.Items.Count()) koru
        if (_guardExtensionMethods && node.Method.IsDefined(typeof(ExtensionAttribute), false) && arguments.Count > 0 &&
            IsGuardable(node.Arguments[0]) && !node.Arguments[0].Type.IsValueType)
        {
            return Expression.Condition(
                Expression.ReferenceEqual(arguments[0], Expression.Constant(null, arguments[0].Type)),
                Expression.Default(node.Type),
                rebuilt);
        }

        return rebuilt;
    }

    private Expression GuardChain(Expression node)
    {
        // Zinciri yapraktan köke topla
        var links = new List<Expression>();
        var current = node;
        while (true)
        {
            links.Add(current);
            if (current is MemberExpression member && member.Expression != null) current = member.Expression;
            else if (current is MethodCallExpression call && call.Object != null) current = call.Object;
            else break;
        }

        links.Reverse(); // kök → yaprak
        var root = links[0];
        Expression rebuilt = root is ParameterExpression || root is ConstantExpression ? root : Visit(root)!;

        var checks = new List<Expression>();
        if (!(root is ParameterExpression) && !(root is ConstantExpression) && links.Count > 1 && !rebuilt.Type.IsValueType && NeedsCheck(rebuilt.Type))
        {
            checks.Add(Expression.ReferenceEqual(rebuilt, Expression.Constant(null, rebuilt.Type)));
        }

        for (var i = 1; i < links.Count; i++)
        {
            rebuilt = links[i] switch
            {
                MemberExpression m => m.Update(rebuilt),
                MethodCallExpression c => c.Update(rebuilt, c.Arguments.Select(a => Visit(a)!)),
                _ => rebuilt
            };

            if (i < links.Count - 1 && !rebuilt.Type.IsValueType && NeedsCheck(rebuilt.Type))
            {
                checks.Add(Expression.ReferenceEqual(rebuilt, Expression.Constant(null, rebuilt.Type)));
            }
        }

        if (checks.Count == 0) return rebuilt;

        var condition = checks.Aggregate((a, b) => Expression.OrElse(a, b));
        return Expression.Condition(condition, Expression.Default(node.Type), rebuilt);
    }

    /// <summary>Projeksiyonlarda koleksiyon navigasyonları SQL tarafında asla null olmadığından kontrol edilmez (gereksiz JOIN üretmemek için).</summary>
    private bool NeedsCheck(Type type) => _guardExtensionMethods || !CollectionExpressionHelper.IsCollectionType(type);

    private static bool IsGuardable(Expression expression)
        => expression is MemberExpression { Expression: not null } || expression is MethodCallExpression { Object: not null };
}

/// <summary>
/// Enum dönüşümleri: AutoMapper ile aynı şekilde önce isme göre (büyük/küçük harf duyarsız), bulunamazsa sayısal değere göre eşler.
/// </summary>
internal static class EnumConverter<TSource, TDestination>
    where TSource : struct, Enum
    where TDestination : struct, Enum
{
    private static readonly Dictionary<TSource, TDestination> ByName = BuildByName();

    internal static TDestination Convert(TSource source)
    {
        if (ByName.TryGetValue(source, out var destination)) return destination;
        return (TDestination)Enum.ToObject(typeof(TDestination), System.Convert.ToInt64(source, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static Dictionary<TSource, TDestination> BuildByName()
    {
        var map = new Dictionary<TSource, TDestination>();
        var destinationNames = Enum.GetNames(typeof(TDestination));
        foreach (TSource value in Enum.GetValues(typeof(TSource)))
        {
            var name = Enum.GetName(typeof(TSource), value);
            var match = destinationNames.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (match != null && !map.ContainsKey(value)) map[value] = (TDestination)Enum.Parse(typeof(TDestination), match);
        }

        return map;
    }
}

/// <summary>
/// String → enum dönüşümü (null/boş değer → varsayılan; büyük/küçük harf duyarsız).
/// </summary>
internal static class EnumParser<TDestination> where TDestination : struct, Enum
{
    internal static TDestination Parse(string? value)
        => string.IsNullOrWhiteSpace(value) ? default : (TDestination)Enum.Parse(typeof(TDestination), value!, ignoreCase: true);
}

/// <summary>
/// String'den yaygın değer türlerine dönüşümler (null/boş değer → varsayılan).
/// </summary>
internal static class StringParsers
{
    internal static readonly MethodInfo ParseGuidMethod = typeof(StringParsers).GetMethod(nameof(ParseGuid), BindingFlags.NonPublic | BindingFlags.Static)!;
    internal static readonly MethodInfo ParseTimeSpanMethod = typeof(StringParsers).GetMethod(nameof(ParseTimeSpan), BindingFlags.NonPublic | BindingFlags.Static)!;
    internal static readonly MethodInfo ParseDateTimeOffsetMethod = typeof(StringParsers).GetMethod(nameof(ParseDateTimeOffset), BindingFlags.NonPublic | BindingFlags.Static)!;
    internal static readonly MethodInfo ChangeTypeMethod = typeof(StringParsers).GetMethod(nameof(ChangeType), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Guid ParseGuid(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.Empty : Guid.Parse(value!);

    private static TimeSpan ParseTimeSpan(string? value) => string.IsNullOrWhiteSpace(value) ? TimeSpan.Zero : TimeSpan.Parse(value!, System.Globalization.CultureInfo.CurrentCulture);

    private static DateTimeOffset ParseDateTimeOffset(string? value) => string.IsNullOrWhiteSpace(value) ? default : DateTimeOffset.Parse(value!, System.Globalization.CultureInfo.CurrentCulture);

    private static T ChangeType<T>(string? value)
        => string.IsNullOrWhiteSpace(value) ? default! : (T)System.Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.CurrentCulture);
}

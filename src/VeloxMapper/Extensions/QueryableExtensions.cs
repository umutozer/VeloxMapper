using System;
using System.Linq;
using System.Linq.Expressions;
using VeloxMapper.Exceptions;

namespace VeloxMapper.Extensions;

/// <summary>
/// Projeksiyon kurallarını denetleyen AST ziyaretçisi.
/// System.Linq.Queryable ve Enumerable metotlarına izin verir;
/// kullanıcı tanımlı MethodCallExpression'ları reddeder (EF Core uyumluluğu).
/// </summary>
internal sealed class ProjectionExpressionVisitor : ExpressionVisitor
{
    /// <summary>
    /// MethodCallExpression ziyaretinde LINQ system metotları dışındaki
    /// tüm metot çağrılarını VeloxProjectionException ile reddeder.
    /// </summary>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var declaringType = node.Method.DeclaringType;

        // System.Linq.Queryable ve Enumerable metotlarına izin ver (Select, Where, vb.)
        if (declaringType == typeof(Queryable) || declaringType == typeof(Enumerable))
        {
            return base.VisitMethodCall(node);
        }

        // Diğer tüm metot çağrıları reddedilir — EF Core IQueryable kısıtlaması
        throw new VeloxProjectionException(
            $"ProjectTo<T>() çağrısı içinde '{node.Method.DeclaringType?.Name}.{node.Method.Name}()' " +
            $"metot çağrısına izin verilmez. IQueryable kısıtlamasına uymalısınız. " +
            $"Alternatif: veriyi önce belleğe yükleyin (.ToList()) ve ardından Map() kullanın.");
    }
}

/// <summary>
/// IQueryable için ProjectTo genişletme metodu.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// IQueryable kaynağını belirtilen hedef türe projeksiyonla çevirir.
    /// Projeksiyon ifadesi <see cref="ProjectionExpressionVisitor"/> ile doğrulanır.
    /// </summary>
    /// <typeparam name="TDestination">Hedef projeksiyon türü</typeparam>
    /// <param name="source">IQueryable kaynak</param>
    /// <param name="mapper">VeloxMapper örneği</param>
    /// <returns>Projeksiyon uygulanmış IQueryable</returns>
    public static IQueryable<TDestination> ProjectTo<TDestination>(this IQueryable source, IVeloxMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);

        var projected = mapper.ProjectTo<TDestination>(source);

        // EF Core kural denetimi: MethodCallExpression kontrolü
        var visitor = new ProjectionExpressionVisitor();
        visitor.Visit(projected.Expression);

        return projected;
    }
}

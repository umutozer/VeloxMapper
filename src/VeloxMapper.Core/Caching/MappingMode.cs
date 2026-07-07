namespace VeloxMapper.Caching;

/// <summary>
/// Eşleştirme stratejisini belirtir. Map, Patch veya ProjectTo.
/// Çakışmaları önlemek için MapperCacheKey içinde kullanılır.
/// </summary>
public enum MappingMode
{
    /// <summary>
    /// Yeni bir nesne oluşturularak dönülür. (örn: mapper.Map&lt;T&gt;(source))
    /// </summary>
    Map,
    
    /// <summary>
    /// Var olan bir hedef nesne üzerine özellikleri yazar. (örn: mapper.Map(source, destination))
    /// </summary>
    Patch,
    
    /// <summary>
    /// IQueryable üzerinden System.Linq.Expressions (AST) tabanlı projeksiyon sağlar.
    /// Metot çağrısı (MethodCallExpression) barındırmaz.
    /// </summary>
    ProjectTo
}

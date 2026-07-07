using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using VeloxMapper.Configuration;

namespace VeloxMapper.Execution;

/// <summary>
/// Property isimlerinden nested path çıkararak flattening expression üretir.
/// Örneğin: hedef <c>AddressCity</c> → kaynak <c>source.Address.City</c>
/// AutoMapper'ın flattening convention'ının VeloxMapper karşılığıdır.
/// </summary>
internal static class FlatteningHelper
{
    /// <summary>
    /// Hedef property adını kaynak türdeki nested property zincirine çözümler.
    /// Başarılı ise property erişim expression'ı döndürür; bulunamazsa null döner.
    /// </summary>
    /// <param name="sourceExpr">Kaynak nesne expression</param>
    /// <param name="sourceType">Kaynak tür</param>
    /// <param name="destPropertyName">Hedef property adı (örn: AddressCity)</param>
    /// <param name="config">Mapper yapılandırması</param>
    /// <returns>Çözümlenmiş expression veya null</returns>
    public static Expression? TryResolveFlattening(
        Expression sourceExpr, Type sourceType, string destPropertyName, MapperConfiguration config)
    {
        // Hedef property adından prefix/postfix'leri arındırıp olası aday isimleri alalım.
        // Hedef adı çözümlediğimiz için HEDEF prefix/postfix listeleri kullanılmalıdır.
        var candidates = NameMatchingHelper.GetNameCandidates(destPropertyName, config.DestinationPrefixes, config.DestinationPostfixes, config.NamingConvention);

        foreach (var candidate in candidates)
        {
            var result = TryResolveFlatteningRecursive(sourceExpr, sourceType, candidate, 0, config);
            if (result != null) return result;
        }

        return null;
    }

    /// <summary>
    /// Rekürsif greedy left-to-right matching algoritması.
    /// "AddressStreetName" → Address → Street → Name şeklinde parçalar.
    /// </summary>
    private static Expression? TryResolveFlatteningRecursive(
        Expression currentExpr, Type currentType, string remaining, int depth, MapperConfiguration config)
    {
        // Sonsuz döngü koruması
        if (depth > 10 || string.IsNullOrEmpty(remaining))
            return null;

        var props = currentType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(config.ShouldMapProperty)
            .ToArray();

        // Greedy: en uzun eşleşen property adını bulmak için soldan sağa dene
        for (int i = 1; i <= remaining.Length; i++)
        {
            var candidate = remaining.Substring(0, i);
            var matchProp = props.FirstOrDefault(p =>
                NameMatchingHelper.IsMatch(p.Name, candidate, config));

            if (matchProp == null || !matchProp.CanRead) continue;

            var propAccess = Expression.Property(currentExpr, matchProp);
            var rest = remaining.Substring(i);

            // Kalan string boşsa — tam eşleşme bulundu
            if (rest.Length == 0)
                return propAccess;

            // Alt property'de devam et (rekürsif)
            var nested = TryResolveFlatteningRecursive(propAccess, matchProp.PropertyType, rest, depth + 1, config);
            if (nested != null)
                return nested;
        }

        return null;
    }
}

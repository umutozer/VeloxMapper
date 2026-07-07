using System;

namespace VeloxMapper.Abstractions;

/// <summary>
/// Hedef property değerini özel bir mantık kullanarak çözen, DI (Dependency Injection) destekli resolver arayüzü.
/// AutoMapper'ın IValueResolver arayüzünün VeloxMapper karşılığıdır.
/// </summary>
/// <typeparam name="TSource">Kaynak nesne türü</typeparam>
/// <typeparam name="TDestination">Hedef nesne türü</typeparam>
/// <typeparam name="TDestMember">Çözümlenecek hedef property türü</typeparam>
public interface IVeloxValueResolver<in TSource, in TDestination, TDestMember>
{
    /// <summary>
    /// Özel çözümleme mantığını yürütür ve hedef property için üretilen değeri döner.
    /// </summary>
    /// <param name="source">Kaynak nesne örneği</param>
    /// <param name="destination">Hedef nesne örneği</param>
    /// <param name="destMember">Hedef property'nin mevcut değeri</param>
    /// <param name="context">Çalışma zamanı mapping bağlamı</param>
    /// <returns>Hedef property'e atanacak değer</returns>
    TDestMember Resolve(TSource source, TDestination destination, TDestMember destMember, VeloxResolutionContext context);
}

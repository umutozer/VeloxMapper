using System;

namespace VeloxMapper.Abstractions;

/// <summary>
/// Kaynak nesnenin belirli bir property değerini parametre olarak alan, DI destekli resolver arayüzü.
/// AutoMapper'ın IMemberValueResolver arayüzünün VeloxMapper karşılığıdır.
/// </summary>
/// <typeparam name="TSource">Kaynak nesne türü</typeparam>
/// <typeparam name="TDestination">Hedef nesne türü</typeparam>
/// <typeparam name="TSourceMember">Kaynak property türü</typeparam>
/// <typeparam name="TDestMember">Çözümlenecek hedef property türü</typeparam>
public interface IVeloxMemberValueResolver<in TSource, in TDestination, in TSourceMember, TDestMember>
{
    /// <summary>
    /// Özel çözümleme mantığını yürütür ve hedef property için üretilen değeri döner.
    /// </summary>
    /// <param name="source">Kaynak nesne örneği</param>
    /// <param name="destination">Hedef nesne örneği</param>
    /// <param name="sourceMember">Kaynak seçilen property değeri</param>
    /// <param name="destMember">Hedef property'nin mevcut değeri</param>
    /// <param name="context">Çalışma zamanı mapping bağlamı</param>
    /// <returns>Hedef property'e atanacak değer</returns>
    TDestMember Resolve(TSource source, TDestination destination, TSourceMember sourceMember, TDestMember destMember, VeloxResolutionContext context);
}

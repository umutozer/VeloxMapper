using System;

namespace VeloxMapper.Abstractions;

/// <summary>
/// Mapping öncesi (BeforeMap) veya sonrası (AfterMap) çalıştırılacak DI-destekli hook.
/// AutoMapper'ın IMappingAction karşılığıdır.
/// </summary>
/// <typeparam name="TSource">Kaynak nesne türü</typeparam>
/// <typeparam name="TDestination">Hedef nesne türü</typeparam>
public interface IVeloxMappingAction<in TSource, in TDestination>
{
    /// <summary>
    /// Haritalama (mapping) öncesi veya sonrasında çalıştırılacak özel mantığı yürütür.
    /// </summary>
    /// <param name="source">Kaynak nesne</param>
    /// <param name="destination">Hedef nesne</param>
    /// <param name="context">Çalışma zamanı mapping bağlamı</param>
    void Process(TSource source, TDestination destination, VeloxResolutionContext context);
}

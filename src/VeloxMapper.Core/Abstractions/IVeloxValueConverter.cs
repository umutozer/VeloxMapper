using System;

namespace VeloxMapper.Abstractions;

/// <summary>
/// Kaynak ve hedef nesnelerden bağımsız, saf member-to-member tür dönüştürücü arayüzü.
/// DI gerektirmez, IValueResolver'dan daha hafiftir.
/// AutoMapper'ın IValueConverter arayüzünün VeloxMapper karşılığıdır.
/// </summary>
/// <typeparam name="TSourceMember">Kaynak property türü</typeparam>
/// <typeparam name="TDestMember">Hedef property türü</typeparam>
public interface IVeloxValueConverter<in TSourceMember, out TDestMember>
{
    /// <summary>
    /// Kaynak property değerini hedef property değerine dönüştürür.
    /// </summary>
    /// <param name="sourceMember">Kaynak property değeri</param>
    /// <param name="context">Çalışma zamanı mapping bağlamı</param>
    /// <returns>Dönüştürülmüş hedef property değeri</returns>
    TDestMember Convert(TSourceMember sourceMember, VeloxResolutionContext context);
}

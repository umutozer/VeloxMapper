namespace VeloxMapper.Abstractions;

/// <summary>
/// Sistemde bir türden diğerine özel dönüşüm mantığı gerektiğinde uygulanacak sınırlı genişletilebilirlik arayüzü.
/// Özel dönüştürücüler, otomatik haritalamanın (auto-mapping) yerine geçer.
/// </summary>
/// <typeparam name="TSource">Kaynak tür</typeparam>
/// <typeparam name="TDestination">Hedef tür</typeparam>
public interface IVeloxTypeConverter<in TSource, out TDestination>
{
    /// <summary>
    /// Özel eşleştirme mantığını yürütür.
    /// </summary>
    /// <param name="source">Kaynak nesne (null olabilir, kontrolleri implementasyon yapmalıdır)</param>
    /// <returns>Oluşturulan hedef nesne</returns>
    TDestination Convert(TSource? source);
}

/// <summary>
/// DI tarafından çözümlenmek yerine, doğrudan sabit bir referans (constant) olarak expression içerisine
/// gömülmesi gereken özel dönüştürücüler için kullanılan dahili işaretçi arayüzü.
/// </summary>
internal interface IConstantConverter
{
}

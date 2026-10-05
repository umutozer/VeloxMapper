namespace VeloxMapper;

/// <summary>
/// Hedef üyenin değerini özel bir mantıkla üreten resolver. AutoMapper'ın <c>IValueResolver</c> arayüzü ile aynı imzaya sahiptir.
/// <c>opt.MapFrom&lt;TResolver&gt;()</c> ile kullanılır; örnek DI konteynerinden (yoksa kurucu ile) çözülür.
/// </summary>
/// <typeparam name="TSource">Kaynak tür.</typeparam>
/// <typeparam name="TDestination">Hedef tür.</typeparam>
/// <typeparam name="TDestMember">Hedef üyenin türü.</typeparam>
public interface IValueResolver<in TSource, in TDestination, TDestMember>
{
    /// <summary>
    /// Hedef üye için değeri üretir.
    /// </summary>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="destination">Hedef nesne.</param>
    /// <param name="destMember">Hedef üyenin mevcut değeri.</param>
    /// <param name="context">Çalışma zamanı bağlamı.</param>
    /// <returns>Hedef üyeye atanacak değer.</returns>
    TDestMember Resolve(TSource source, TDestination destination, TDestMember destMember, ResolutionContext context);
}

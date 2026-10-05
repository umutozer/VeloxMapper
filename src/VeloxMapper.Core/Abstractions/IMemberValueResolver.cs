namespace VeloxMapper;

/// <summary>
/// Kaynak nesnenin belirli bir üyesini girdi olarak alan resolver. AutoMapper'ın <c>IMemberValueResolver</c> arayüzü ile aynı imzaya sahiptir.
/// <c>opt.MapFrom&lt;TResolver, TSourceMember&gt;(src =&gt; src.Member)</c> ile kullanılır.
/// </summary>
/// <typeparam name="TSource">Kaynak tür.</typeparam>
/// <typeparam name="TDestination">Hedef tür.</typeparam>
/// <typeparam name="TSourceMember">Girdi olarak kullanılan kaynak üyenin türü.</typeparam>
/// <typeparam name="TDestMember">Hedef üyenin türü.</typeparam>
public interface IMemberValueResolver<in TSource, in TDestination, in TSourceMember, TDestMember>
{
    /// <summary>
    /// Hedef üye için değeri üretir.
    /// </summary>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="destination">Hedef nesne.</param>
    /// <param name="sourceMember">Seçilen kaynak üyenin değeri.</param>
    /// <param name="destMember">Hedef üyenin mevcut değeri.</param>
    /// <param name="context">Çalışma zamanı bağlamı.</param>
    /// <returns>Hedef üyeye atanacak değer.</returns>
    TDestMember Resolve(TSource source, TDestination destination, TSourceMember sourceMember, TDestMember destMember, ResolutionContext context);
}

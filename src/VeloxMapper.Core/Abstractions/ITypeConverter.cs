namespace VeloxMapper;

/// <summary>
/// Bir tür çiftinin eşleştirmesini tamamen devralan dönüştürücü. AutoMapper'ın <c>ITypeConverter</c> arayüzü ile aynı imzaya sahiptir.
/// <c>CreateMap&lt;TSource, TDestination&gt;().ConvertUsing&lt;MyConverter&gt;()</c> veya <c>.ConvertUsing(new MyConverter())</c> ile kullanılır.
/// </summary>
/// <typeparam name="TSource">Kaynak tür.</typeparam>
/// <typeparam name="TDestination">Hedef tür.</typeparam>
public interface ITypeConverter<in TSource, TDestination>
{
    /// <summary>
    /// Kaynak nesneyi hedef türe dönüştürür.
    /// </summary>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="destination">Mevcut hedef nesne (<c>Map(source, destination)</c> çağrısında doludur; aksi halde varsayılan değerdir).</param>
    /// <param name="context">Çalışma zamanı bağlamı.</param>
    /// <returns>Dönüştürülmüş hedef nesne.</returns>
    TDestination Convert(TSource source, TDestination destination, ResolutionContext context);
}

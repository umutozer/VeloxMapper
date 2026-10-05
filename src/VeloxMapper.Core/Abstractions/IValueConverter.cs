namespace VeloxMapper;

/// <summary>
/// Tek bir üye değerini başka bir türe dönüştüren, yeniden kullanılabilir dönüştürücü.
/// AutoMapper'ın <c>IValueConverter</c> arayüzü ile aynı imzaya sahiptir.
/// <c>opt.ConvertUsing(new MyConverter(), src =&gt; src.Member)</c> veya <c>opt.ConvertUsing&lt;MyConverter, TSourceMember&gt;()</c> ile kullanılır.
/// </summary>
/// <typeparam name="TSourceMember">Kaynak üyenin türü.</typeparam>
/// <typeparam name="TDestinationMember">Hedef üyenin türü.</typeparam>
public interface IValueConverter<in TSourceMember, out TDestinationMember>
{
    /// <summary>
    /// Kaynak üye değerini hedef üye türüne dönüştürür.
    /// </summary>
    /// <param name="sourceMember">Kaynak üye değeri.</param>
    /// <param name="context">Çalışma zamanı bağlamı.</param>
    /// <returns>Dönüştürülmüş değer.</returns>
    TDestinationMember Convert(TSourceMember sourceMember, ResolutionContext context);
}

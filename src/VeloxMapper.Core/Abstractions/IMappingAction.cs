namespace VeloxMapper;

/// <summary>
/// Eşleştirme öncesinde (<c>BeforeMap</c>) veya sonrasında (<c>AfterMap</c>) çalışan, DI destekli eylem.
/// AutoMapper'ın <c>IMappingAction</c> arayüzü ile aynı imzaya sahiptir.
/// </summary>
/// <typeparam name="TSource">Kaynak tür.</typeparam>
/// <typeparam name="TDestination">Hedef tür.</typeparam>
public interface IMappingAction<in TSource, in TDestination>
{
    /// <summary>
    /// Eylemi çalıştırır.
    /// </summary>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="destination">Hedef nesne.</param>
    /// <param name="context">Çalışma zamanı bağlamı.</param>
    void Process(TSource source, TDestination destination, ResolutionContext context);
}

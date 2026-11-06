using System;

namespace VeloxMapper;

/// <summary>
/// <see cref="IMapper"/> arayüzünün yalnızca <c>Map</c> overload'larını içeren taban arayüzü. AutoMapper'ın <c>IMapperBase</c>
/// arayüzü ile aynı addadır; <c>IMapperBase</c> parametresi alan mevcut kod değiştirilmeden derlenir.
/// </summary>
public interface IMapperBase
{
    /// <summary>
    /// Kaynak nesneyi yeni bir <typeparamref name="TDestination"/> örneğine eşler. Kaynak türü çalışma zamanında belirlenir.
    /// </summary>
    /// <typeparam name="TDestination">Hedef tür.</typeparam>
    /// <param name="source">Kaynak nesne (<c>null</c> olabilir).</param>
    /// <returns>Eşleştirilmiş hedef nesne; kaynak <c>null</c> ise <c>null</c> (koleksiyonlar için boş koleksiyon).</returns>
    TDestination Map<TDestination>(object? source);

    /// <summary>
    /// Kaynak nesneyi, çağrıya özel seçeneklerle yeni bir <typeparamref name="TDestination"/> örneğine eşler.
    /// </summary>
    /// <typeparam name="TDestination">Hedef tür.</typeparam>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="opts">Çağrı seçenekleri (Items, BeforeMap, AfterMap...).</param>
    /// <returns>Eşleştirilmiş hedef nesne.</returns>
    TDestination Map<TDestination>(object? source, Action<IMappingOperationOptions<object, TDestination>> opts);

    /// <summary>
    /// Kaynak nesneyi yeni bir <typeparamref name="TDestination"/> örneğine eşler.
    /// </summary>
    /// <typeparam name="TSource">Kaynak tür.</typeparam>
    /// <typeparam name="TDestination">Hedef tür.</typeparam>
    /// <param name="source">Kaynak nesne.</param>
    /// <returns>Eşleştirilmiş hedef nesne.</returns>
    TDestination Map<TSource, TDestination>(TSource source);

    /// <summary>
    /// Kaynak nesneyi, çağrıya özel seçeneklerle yeni bir <typeparamref name="TDestination"/> örneğine eşler.
    /// </summary>
    /// <typeparam name="TSource">Kaynak tür.</typeparam>
    /// <typeparam name="TDestination">Hedef tür.</typeparam>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="opts">Çağrı seçenekleri.</param>
    /// <returns>Eşleştirilmiş hedef nesne.</returns>
    TDestination Map<TSource, TDestination>(TSource source, Action<IMappingOperationOptions<TSource, TDestination>> opts);

    /// <summary>
    /// Kaynak nesneyi var olan hedef nesneye eşler ve hedefi döndürür. İç içe nesneler mevcut örneklere,
    /// koleksiyonlar mevcut koleksiyon örneklerine eşlenir (EF Core tarafından takip edilen varlıklar için güvenlidir).
    /// </summary>
    /// <typeparam name="TSource">Kaynak tür.</typeparam>
    /// <typeparam name="TDestination">Hedef tür.</typeparam>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="destination">Var olan hedef nesne; <c>null</c> ise yeni örnek oluşturulur.</param>
    /// <returns>Güncellenmiş hedef nesne.</returns>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);

    /// <summary>
    /// Kaynak nesneyi, çağrıya özel seçeneklerle var olan hedef nesneye eşler ve hedefi döndürür.
    /// </summary>
    /// <typeparam name="TSource">Kaynak tür.</typeparam>
    /// <typeparam name="TDestination">Hedef tür.</typeparam>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="destination">Var olan hedef nesne.</param>
    /// <param name="opts">Çağrı seçenekleri.</param>
    /// <returns>Güncellenmiş hedef nesne.</returns>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination, Action<IMappingOperationOptions<TSource, TDestination>> opts);

    /// <summary>
    /// Türleri çalışma zamanında verilen bir eşleştirme yapar (generic repository / altyapı kodu için).
    /// </summary>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="sourceType">Kaynak tür.</param>
    /// <param name="destinationType">Hedef tür.</param>
    /// <returns>Eşleştirilmiş hedef nesne.</returns>
    object Map(object? source, Type sourceType, Type destinationType);

    /// <summary>
    /// Türleri çalışma zamanında verilen, çağrıya özel seçenekli bir eşleştirme yapar.
    /// </summary>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="sourceType">Kaynak tür.</param>
    /// <param name="destinationType">Hedef tür.</param>
    /// <param name="opts">Çağrı seçenekleri.</param>
    /// <returns>Eşleştirilmiş hedef nesne.</returns>
    object Map(object? source, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts);

    /// <summary>
    /// Türleri çalışma zamanında verilen, var olan hedefe eşleştirme yapar.
    /// </summary>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="destination">Var olan hedef nesne.</param>
    /// <param name="sourceType">Kaynak tür.</param>
    /// <param name="destinationType">Hedef tür.</param>
    /// <returns>Güncellenmiş hedef nesne.</returns>
    object Map(object? source, object? destination, Type sourceType, Type destinationType);

    /// <summary>
    /// Türleri çalışma zamanında verilen, var olan hedefe çağrıya özel seçeneklerle eşleştirme yapar.
    /// </summary>
    /// <param name="source">Kaynak nesne.</param>
    /// <param name="destination">Var olan hedef nesne.</param>
    /// <param name="sourceType">Kaynak tür.</param>
    /// <param name="destinationType">Hedef tür.</param>
    /// <param name="opts">Çağrı seçenekleri.</param>
    /// <returns>Güncellenmiş hedef nesne.</returns>
    object Map(object? source, object? destination, Type sourceType, Type destinationType, Action<IMappingOperationOptions<object, object>> opts);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace VeloxMapper;

/// <summary>
/// Nesne eşleştirme işlemlerini yürüten ana arayüz. AutoMapper'ın <c>IMapper</c> arayüzü ile aynı isim, overload ve
/// davranışlara sahiptir; mevcut AutoMapper kodu yalnızca <c>using</c> satırı değiştirilerek derlenir.
/// </summary>
/// <remarks>
/// Thread-safe'dir. DI ile kullanıldığında <c>AddVeloxMapper</c> bu arayüzü <b>transient</b> olarak kaydeder
/// (derlenmiş eşleştirmeler <see cref="IConfigurationProvider"/> üzerinde paylaşılır, mapper örneği hafiftir).
/// </remarks>
public interface IMapper
{
    /// <summary>
    /// Bu mapper'ın dayandığı yapılandırma. <c>ProjectTo</c>, doğrulama ve ön derleme için kullanılır.
    /// </summary>
    IConfigurationProvider ConfigurationProvider { get; }

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

    /// <summary>
    /// <see cref="IQueryable"/> kaynağını hedef türe projekte eder (EF Core tarafından SQL'e çevrilebilir ifade üretir).
    /// </summary>
    /// <typeparam name="TDestination">Hedef tür.</typeparam>
    /// <param name="source">Sorgu kaynağı.</param>
    /// <param name="parameters">Parametreli <c>MapFrom</c> ifadeleri için değerler (anonim nesne).</param>
    /// <param name="membersToExpand"><c>ExplicitExpansion</c> ile işaretlenmiş, genişletilecek üyeler.</param>
    /// <returns>Projeksiyon uygulanmış sorgu.</returns>
    IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, object? parameters = null, params Expression<Func<TDestination, object?>>[] membersToExpand);

    /// <summary>
    /// <see cref="IQueryable"/> kaynağını hedef türe projekte eder.
    /// </summary>
    /// <typeparam name="TDestination">Hedef tür.</typeparam>
    /// <param name="source">Sorgu kaynağı.</param>
    /// <param name="parameters">Parametreli <c>MapFrom</c> ifadeleri için değerler.</param>
    /// <param name="membersToExpand">Genişletilecek üye yolları (ör. <c>"Customer.Address"</c>).</param>
    /// <returns>Projeksiyon uygulanmış sorgu.</returns>
    IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source, IDictionary<string, object> parameters, params string[] membersToExpand);

    /// <summary>
    /// <see cref="IQueryable"/> kaynağını çalışma zamanında verilen hedef türe projekte eder.
    /// </summary>
    /// <param name="source">Sorgu kaynağı.</param>
    /// <param name="destinationType">Hedef tür.</param>
    /// <param name="parameters">Parametreli <c>MapFrom</c> ifadeleri için değerler.</param>
    /// <param name="membersToExpand">Genişletilecek üye yolları.</param>
    /// <returns>Projeksiyon uygulanmış sorgu.</returns>
    IQueryable ProjectTo(IQueryable source, Type destinationType, IDictionary<string, object>? parameters = null, params string[] membersToExpand);
}

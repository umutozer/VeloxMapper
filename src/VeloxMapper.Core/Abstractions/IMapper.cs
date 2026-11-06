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
public interface IMapper : IMapperBase
{
    /// <summary>
    /// Bu mapper'ın dayandığı yapılandırma. <c>ProjectTo</c>, doğrulama ve ön derleme için kullanılır.
    /// </summary>
    IConfigurationProvider ConfigurationProvider { get; }

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

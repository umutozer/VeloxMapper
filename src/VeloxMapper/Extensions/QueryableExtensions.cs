using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;

namespace VeloxMapper.QueryableExtensions
{
    /// <summary>
    /// <see cref="IQueryable"/> projeksiyon genişletmeleri. AutoMapper'ın <c>AutoMapper.QueryableExtensions.Extensions</c>
    /// sınıfı ile aynı imzalara sahiptir: <c>dbContext.Orders.ProjectTo&lt;OrderDto&gt;(mapper.ConfigurationProvider)</c>.
    /// </summary>
    /// <remarks>
    /// Üretilen ifade EF Core tarafından SQL'e çevrilir; yalnızca hedefte gereken kolonlar sorgulanır.
    /// Resolver, value converter ve fonksiyon tabanlı <c>MapFrom</c> kuralları sorguya çevrilemeyeceği için projeksiyonda atlanır.
    /// </remarks>
    public static class Extensions
    {
        /// <summary>Sorguyu hedef türe projekte eder.</summary>
        /// <param name="source">Sorgu.</param>
        /// <param name="configuration">Yapılandırma (<c>mapper.ConfigurationProvider</c>).</param>
        /// <param name="membersToExpand"><c>ExplicitExpansion</c> ile işaretli, sorguya dahil edilecek üyeler.</param>
        public static IQueryable<TDestination> ProjectTo<TDestination>(this IQueryable source, IConfigurationProvider configuration,
            params Expression<Func<TDestination, object?>>[] membersToExpand)
            => source.ProjectTo<TDestination>(configuration, (object?)null, membersToExpand);

        /// <summary>Sorguyu, parametreli <c>MapFrom</c> değerleriyle hedef türe projekte eder.</summary>
        /// <param name="source">Sorgu.</param>
        /// <param name="configuration">Yapılandırma.</param>
        /// <param name="parameters">Parametre değerleri (anonim nesne: <c>new { userId }</c>).</param>
        /// <param name="membersToExpand">Genişletilecek üyeler.</param>
        public static IQueryable<TDestination> ProjectTo<TDestination>(this IQueryable source, IConfigurationProvider configuration, object? parameters,
            params Expression<Func<TDestination, object?>>[] membersToExpand)
            => (IQueryable<TDestination>)Config(configuration).Project(source, typeof(TDestination), ProjectionParameters.FromObject(parameters), ProjectionParameters.Paths(membersToExpand));

        /// <summary>Sorguyu, sözlükle verilen parametre değerleriyle hedef türe projekte eder.</summary>
        /// <param name="source">Sorgu.</param>
        /// <param name="configuration">Yapılandırma.</param>
        /// <param name="parameters">Parametre değerleri.</param>
        /// <param name="membersToExpand">Genişletilecek üye yolları.</param>
        public static IQueryable<TDestination> ProjectTo<TDestination>(this IQueryable source, IConfigurationProvider configuration,
            IDictionary<string, object> parameters, params string[] membersToExpand)
            => (IQueryable<TDestination>)Config(configuration).Project(source, typeof(TDestination), parameters, membersToExpand);

        /// <summary>Sorguyu çalışma zamanında verilen hedef türe projekte eder.</summary>
        /// <param name="source">Sorgu.</param>
        /// <param name="destinationType">Hedef tür.</param>
        /// <param name="configuration">Yapılandırma.</param>
        public static IQueryable ProjectTo(this IQueryable source, Type destinationType, IConfigurationProvider configuration)
            => Config(configuration).Project(source, destinationType, null, null);

        /// <summary>Sorguyu çalışma zamanında verilen hedef türe, parametre ve genişletmelerle projekte eder.</summary>
        /// <param name="source">Sorgu.</param>
        /// <param name="destinationType">Hedef tür.</param>
        /// <param name="configuration">Yapılandırma.</param>
        /// <param name="parameters">Parametre değerleri.</param>
        /// <param name="membersToExpand">Genişletilecek üye yolları.</param>
        public static IQueryable ProjectTo(this IQueryable source, Type destinationType, IConfigurationProvider configuration,
            IDictionary<string, object> parameters, params string[] membersToExpand)
            => Config(configuration).Project(source, destinationType, parameters, membersToExpand);

        /// <summary>Sorguyu mapper'ın yapılandırmasıyla hedef türe projekte eder (<c>query.ProjectTo&lt;Dto&gt;(mapper)</c>).</summary>
        /// <param name="source">Sorgu.</param>
        /// <param name="mapper">Mapper.</param>
        /// <param name="membersToExpand">Genişletilecek üyeler.</param>
        public static IQueryable<TDestination> ProjectTo<TDestination>(this IQueryable source, IMapper mapper,
            params Expression<Func<TDestination, object?>>[] membersToExpand)
        {
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));
            return source.ProjectTo<TDestination>(mapper.ConfigurationProvider, (object?)null, membersToExpand);
        }

        private static MapperConfiguration Config(IConfigurationProvider configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            return configuration as MapperConfiguration
                   ?? throw new ArgumentException($"Yapılandırma bir {nameof(MapperConfiguration)} olmalıdır.", nameof(configuration));
        }
    }
}

namespace VeloxMapper.Extensions
{
    /// <summary>
    /// VeloxMapper 5.x ile geriye dönük uyumluluk için korunur. Yeni kodda <c>using VeloxMapper.QueryableExtensions;</c> kullanın.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class QueryableExtensions
    {
        /// <summary>Sorguyu mapper'ın yapılandırmasıyla hedef türe projekte eder.</summary>
        /// <param name="source">Sorgu.</param>
        /// <param name="mapper">Mapper.</param>
        public static IQueryable<TDestination> ProjectTo<TDestination>(this IQueryable source, IVeloxMapper mapper)
        {
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));
            return mapper.ProjectTo<TDestination>(source);
        }
    }
}

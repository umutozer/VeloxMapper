using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VeloxMapper;
using VeloxMapper.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// VeloxMapper'ı <see cref="IServiceCollection"/>'a kaydeden genişletme metotları.
/// AutoMapper'ın <c>AddAutoMapper</c> overload'larının tamamı <c>AddVeloxMapper</c> adıyla ve geçiş kolaylığı için
/// <c>AddAutoMapper</c> takma adıyla sunulur.
/// </summary>
/// <remarks>
/// Kayıtlar:
/// <list type="bullet">
/// <item><see cref="MapperConfiguration"/> ve <see cref="IConfigurationProvider"/>: singleton (derlenmiş eşleştirmeler burada önbelleklenir).</item>
/// <item><see cref="IMapper"/> ve <see cref="IVeloxMapper"/>: transient (AutoMapper ile aynı); scoped servisler (ör. DbContext) resolver'lara doğru kapsamdan enjekte edilir.</item>
/// <item>Taranan assembly'lerdeki <c>IValueResolver</c>, <c>IMemberValueResolver</c>, <c>ITypeConverter</c>, <c>IValueConverter</c>,
/// <c>IMappingAction</c> uygulamaları: transient.</item>
/// </list>
/// Metot birden fazla kez çağrılabilir (ör. modüler uygulamalarda); tüm yapılandırmalar ve assembly'ler tek bir yapılandırmada birleşir.
/// </remarks>
public static class VeloxMapperServiceCollectionExtensions
{
    /// <summary>VeloxMapper'ı yapılandırma delegesiyle kaydeder.</summary>
    public static IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction)
        => AddCore(services, Wrap(configAction), Enumerable.Empty<Assembly>());

    /// <summary>VeloxMapper'ı yapılandırma delegesiyle kaydeder ve verilen assembly'leri tarar.</summary>
    public static IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, params Assembly[] assemblies)
        => AddCore(services, Wrap(configAction), assemblies);

    /// <summary>VeloxMapper'ı yapılandırma delegesiyle kaydeder ve verilen assembly'leri tarar.</summary>
    public static IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, IEnumerable<Assembly> assemblies)
        => AddCore(services, Wrap(configAction), assemblies);

    /// <summary>VeloxMapper'ı yapılandırma delegesiyle kaydeder ve verilen türlerin assembly'lerini tarar.</summary>
    public static IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, params Type[] profileAssemblyMarkerTypes)
        => AddCore(services, Wrap(configAction), Assemblies(profileAssemblyMarkerTypes));

    /// <summary>VeloxMapper'ı yapılandırma delegesiyle kaydeder ve verilen türlerin assembly'lerini tarar.</summary>
    public static IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, IEnumerable<Type> profileAssemblyMarkerTypes)
        => AddCore(services, Wrap(configAction), Assemblies(profileAssemblyMarkerTypes));

    /// <summary>VeloxMapper'ı servis sağlayıcıya erişen bir yapılandırma delegesiyle kaydeder ve verilen assembly'leri tarar.</summary>
    public static IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<IServiceProvider, VeloxMapperOptions> configAction, params Assembly[] assemblies)
        => AddCore(services, configAction ?? throw new ArgumentNullException(nameof(configAction)), assemblies);

    /// <summary>VeloxMapper'ı servis sağlayıcıya erişen bir yapılandırma delegesiyle kaydeder ve verilen türlerin assembly'lerini tarar.</summary>
    public static IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<IServiceProvider, VeloxMapperOptions> configAction, params Type[] profileAssemblyMarkerTypes)
        => AddCore(services, configAction ?? throw new ArgumentNullException(nameof(configAction)), Assemblies(profileAssemblyMarkerTypes));

    /// <summary>Verilen assembly'lerdeki profilleri, <c>[AutoMap]</c> türlerini ve genişletme türlerini kaydeder.</summary>
    public static IServiceCollection AddVeloxMapper(this IServiceCollection services, params Assembly[] assemblies)
        => AddCore(services, null, assemblies);

    /// <summary>Verilen assembly'lerdeki profilleri, <c>[AutoMap]</c> türlerini ve genişletme türlerini kaydeder.</summary>
    public static IServiceCollection AddVeloxMapper(this IServiceCollection services, IEnumerable<Assembly> assemblies)
        => AddCore(services, null, assemblies);

    /// <summary>Verilen türlerin bulunduğu assembly'leri tarar: <c>services.AddVeloxMapper(typeof(Program))</c>.</summary>
    public static IServiceCollection AddVeloxMapper(this IServiceCollection services, params Type[] profileAssemblyMarkerTypes)
        => AddCore(services, null, Assemblies(profileAssemblyMarkerTypes));

    /// <summary>Verilen türlerin bulunduğu assembly'leri tarar.</summary>
    public static IServiceCollection AddVeloxMapper(this IServiceCollection services, IEnumerable<Type> profileAssemblyMarkerTypes)
        => AddCore(services, null, Assemblies(profileAssemblyMarkerTypes));

    // ─── AutoMapper geçiş takma adları ──────────────────────────────────────

    /// <summary>AutoMapper'dan geçiş için <see cref="AddVeloxMapper(IServiceCollection, Action{VeloxMapperOptions})"/> takma adı.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction)
        => services.AddVeloxMapper(configAction);

    /// <summary>AutoMapper'dan geçiş için takma ad.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, params Assembly[] assemblies)
        => services.AddVeloxMapper(configAction, assemblies);

    /// <summary>AutoMapper'dan geçiş için takma ad.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, IEnumerable<Assembly> assemblies)
        => services.AddVeloxMapper(configAction, assemblies);

    /// <summary>AutoMapper'dan geçiş için takma ad.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, params Type[] profileAssemblyMarkerTypes)
        => services.AddVeloxMapper(configAction, profileAssemblyMarkerTypes);

    /// <summary>AutoMapper'dan geçiş için takma ad.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, IEnumerable<Type> profileAssemblyMarkerTypes)
        => services.AddVeloxMapper(configAction, profileAssemblyMarkerTypes);

    /// <summary>AutoMapper'dan geçiş için takma ad.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<IServiceProvider, VeloxMapperOptions> configAction, params Assembly[] assemblies)
        => services.AddVeloxMapper(configAction, assemblies);

    /// <summary>AutoMapper'dan geçiş için takma ad.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddAutoMapper(this IServiceCollection services, Action<IServiceProvider, VeloxMapperOptions> configAction, params Type[] profileAssemblyMarkerTypes)
        => services.AddVeloxMapper(configAction, profileAssemblyMarkerTypes);

    /// <summary>AutoMapper'dan geçiş için takma ad.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddAutoMapper(this IServiceCollection services, params Assembly[] assemblies)
        => services.AddVeloxMapper(assemblies);

    /// <summary>AutoMapper'dan geçiş için takma ad.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddAutoMapper(this IServiceCollection services, IEnumerable<Assembly> assemblies)
        => services.AddVeloxMapper(assemblies);

    /// <summary>AutoMapper'dan geçiş için takma ad: <c>services.AddAutoMapper(typeof(Program))</c>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddAutoMapper(this IServiceCollection services, params Type[] profileAssemblyMarkerTypes)
        => services.AddVeloxMapper(profileAssemblyMarkerTypes);

    /// <summary>AutoMapper'dan geçiş için takma ad.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddAutoMapper(this IServiceCollection services, IEnumerable<Type> profileAssemblyMarkerTypes)
        => services.AddVeloxMapper(profileAssemblyMarkerTypes);

    // ─── Çekirdek ───────────────────────────────────────────────────────────

    private static Action<IServiceProvider, VeloxMapperOptions> Wrap(Action<VeloxMapperOptions> configAction)
    {
        if (configAction == null) throw new ArgumentNullException(nameof(configAction));
        return (_, cfg) => configAction(cfg);
    }

    private static IEnumerable<Assembly> Assemblies(IEnumerable<Type>? markerTypes)
    {
        if (markerTypes == null) throw new ArgumentNullException(nameof(markerTypes));
        return markerTypes.Where(t => t != null).Select(t => t.Assembly).Distinct();
    }

    private static IServiceCollection AddCore(IServiceCollection services, Action<IServiceProvider, VeloxMapperOptions>? configAction, IEnumerable<Assembly> assemblies)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));

        var registry = services.FirstOrDefault(d => d.ServiceType == typeof(VeloxMapperServiceRegistry))?.ImplementationInstance as VeloxMapperServiceRegistry;
        if (registry == null)
        {
            registry = new VeloxMapperServiceRegistry();
            services.AddSingleton(registry);
            services.TryAddSingleton(sp =>
            {
                var current = sp.GetRequiredService<VeloxMapperServiceRegistry>();
                return new MapperConfiguration(cfg =>
                {
                    cfg.AddMaps(current.Assemblies);
                    foreach (var action in current.ConfigActions) action(sp, cfg);
                }, sp.GetService<ILoggerFactory>());
            });
            services.TryAddSingleton<IConfigurationProvider>(sp => sp.GetRequiredService<MapperConfiguration>());
            services.TryAddTransient<IMapper>(sp => new Mapper(sp.GetRequiredService<MapperConfiguration>(), sp));
            services.TryAddTransient<IVeloxMapper>(sp => new Mapper(sp.GetRequiredService<MapperConfiguration>(), sp));
        }

        if (configAction != null) registry.ConfigActions.Add(configAction);

        foreach (var assembly in assemblies)
        {
            if (assembly == null || !registry.Assemblies.Add(assembly)) continue;
            RegisterExtensibilityTypes(services, assembly);
        }

        return services;
    }

    private static void RegisterExtensibilityTypes(IServiceCollection services, Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface || !type.IsClass) continue;
            if (ExtensibilityTypes.Implements(type, ExtensibilityTypes.All))
            {
                services.TryAddTransient(type);
            }
        }
    }

    /// <summary>Birden fazla <c>AddVeloxMapper</c> çağrısının yapılandırmalarını biriktiren iç kayıt.</summary>
    private sealed class VeloxMapperServiceRegistry
    {
        public List<Action<IServiceProvider, VeloxMapperOptions>> ConfigActions { get; } = new();
        public HashSet<Assembly> Assemblies { get; } = new();
    }
}

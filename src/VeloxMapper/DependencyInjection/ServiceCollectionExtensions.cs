using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using VeloxMapper.Configuration;

namespace VeloxMapper.DependencyInjection;

/// <summary>
/// VeloxMapper için IServiceCollection genişletme metotları.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// VeloxMapper'ı yapılandırma delegesi ile IServiceCollection'a Singleton olarak kaydeder.
    /// <para>
    /// Kullanım:
    /// <code>
    /// builder.Services.AddVeloxMapper(cfg =&gt;
    /// {
    ///     cfg.AddProfilesFromAssembly(typeof(Program).Assembly);
    ///     cfg.PatchMapping.IgnoreNullValues = true;
    /// });
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="services">Servis koleksiyonu</param>
    /// <param name="configure">Yapılandırma delegesi</param>
    /// <returns>Zincirleme kullanım için IServiceCollection</returns>
    public static IServiceCollection AddVeloxMapper(
        this IServiceCollection services,
        Action<VeloxMapperOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var config = new MapperConfiguration(configure);

        services.AddSingleton(config);
        services.AddSingleton<IVeloxMapper>(sp => new Mapper(config, sp));

        // Resolver ve MappingAction'ları otomatik olarak DI konteynerine Transient olarak kaydet
        RegisterResolversAndActions(services, config.GetAllRegistrations());

        return services;
    }

    private static void RegisterResolversAndActions(IServiceCollection services, System.Collections.Generic.IEnumerable<MappingRegistration> registrations)
    {
        var typesToRegister = new System.Collections.Generic.HashSet<Type>();

        foreach (var reg in registrations)
        {
            // BeforeMap ve AfterMap eylemleri
            foreach (var action in reg.BeforeMapActions)
            {
                if (action is Type actionType)
                {
                    typesToRegister.Add(actionType);
                }
            }
            foreach (var action in reg.AfterMapActions)
            {
                if (action is Type actionType)
                {
                    typesToRegister.Add(actionType);
                }
            }

            // Member kuralları içindeki resolver ve converter'lar
            foreach (var rule in reg.MemberRules.Values)
            {
                if (rule.ResolverType != null)
                {
                    typesToRegister.Add(rule.ResolverType);
                }
                if (rule.MemberValueResolverType != null)
                {
                    typesToRegister.Add(rule.MemberValueResolverType);
                }
                if (rule.ValueConverterType != null)
                {
                    typesToRegister.Add(rule.ValueConverterType);
                }
            }

            // ForPath kuralları içindeki resolver ve converter'lar (Reviewer bulgusu)
            foreach (var rule in reg.ForPathRules)
            {
                var memberRule = rule.MemberRule;
                if (memberRule.ResolverType != null)
                {
                    typesToRegister.Add(memberRule.ResolverType);
                }
                if (memberRule.MemberValueResolverType != null)
                {
                    typesToRegister.Add(memberRule.MemberValueResolverType);
                }
                if (memberRule.ValueConverterType != null)
                {
                    typesToRegister.Add(memberRule.ValueConverterType);
                }
            }
        }

        // Bulunan tüm tipleri Transient olarak kaydet (eğer interface değil ve somut sınıf ise)
        foreach (var type in typesToRegister)
        {
            if (!type.IsAbstract && !type.IsInterface)
            {
                services.AddTransient(type);
            }
        }
    }

    /// <summary>
    /// VeloxMapper'ı assembly scanning ile IServiceCollection'a Singleton olarak kaydeder.
    /// Verilen assembly'lerdeki tüm <see cref="VeloxProfile"/> alt sınıflarını otomatik bulur.
    /// <para>
    /// Kullanım:
    /// <code>
    /// // Tek assembly
    /// builder.Services.AddVeloxMapper(typeof(Program).Assembly);
    ///
    /// // Birden fazla assembly
    /// builder.Services.AddVeloxMapper(
    ///     typeof(Program).Assembly,
    ///     typeof(SomeProfile).Assembly);
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="services">Servis koleksiyonu</param>
    /// <param name="assemblies">Taranacak assembly'ler</param>
    /// <returns>Zincirleme kullanım için IServiceCollection</returns>
    public static IServiceCollection AddVeloxMapper(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        return services.AddVeloxMapper(cfg =>
        {
            cfg.AddProfilesFromAssemblies(assemblies);
        });
    }
}

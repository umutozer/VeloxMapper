// AutoMapper genişletme noktalarının (resolver, converter, action, DI) AutoMapper imzalarıyla çalıştığını doğrular.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VeloxMapper;

namespace VeloxMapper.Tests.Migration.Extensibility;

public class Source
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public string? CreatedOn { get; set; }
    public int TenantId { get; set; }
}

public class Destination
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Amount { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public string Tenant { get; set; } = "";
    public string Audit { get; set; } = "";
}

public interface IClock
{
    DateTime Now { get; }
}

public sealed class FixedClock : IClock
{
    public DateTime Now => new(2026, 1, 1);
}

public sealed class RequestScope
{
    public string Tenant { get; } = "tenant-" + Guid.NewGuid().ToString("N").Substring(0, 4);
}

// IValueResolver — AutoMapper imzası (ResolutionContext ile), scoped bağımlılık enjekte edilir
public sealed class TenantResolver : IValueResolver<Source, Destination, string>
{
    private readonly RequestScope _scope;
    public TenantResolver(RequestScope scope) => _scope = scope;
    public string Resolve(Source source, Destination destination, string destMember, ResolutionContext context) => _scope.Tenant + "/" + source.TenantId;
}

// IMemberValueResolver
public sealed class UpperResolver : IMemberValueResolver<Source, Destination, string, string>
{
    public string Resolve(Source source, Destination destination, string sourceMember, string destMember, ResolutionContext context) => sourceMember.ToUpperInvariant();
}

// IValueConverter
public sealed class CurrencyConverter : IValueConverter<decimal, string>
{
    public string Convert(decimal sourceMember, ResolutionContext context) => sourceMember.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " TL";
}

// ITypeConverter (global tür dönüştürücü: string → DateTime)
public sealed class DateTimeTypeConverter : ITypeConverter<string?, DateTime>
{
    public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
        => string.IsNullOrEmpty(source) ? DateTime.MinValue : DateTime.Parse(source, System.Globalization.CultureInfo.InvariantCulture);
}

// IMappingAction (DI ile bağımlılık alır)
public sealed class AuditAction : IMappingAction<Source, Destination>
{
    private readonly IClock _clock;
    public AuditAction(IClock clock) => _clock = clock;
    public void Process(Source source, Destination destination, ResolutionContext context) => destination.Audit = "mapped@" + _clock.Now.Year;
}

public sealed class ExtensibilityProfile : Profile
{
    public ExtensibilityProfile()
    {
        CreateMap<string?, DateTime>().ConvertUsing<DateTimeTypeConverter>();

        CreateMap<Source, Destination>()
            .ForMember(d => d.Name, o => o.MapFrom<UpperResolver, string>(s => s.Name))
            .ForMember(d => d.Amount, o => o.ConvertUsing(new CurrencyConverter(), s => s.Amount))
            .ForMember(d => d.Tenant, o => o.MapFrom<TenantResolver>())
            .AfterMap<AuditAction>();
    }
}

public class AutoMapperExtensibilityTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<RequestScope>();
        services.AddSingleton<IClock, FixedClock>();
        services.AddVeloxMapper(cfg => cfg.AddProfile<ExtensibilityProfile>(), typeof(ExtensibilityProfile));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void Resolver_converter_action_DI_ile_scoped_bagimliliklari_alir()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
        var requestScope = scope.ServiceProvider.GetRequiredService<RequestScope>();

        var result = mapper.Map<Destination>(new Source { Id = 1, Name = "kalem", Amount = 12.5m, CreatedOn = "2025-03-04", TenantId = 9 });

        Assert.Equal("KALEM", result.Name);
        Assert.Equal("12.50 TL", result.Amount);
        Assert.Equal(new DateTime(2025, 3, 4), result.CreatedOn);         // global ITypeConverter iç üyelerde de kullanılır
        Assert.Equal(requestScope.Tenant + "/9", result.Tenant);           // aynı scope'tan çözüldü
        Assert.Equal("mapped@2026", result.Audit);
    }

    [Fact]
    public void IMapper_transient_IConfigurationProvider_singleton_kaydedilir()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var m1 = scope.ServiceProvider.GetRequiredService<IMapper>();
        var m2 = scope.ServiceProvider.GetRequiredService<IMapper>();

        Assert.NotSame(m1, m2);
        Assert.Same(m1.ConfigurationProvider, m2.ConfigurationProvider);
        Assert.Same(provider.GetRequiredService<IConfigurationProvider>(), provider.GetRequiredService<MapperConfiguration>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IVeloxMapper>());
        Assert.NotNull(scope.ServiceProvider.GetService<TenantResolver>()); // taranan assembly'deki genişletme türleri kayıtlı
    }

    [Fact]
    public void AddVeloxMapper_birden_fazla_cagri_tek_yapilandirmada_birlesir()
    {
        var services = new ServiceCollection();
        services.AddVeloxMapper(cfg => cfg.CreateMap<Source, Destination>().ForMember(d => d.Amount, o => o.Ignore()).ForMember(d => d.CreatedOn, o => o.Ignore())
            .ForMember(d => d.Tenant, o => o.Ignore()).ForMember(d => d.Audit, o => o.Ignore()));
        services.AddVeloxMapper(cfg => cfg.CreateMap<Destination, Source>(MemberList.None));
        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMapper>();

        Assert.Equal("x", mapper.Map<Destination>(new Source { Name = "x" }).Name);
        Assert.Equal("y", mapper.Map<Source>(new Destination { Name = "y" }).Name);
    }

    [Fact]
    public void AddVeloxMapper_servis_saglayicili_yapilandirma_delegesi()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, FixedClock>();
        services.AddVeloxMapper((sp, cfg) =>
        {
            var year = sp.GetRequiredService<IClock>().Now.Year;
            cfg.CreateMap<Source, Destination>(MemberList.None).ForMember(d => d.Audit, o => o.MapFrom(_ => "y" + year));
        }, Array.Empty<System.Reflection.Assembly>());
        using var provider = services.BuildServiceProvider();

        Assert.Equal("y2026", provider.GetRequiredService<IMapper>().Map<Destination>(new Source()).Audit);
    }

    [Fact]
    public void ConstructServicesUsing_ve_CreateMapper_serviceCtor()
    {
        var created = new List<Type>();
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Source, Destination>(MemberList.None).ForMember(d => d.Name, o => o.MapFrom<UpperResolver, string>(s => s.Name));
        });

        var mapper = config.CreateMapper(type =>
        {
            created.Add(type);
            return Activator.CreateInstance(type)!;
        });

        Assert.Equal("ABC", mapper.Map<Destination>(new Source { Name = "abc" }).Name);
        Assert.Contains(typeof(UpperResolver), created);
    }

    [Fact]
    public void Resolver_ornegi_ve_inline_ConvertUsing_fonksiyonlari()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<int, string>().ConvertUsing((src, dest) => "#" + src);
            cfg.CreateMap<Source, Destination>(MemberList.None)
                .ForMember(d => d.Name, o => o.MapFrom(new ConstantResolver("sabit")));
        });
        var mapper = config.CreateMapper();

        var result = mapper.Map<Destination>(new Source { Id = 5 });
        Assert.Equal("sabit", result.Name);
        Assert.Equal("#5", mapper.Map<string>(5));
    }

    [Fact]
    public void MapperConfiguration_ILoggerFactory_imzasi_ve_LicenseKey_derlenir()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.LicenseKey = "gerekli-degil";
            cfg.CreateMap<Source, Destination>(MemberList.None);
        }, NullLoggerFactory.Instance);

        Assert.Equal(5, config.CreateMapper().Map<Destination>(new Source { Id = 5 }).Id);
    }

    [Fact]
    public void BeforeMap_AfterMap_inline_baglamli_eylemler()
    {
        var config = new MapperConfiguration(cfg =>
            cfg.CreateMap<Source, Destination>(MemberList.None)
                .BeforeMap((src, dest) => src.Name = src.Name.Trim())
                .AfterMap((src, dest, ctx) => dest.Audit = (string)ctx.Items["user"]));
        var mapper = config.CreateMapper();

        var result = mapper.Map<Destination>(new Source { Name = "  x  " }, opt => opt.Items["user"] = "admin");
        Assert.Equal("x", result.Name);
        Assert.Equal("admin", result.Audit);
    }

    private sealed class ConstantResolver : IValueResolver<Source, Destination, string>
    {
        private readonly string _value;
        public ConstantResolver(string value) => _value = value;
        public string Resolve(Source source, Destination destination, string destMember, ResolutionContext context) => _value;
    }
}

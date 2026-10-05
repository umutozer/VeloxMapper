using System;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using VeloxMapper.Abstractions;
using VeloxMapper.Configuration;

namespace VeloxMapper.Tests;

#region Test Modelleri ve Servisleri

/// <summary>
/// Test için sahte bir servis arayüzü.
/// </summary>
public interface IDateTimeProvider
{
    DateTime GetCurrentDateTime();
}

/// <summary>
/// Test için sahte bir servis implementasyonu.
/// </summary>
public class FakeDateTimeProvider : IDateTimeProvider
{
    public DateTime GetCurrentDateTime() => new DateTime(2026, 5, 22, 12, 0, 0);
}

public class ResolverSrc
{
    public string FirstName { get; set; } = "Umut";
    public string LastName { get; set; } = "Özel";
    public int? Age { get; set; }
    public string? Status { get; set; }
}

public class ResolverDst
{
    public string FullName { get; set; } = "";
    public string CurrentTimeStr { get; set; } = "";
    public int CalculatedAge { get; set; }
    public string StatusWithDefault { get; set; } = "";
}

#endregion

#region Value Resolver ve Converter Tanımları

/// <summary>
/// DI servis bağımlılığı olan bir ValueResolver.
/// Kaynak nesne ve servisi kullanarak hedef nesne için değer üretir.
/// </summary>
public class CurrentTimeResolver : IVeloxValueResolver<ResolverSrc, ResolverDst, string>
{
    private readonly IDateTimeProvider _dateTimeProvider;

    // DI ile bağımlılık enjekte edilir
    public CurrentTimeResolver(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    public string Resolve(ResolverSrc source, ResolverDst destination, string destMember, VeloxResolutionContext context)
    {
        // Servisten gelen zamanı formatlayarak döner
        return _dateTimeProvider.GetCurrentDateTime().ToString("yyyy-MM-dd HH:mm:ss");
    }
}

/// <summary>
/// Kaynaktaki belirli bir üyenin değerini ve diğer parametreleri kullanarak çözümleme yapan MemberValueResolver.
/// </summary>
public class AgeResolver : IVeloxMemberValueResolver<ResolverSrc, ResolverDst, int?, int>
{
    public int Resolve(ResolverSrc source, ResolverDst destination, int? sourceMember, int destMember, VeloxResolutionContext context)
    {
        // Eğer yaş değeri gelmişse 10 ekler, yoksa varsayılan 18 döner
        return sourceMember.HasValue ? sourceMember.Value + 10 : 18;
    }
}

/// <summary>
/// Kaynak ve hedef nesnelerden bağımsız olarak üye bazında dönüşüm yapan basit bir Converter.
/// </summary>
public class UpperCaseConverter : IVeloxValueConverter<string, string>
{
    public string Convert(string sourceMember, VeloxResolutionContext context)
    {
        return sourceMember?.ToUpperInvariant() ?? string.Empty;
    }
}

#endregion

#region Mapping Profili

public class ResolverTestProfile : VeloxProfile
{
    public ResolverTestProfile()
    {
        CreateMap<ResolverSrc, ResolverDst>()
            // IVeloxValueResolver kullanarak DI-destekli çözümleme
            .ForMember(d => d.CurrentTimeStr, opt => opt.MapFrom<CurrentTimeResolver>())
            // IVeloxMemberValueResolver kullanarak üye bazlı çözümleme
            .ForMember(d => d.CalculatedAge, opt => opt.MapFrom<AgeResolver, int?>(src => src.Age))
            // IVeloxValueConverter kullanarak dönüşüm
            .ForMember(d => d.FullName, opt => opt.ConvertUsing(new UpperCaseConverter(), src => $"{src.FirstName} {src.LastName}"))
            // NullSubstitute kuralı: Null ise varsayılan "ACTIVE" atanır
            .ForMember(d => d.StatusWithDefault, opt => {
                opt.NullSubstitute("ACTIVE");
                opt.MapFrom(s => s.Status!);
            });
    }
}

#endregion

public class VeloxMapperV5ResolverTests
{
    [Fact]
    public void DI_ValueResolver_Ve_MemberResolver_Calismali()
    {
        // 1. Servisleri ve DI konteynerini yapılandır
        var services = new ServiceCollection();
        
        // DateTimeProvider servisini kaydet
        services.AddSingleton<IDateTimeProvider, FakeDateTimeProvider>();
        
        // VeloxMapper'ı ekle ve profili kaydet
        services.AddVeloxMapper(cfg =>
        {
            cfg.AddProfile<ResolverTestProfile>();
        });

        var serviceProvider = services.BuildServiceProvider();

        // 2. Mapper örneğini al
        var mapper = serviceProvider.GetRequiredService<IVeloxMapper>();

        // 3. Mapping işlemini gerçekleştir
        var source = new ResolverSrc
        {
            FirstName = "ahmet",
            LastName = "yilmaz",
            Age = 25,
            Status = null // NullSubstitute testi için null veriyoruz
        };

        var dest = mapper.Map<ResolverSrc, ResolverDst>(source);

        // 4. Doğrulamalar
        Assert.NotNull(dest);
        
        // UpperCaseConverter testi: "ahmet yılmaz" -> "AHMET YILMAZ"
        Assert.Equal("AHMET YILMAZ", dest.FullName);
        
        // CurrentTimeResolver (DI bağımlı) testi: "2026-05-22 12:00:00"
        Assert.Equal("2026-05-22 12:00:00", dest.CurrentTimeStr);
        
        // AgeResolver (MemberValueResolver) testi: 25 + 10 = 35
        Assert.Equal(35, dest.CalculatedAge);
        
        // NullSubstitute testi: Null girince "ACTIVE" olmalı
        Assert.Equal("ACTIVE", dest.StatusWithDefault);
    }

    [Fact]
    public void Condition_Ve_PreCondition_Filtrelemesi_Calismali()
    {
        // 1. Yapılandırma ve Mapper
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ResolverSrc, ResolverDst>()
                // PreCondition: Kaynak adresi "Umut" ise haritala, değilse atamayı komple pas geç
                .ForMember(d => d.FullName, opt => {
                    opt.PreCondition(src => src.FirstName == "Umut");
                    opt.MapFrom(src => $"{src.FirstName} {src.LastName}");
                })
                // Condition: Yaş 18'den büyükse atamayı yap
                .ForMember(d => d.CalculatedAge, opt => {
                    opt.Condition((src, dest, val) => val >= 18);
                    opt.MapFrom(src => src.Age ?? 0);
                });
        });

        var mapper = new Mapper(config);

        // Durum A: PreCondition ve Condition geçerli
        var sourceA = new ResolverSrc { FirstName = "Umut", LastName = "Özel", Age = 20 };
        var destA = mapper.Map<ResolverDst>(sourceA);

        Assert.Equal("Umut Özel", destA.FullName);
        Assert.Equal(20, destA.CalculatedAge);

        // Durum B: PreCondition başarısız (FirstName != Umut), Condition başarısız (Age < 18)
        var sourceB = new ResolverSrc { FirstName = "Can", LastName = "Demir", Age = 15 };
        var destB = mapper.Map<ResolverDst>(sourceB);

        Assert.Equal("", destB.FullName); // Atanmadı, varsayılan boş kaldı
        Assert.Equal(0, destB.CalculatedAge); // Atanmadı, varsayılan 0 kaldı
    }

    [Fact]
    public void AssertConfigurationIsValid_CustomConverterIleBasariliOlmalidir()
    {
        var config = new MapperConfiguration(cfg =>
        {
            // Özel dönüştürücüyü kaydet (Reviewer bulgusu doğrulama)
            cfg.AddCustomConverter(new CustomValueConverter());
            
            // Üst modeli map et
            cfg.CreateMap<CustomModelSrc, CustomModelDst>();
        });

        // Bu doğrulamanın hata fırlatmadan geçmesi gerekir
        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void AddVeloxMapper_ForPathResolverlarininDIKaydiniYapmalidir()
    {
        var services = new ServiceCollection();
        services.AddVeloxMapper(cfg =>
        {
            cfg.AddProfile<ForPathResolverProfile>();
        });

        var sp = services.BuildServiceProvider();
        

        var mapper = sp.GetRequiredService<IVeloxMapper>();
        var source = new ForPathRootSrc();
        var dest = mapper.Map<ForPathRootDst>(source);

        Assert.Equal("NESTED", dest.Sub.UpperName);
    }
}

#region Custom Converter Test Modelleri

public class CustomModelSrc
{
    public CustomValueSrc Value { get; set; } = new();
}

public class CustomModelDst
{
    public CustomValueDst Value { get; set; } = new();
}

public class CustomValueSrc
{
    public int Number { get; set; }
}

public class CustomValueDst
{
    public string Text { get; set; } = "";
}

public class CustomValueConverter : IVeloxTypeConverter<CustomValueSrc, CustomValueDst>
{
    public CustomValueDst Convert(CustomValueSrc? source)
    {
        return new CustomValueDst { Text = source?.Number.ToString() ?? "" };
    }
}

#endregion

#region ForPath Resolver Test Modelleri

public class ForPathNestedSrc
{
    public string Name { get; set; } = "Nested";
}

public class ForPathRootSrc
{
    public ForPathNestedSrc Nested { get; set; } = new();
}

public class ForPathRootDst
{
    public ForPathNestedDst Sub { get; set; } = new();
}

public class ForPathNestedDst
{
    public string UpperName { get; set; } = "";
}

public class ForPathNameResolver : IVeloxValueResolver<ForPathRootSrc, ForPathRootDst, string>
{
    public string Resolve(ForPathRootSrc source, ForPathRootDst destination, string destMember, VeloxResolutionContext context)
    {
        return source.Nested?.Name.ToUpperInvariant() ?? "";
    }
}

public class ForPathResolverProfile : VeloxProfile
{
    public ForPathResolverProfile()
    {
        CreateMap<ForPathRootSrc, ForPathRootDst>()
            .ForPath(d => d.Sub.UpperName, opt => opt.MapFrom<ForPathNameResolver>());
    }
}

#endregion


using System;
using Xunit;
using VeloxMapper.Configuration;

namespace VeloxMapper.Tests;

#region Test Modelleri

public class BaseUser
{
    public int Id { get; set; }
    public string Name { get; set; } = "Base";
    public AddressInfo? Address { get; set; }
}

// Castle/EF Core Lazy Loading Proxy simülasyonu
public class BaseUserProxy : BaseUser
{
    // Proxy sınıfları genellikle dynamic/lazy loading için özellikleri override eder veya ek veriler taşır
    public bool IsProxyLoaded { get; set; } = true;
}

public class AddressInfo
{
    public string City { get; set; } = "Ankara";
    public int ZipCode { get; set; } = 6000;
    public int? ExtraCode { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? City { get; set; }
    public int ZipCode { get; set; }
    public int? ExtraCode { get; set; }
    public AddressDto? Address { get; set; }
}

public class AddressDto
{
    public string City { get; set; } = "";
    public int ZipCode { get; set; }
}

public class FlattenedUserDto
{
    public string? AddressCity { get; set; }
}

#endregion

/// <summary>
/// Dynamic Proxy unwrapping ve otomatik null propagation (güvenli üye erişimi) birim testleri.
/// </summary>
public class ProxyAndNullPropagationTests
{
    [Fact]
    public void GetUnproxiedType_ProxySinflarini_AtaSiniifaCozumlemeli()
    {
        // 1. Arrange & Act
        var normalType = typeof(BaseUser);
        var proxyType = typeof(BaseUserProxy);

        var resolvedNormal = Mapper.GetUnproxiedType(normalType);
        var resolvedProxy = Mapper.GetUnproxiedType(proxyType);

        // 2. Assert
        Assert.Equal(typeof(BaseUser), resolvedNormal);
        Assert.Equal(typeof(BaseUser), resolvedProxy);
    }

    [Fact]
    public void Map_ProxyNesnesiGonderildiginde_AtaSinifEslemesiniKullanmali()
    {
        // 1. Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<BaseUser, UserDto>();
        });
        var mapper = new Mapper(config);

        var proxyUser = new BaseUserProxy { Id = 1, Name = "Umut" };

        // 2. Act
        var dto = mapper.Map<UserDto>(proxyUser);

        // 3. Assert
        Assert.NotNull(dto);
        Assert.Equal(1, dto.Id);
        Assert.Equal("Umut", dto.Name);
    }

    [Fact]
    public void Map_NullPropagation_AraNesneNullIken_NullReferenceExceptionFirlatmamali()
    {
        // 1. Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<BaseUser, UserDto>()
               .ForMember(d => d.City, opt => opt.MapFrom(s => s.Address!.City))
               .ForMember(d => d.ZipCode, opt => opt.MapFrom(s => s.Address!.ZipCode))
               .ForMember(d => d.ExtraCode, opt => opt.MapFrom(s => s.Address!.ExtraCode));
        });
        var mapper = new Mapper(config);

        // Address null olarak ayarlandı
        var user = new BaseUser { Id = 1, Name = "Test", Address = null };

        // 2. Act
        var dto = mapper.Map<UserDto>(user);

        // 3. Assert
        Assert.NotNull(dto);
        Assert.Null(dto.City); // s.Address.City null dönmeli (NRE fırlatmadan)
        Assert.Equal(0, dto.ZipCode); // s.Address.ZipCode default(int) yani 0 dönmeli
        Assert.Null(dto.ExtraCode); // s.Address.ExtraCode default(int?) yani null dönmeli
    }

    [Fact]
    public void Map_FlatteningNullPropagation_AraNesneNullIken_NullReferenceExceptionFirlatmamali()
    {
        // 1. Arrange
        var config = new MapperConfiguration(cfg =>
        {
            // AddressCity -> Address.City flattening eşleşmesi otomatik yapılır
            cfg.CreateMap<BaseUser, FlattenedUserDto>();
        });
        var mapper = new Mapper(config);

        var user = new BaseUser { Id = 1, Name = "Test", Address = null };

        // 2. Act
        var dto = mapper.Map<FlattenedUserDto>(user);

        // 3. Assert
        Assert.NotNull(dto);
        Assert.Null(dto.AddressCity); // Address null olduğu için NameMatching/Flattening null dönmeli
    }

    [Fact]
    public void Map_NestedComplexTypeNullIken_HedefNesneyiNullBrakmali()
    {
        // 1. Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<BaseUser, UserDto>();
            cfg.CreateMap<AddressInfo, AddressDto>();
        });
        var mapper = new Mapper(config);

        var user = new BaseUser { Id = 1, Name = "Test", Address = null };

        // 2. Act
        var dto = mapper.Map<UserDto>(user);

        // 3. Assert
        Assert.NotNull(dto);
        Assert.Null(dto.Address); // Alt nesne kaynağı null iken hedef de null kalmalı
    }
}

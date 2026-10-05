using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using VeloxMapper.Configuration;
using VeloxMapper.Exceptions;

namespace VeloxMapper.Tests;

#region İzole Test Modelleri

public class SimpleEnumSrc { public SourceStatus Status { get; set; } }
public class SimpleEnumDst { public DestStatus Status { get; set; } }

public class SimpleNullableSrc { public int? Value { get; set; } }
public class SimpleNullableDst { public int Value { get; set; } }

public class SimpleCollectionSrc { public List<int> Values { get; set; } = new(); }
public class SimpleCollectionDst { public int[] Values { get; set; } = Array.Empty<int>(); }

public class SimpleFlattenSrc { public AddressSource Address { get; set; } = new(); }
public class SimpleFlattenDst { public string AddressCity { get; set; } = ""; }

public class CompatibilitySource
{
    public string Value1 { get; set; } = "V1";
    public string Value2 { get; set; } = "V2";
}

public class CompatibilityDest
{
    public string Value1 { get; set; } = "";
    public string Value2 { get; set; } = "";
}

public class FactorySource
{
    public string Name { get; set; } = "";
}

public class FactoryDest
{
    public string Name { get; }
    public string ExtraInfo { get; set; } = "";

    public FactoryDest(string name)
    {
        Name = name;
    }
}

#endregion

#region Test Modelleri ve Enum'ları

public enum SourceStatus
{
    New,
    Active,
    Suspended,
    Deleted
}

public enum DestStatus
{
    New,
    Active,
    Suspended,
    Deleted
}

public class AddressSource
{
    public string City { get; set; } = "Istanbul";
    public string Street { get; set; } = "Bagdat Cad.";
    public int ZipCode { get; set; } = 34000;
}

public class AddressDest
{
    public string City { get; set; } = "";
    public string Street { get; set; } = "";
    public int ZipCode { get; set; }
}

public class ItemSource
{
    public string Key { get; set; } = "";
    public double Value { get; set; }
}

public class ItemDest
{
    public string Key { get; set; } = "";
    public double Value { get; set; }
}

public class ComplexUserSource
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public SourceStatus Status { get; set; }
    public int? Score { get; set; }
    public double? NullableDouble { get; set; }
    public double ActiveScore { get; set; } = 99.9;
    public AddressSource Address { get; set; } = new();
    public List<ItemSource> Items { get; set; } = new();
    public string[] Tags { get; set; } = Array.Empty<string>();
}

public class ComplexUserDest
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public DestStatus Status { get; set; }
    public int Score { get; set; }
    public double NullableDouble { get; set; }
    public double? ActiveScore { get; set; }
    
    public string AddressCity { get; set; } = "";
    public string AddressStreet { get; set; } = "";
    public int AddressZipCode { get; set; }
    
    public AddressDest Address { get; set; } = new();
    
    public ItemDest[] Items { get; set; } = Array.Empty<ItemDest>();
    public List<string> Tags { get; set; } = new();
    
    public string IgnoredField { get; set; } = "Dokunulmamalı";
}

#endregion

#region Haritalama Profilleri

public class UserMappingProfile : VeloxProfile
{
    public UserMappingProfile()
    {
        CreateMap<AddressSource, AddressDest>();
        CreateMap<ItemSource, ItemDest>();
        CreateMap<ComplexUserSource, ComplexUserDest>()
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))
            .ForMember(dest => dest.IgnoredField, opt => opt.Ignore());

        // İzole test profilleri
        CreateMap<SimpleEnumSrc, SimpleEnumDst>();
        CreateMap<SimpleNullableSrc, SimpleNullableDst>();
        CreateMap<SimpleCollectionSrc, SimpleCollectionDst>();
        CreateMap<SimpleFlattenSrc, SimpleFlattenDst>();
    }
}

#endregion

public class VeloxMapperV4Tests
{
    private readonly MapperConfiguration _config;
    private readonly Mapper _mapper;

    public VeloxMapperV4Tests()
    {
        _config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<UserMappingProfile>();
        });
        _mapper = new Mapper(_config);
    }

    [Fact]
    public void Isolated_EnumMapping_Calismali()
    {
        var source = new SimpleEnumSrc { Status = SourceStatus.Active };
        var dest = _mapper.Map<SimpleEnumDst>(source);
        Assert.Equal(DestStatus.Active, dest.Status);
    }

    [Fact]
    public void Isolated_NullableMapping_Calismali()
    {
        var source = new SimpleNullableSrc { Value = 42 };
        var dest = _mapper.Map<SimpleNullableDst>(source);
        Assert.Equal(42, dest.Value);
    }

    [Fact]
    public void Isolated_CollectionMapping_Calismali()
    {
        var source = new SimpleCollectionSrc { Values = new List<int> { 1, 2, 3 } };
        var dest = _mapper.Map<SimpleCollectionDst>(source);
        Assert.Equal(new[] { 1, 2, 3 }, dest.Values);
    }

    [Fact]
    public void Isolated_FlattenMapping_Calismali()
    {
        var source = new SimpleFlattenSrc { Address = new AddressSource { City = "Izmir" } };
        var dest = _mapper.Map<SimpleFlattenDst>(source);
        Assert.Equal("Izmir", dest.AddressCity);
    }

    [Fact]
    public void Profile_YuklemeVeTemelMapping_BasariylaCalismali()
    {
        var source = new AddressSource { City = "Ankara", Street = "Cankaya", ZipCode = 6000 };
        var dest = _mapper.Map<AddressDest>(source);
        Assert.NotNull(dest);
        Assert.Equal("Ankara", dest.City);
        Assert.Equal("Cankaya", dest.Street);
        Assert.Equal(6000, dest.ZipCode);
    }

    [Fact]
    public void ForMember_IgnoreKurali_HedefiGuncellememeli()
    {
        var source = new ComplexUserSource { Id = 1, FirstName = "Ali", LastName = "Veli" };
        var dest = _mapper.Map<ComplexUserDest>(source);
        Assert.NotNull(dest);
        Assert.Equal("Dokunulmamalı", dest.IgnoredField);
    }

    [Fact]
    public void ForMember_MapFromKurali_OzelIfadeyiEslemeli()
    {
        var source = new ComplexUserSource { Id = 12, FirstName = "Kemal", LastName = "Sunal" };
        var dest = _mapper.Map<ComplexUserDest>(source);
        Assert.NotNull(dest);
        Assert.Equal("Kemal Sunal", dest.FullName);
    }

    [Fact]
    public void Enum_FarkliEnumlarArasiMapping_BasariylaCalismali()
    {
        var source = new ComplexUserSource { Status = SourceStatus.Suspended };
        var dest = _mapper.Map<ComplexUserDest>(source);
        Assert.NotNull(dest);
        Assert.Equal(DestStatus.Suspended, dest.Status);
    }

    [Fact]
    public void Nullable_SarmaVeAcmaIslemleri_BasariylaCalismali()
    {
        var source = new ComplexUserSource
        {
            Score = 150,
            NullableDouble = 12.5,
            ActiveScore = 88.4
        };
        var dest = _mapper.Map<ComplexUserDest>(source);
        Assert.NotNull(dest);
        Assert.Equal(150, dest.Score);
        Assert.Equal(12.5, dest.NullableDouble);
        Assert.Equal(88.4, dest.ActiveScore);
    }

    [Fact]
    public void Nullable_KaynakNullOldugunda_HedefeDefaultDegerAtamali()
    {
        var source = new ComplexUserSource { Score = null, NullableDouble = null };
        var dest = _mapper.Map<ComplexUserDest>(source);
        Assert.NotNull(dest);
        Assert.Equal(0, dest.Score);
        Assert.Equal(0.0, dest.NullableDouble);
    }

    [Fact]
    public void NestedObject_AltNesneHaritalama_BasariylaCalismali()
    {
        var source = new ComplexUserSource { Address = new AddressSource { City = "Izmir", Street = "Kordon", ZipCode = 35000 } };
        var dest = _mapper.Map<ComplexUserDest>(source);
        Assert.NotNull(dest.Address);
        Assert.Equal("Izmir", dest.Address.City);
        Assert.Equal("Kordon", dest.Address.Street);
        Assert.Equal(35000, dest.Address.ZipCode);
    }

    [Fact]
    public void Flattening_NestedOzellikIsmindenDuzlestirme_BasariylaCalismali()
    {
        var source = new ComplexUserSource { Address = new AddressSource { City = "Bursa", Street = "Heykel", ZipCode = 16000 } };
        var dest = _mapper.Map<ComplexUserDest>(source);
        Assert.Equal("Bursa", dest.AddressCity);
        Assert.Equal("Heykel", dest.AddressStreet);
        Assert.Equal(16000, dest.AddressZipCode);
    }

    [Fact]
    public void Collection_FarkliKoleksiyonTipleriArasiMapping_BasariylaCalismali()
    {
        var source = new ComplexUserSource
        {
            Items = new List<ItemSource>
            {
                new() { Key = "K1", Value = 1.1 },
                new() { Key = "K2", Value = 2.2 }
            },
            Tags = new[] { "tag1", "tag2", "tag3" }
        };
        var dest = _mapper.Map<ComplexUserDest>(source);
        Assert.NotNull(dest.Items);
        Assert.Equal(2, dest.Items.Length);
        Assert.Equal("K1", dest.Items[0].Key);
        Assert.Equal(1.1, dest.Items[0].Value);

        Assert.NotNull(dest.Tags);
        Assert.Equal(3, dest.Tags.Count);
        Assert.Equal("tag1", dest.Tags[0]);
    }

    [Fact]
    public void DI_AssemblyScanningVeServisKaydi_BasariylaCalismali()
    {
        var services = new ServiceCollection();
        services.AddVeloxMapper(cfg =>
        {
            cfg.AddProfilesFromAssembly(typeof(VeloxMapperV4Tests).Assembly);
        });
        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IVeloxMapper>();
        var source = new AddressSource { City = "Izmir", Street = "Bornova", ZipCode = 35100 };
        var dest = mapper.Map<AddressDest>(source);

        Assert.NotNull(mapper);
        Assert.NotNull(dest);
        Assert.Equal("Izmir", dest.City);
    }

    [Fact]
    public void Validation_CakisanHaritalama_FailFastHataFirlatmali()
    {
        Assert.Throws<VeloxConfigurationException>(() =>
        {
            _ = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<AddressSource, AddressDest>();
                cfg.AddProfile<UserMappingProfile>();
            });
        });
    }

    [Fact]
    public void Map_IEnumerableGirdiler_TopluListDondurmeli()
    {
        var sources = new List<AddressSource>
        {
            new() { City = "A", Street = "S1", ZipCode = 1 },
            new() { City = "B", Street = "S2", ZipCode = 2 }
        };
        var destList = _mapper.Map<List<AddressDest>>(sources);

        Assert.NotNull(destList);
        Assert.Equal(2, destList.Count);
        Assert.Equal("A", destList[0].City);
        Assert.Equal("B", destList[1].City);
    }

    [Fact]
    public void ReverseMap_OtomatikTersEslemeUretmeli()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CompatibilitySource, CompatibilityDest>().ReverseMap();
        });
        var mapper = new Mapper(config);

        // A -> B
        var src = new CompatibilitySource { Value1 = "A1", Value2 = "A2" };
        var dest = mapper.Map<CompatibilityDest>(src);
        Assert.Equal("A1", dest.Value1);
        Assert.Equal("A2", dest.Value2);

        // B -> A (Reverse)
        var reverseSrc = new CompatibilityDest { Value1 = "B1", Value2 = "B2" };
        var reverseDest = mapper.Map<CompatibilitySource>(reverseSrc);
        Assert.Equal("B1", reverseDest.Value1);
        Assert.Equal("B2", reverseDest.Value2);
    }

    [Fact]
    public void ConstructUsing_OzelFactoryCalismali()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<FactorySource, FactoryDest>()
                .ConstructUsing(src => new FactoryDest(src.Name + "_Custom"))
                .ForMember(d => d.ExtraInfo, opt => opt.MapFrom(s => "Extra_" + s.Name));
        });
        var mapper = new Mapper(config);

        var src = new FactorySource { Name = "Velox" };
        var dest = mapper.Map<FactoryDest>(src);

        Assert.Equal("Velox_Custom", dest.Name);
        Assert.Equal("Extra_Velox", dest.ExtraInfo);
    }

    [Fact]
    public void ForAllMembers_Ignore_TumAlanlarAtlanmali()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CompatibilitySource, CompatibilityDest>()
                .ForAllMembers(opt => opt.Ignore());
        });
        var mapper = new Mapper(config);

        var src = new CompatibilitySource { Value1 = "New1", Value2 = "New2" };
        var dest = mapper.Map<CompatibilityDest>(src);

        Assert.Equal("", dest.Value1);
        Assert.Equal("", dest.Value2);
    }

    [Fact]
    public void ForAllMembers_Condition_SadeceKosuluSaglayanlarAtanmali()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CompatibilitySource, CompatibilityDest>()
                .ForAllMembers(opt => opt.Condition((src, dest, val) => val?.ToString() == "Keep"));
        });
        var mapper = new Mapper(config);

        var src = new CompatibilitySource { Value1 = "Keep", Value2 = "Discard" };
        var dest = mapper.Map<CompatibilityDest>(src);

        Assert.Equal("Keep", dest.Value1);
        Assert.Equal("", dest.Value2);
    }
}



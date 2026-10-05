using System;
using Xunit;
using VeloxMapper.Configuration;

namespace VeloxMapper.Tests;

#region Test Modelleri

/// <summary>
/// Rekürsif ve döngüsel referans testleri için kullanılan kaynak düğüm sınıfı.
/// </summary>
public class NodeSrc
{
    public string Name { get; set; } = "";
    public NodeSrc? Child { get; set; }
}

/// <summary>
/// Rekürsif ve döngüsel referans testleri için kullanılan hedef düğüm sınıfı.
/// </summary>
public class NodeDst
{
    public string Name { get; set; } = "";
    public NodeDst? Child { get; set; }
}

#endregion

public class VeloxMapperV5AdvancedTests
{
    [Fact]
    public void PreserveReferences_DonguselReferanslari_Korumali_Ve_SonsuzDonguyu_Engellemeli()
    {
        // 1. Yapılandırma - PreserveReferences etkinleştirilmiş
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<NodeSrc, NodeDst>()
               .PreserveReferences();
        });

        var mapper = new Mapper(config);

        // 2. Döngüsel referansa sahip veri kümesi oluştur (Parent <-> Child)
        var parent = new NodeSrc { Name = "Parent" };
        var child = new NodeSrc { Name = "Child" };
        
        parent.Child = child;
        child.Child = parent; // Döngüsel bağlantı

        // 3. Mapping işlemini gerçekleştir
        var destParent = mapper.Map<NodeSrc, NodeDst>(parent);

        // 4. Doğrulamalar
        Assert.NotNull(destParent);
        Assert.Equal("Parent", destParent.Name);
        
        Assert.NotNull(destParent.Child);
        Assert.Equal("Child", destParent.Child.Name);

        // Döngüsel referansın doğru korunduğunu doğrula: parent.Child.Child == parent olmalı
        Assert.Same(destParent, destParent.Child.Child);
    }

    [Fact]
    public void MaxDepth_BelirtilenDerinlikSinirini_Asmamali()
    {
        // 1. Yapılandırma - MaxDepth derinlik sınırı 2 olarak belirlenmiş
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<NodeSrc, NodeDst>()
               .MaxDepth(2);
        });

        var mapper = new Mapper(config);

        // 2. 3 seviyeli derinliğe sahip veri kümesi oluştur (Level 1 -> Level 2 -> Level 3)
        var level1 = new NodeSrc { Name = "Level 1" };
        var level2 = new NodeSrc { Name = "Level 2" };
        var level3 = new NodeSrc { Name = "Level 3" };

        level1.Child = level2;
        level2.Child = level3;

        // 3. Mapping işlemini gerçekleştir
        var dest = mapper.Map<NodeSrc, NodeDst>(level1);

        // 4. Doğrulamalar
        Assert.NotNull(dest);
        Assert.Equal("Level 1", dest.Name);

        // 2. seviye (Level 2) map edilmiş olmalı
        Assert.NotNull(dest.Child);
        Assert.Equal("Level 2", dest.Child.Name);

        // 3. seviye (Level 3) derinlik sınırı 2 olduğu için null olmalı (aşılmamalı)
        Assert.Null(dest.Child.Child);
    }

    [Fact]
    public void Include_PolimorfikHaritalamayi_Desteklemeli()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Animal, AnimalDto>()
               .Include<Dog, DogDto>();
            cfg.CreateMap<Dog, DogDto>();
        });

        var mapper = new Mapper(config);

        Animal sourceDog = new Dog { Name = "Karabas", Breed = "Sivas Kangali" };
        var destDogDto = mapper.Map<Animal, AnimalDto>(sourceDog);

        Assert.NotNull(destDogDto);
        var dogDto = Assert.IsType<DogDto>(destDogDto);
        Assert.Equal("Karabas", dogDto.Name);
        Assert.Equal("Sivas Kangali", dogDto.Breed);
    }

    [Fact]
    public void IncludeAllDerived_PolimorfikHaritalamayi_Otomatik_Tanimlamali()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Animal, AnimalDto>()
               .IncludeAllDerived();
            cfg.CreateMap<Dog, DogDto>();
        });

        var mapper = new Mapper(config);

        Animal sourceDog = new Dog { Name = "Karabas", Breed = "Sivas Kangali" };
        var destDogDto = mapper.Map<Animal, AnimalDto>(sourceDog);

        Assert.NotNull(destDogDto);
        var dogDto = Assert.IsType<DogDto>(destDogDto);
        Assert.Equal("Karabas", dogDto.Name);
        Assert.Equal("Sivas Kangali", dogDto.Breed);
    }

    [Fact]
    public void IncludeBase_BaseHaritalamaKurallarini_MirasAlmali()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Animal, AnimalDto>()
               .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name + " (Animal)"));
               
            cfg.CreateMap<Dog, DogDto>()
               .IncludeBase<Animal, AnimalDto>();
        });

        var mapper = new Mapper(config);

        var dog = new Dog { Name = "Karabas", Breed = "Kangal" };
        var dogDto = mapper.Map<Dog, DogDto>(dog);

        Assert.NotNull(dogDto);
        Assert.Equal("Karabas (Animal)", dogDto.Name);
        Assert.Equal("Kangal", dogDto.Breed);
    }

    [Fact]
    public void ForPath_DerinYolaAtama_Yapabilmeli_Ve_NullNesneleri_Olusturabilmeli()
    {
        // 1. Yapılandırma - ForPath kuralı eklenmiş
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<MainSrc, MainDest>()
               .ForPath(dest => dest.Sub!.City, opt => opt.MapFrom(src => src.SourceCity))
               .ForPath(dest => dest.Sub!.Street, opt => opt.MapFrom(src => src.SourceStreet));
        });

        var mapper = new Mapper(config);

        var src = new MainSrc
        {
            Name = "Test",
            SourceCity = "Istanbul",
            SourceStreet = "Kadikoy"
        };

        // 2. Mapping işlemini gerçekleştir
        var dest = mapper.Map<MainSrc, MainDest>(src);

        // 3. Doğrulamalar
        Assert.NotNull(dest);
        Assert.Equal("Test", dest.Name);
        Assert.NotNull(dest.Sub);
        Assert.Equal("Istanbul", dest.Sub.City);
        Assert.Equal("Kadikoy", dest.Sub.Street);
    }

    [Fact]
    public void ForCtorParam_ConstructorParametresini_Esleyebilmeli()
    {
        // 1. Yapılandırma - ForCtorParam kuralı eklenmiş
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ImmutableSrc, ImmutableDest>()
               .ForCtorParam("fullName", opt => opt.MapFrom(src => src.Name + " (Ctor)"));
        });

        var mapper = new Mapper(config);

        var src = new ImmutableSrc { Name = "Ahmet", Age = 30 };

        // 2. Mapping işlemini gerçekleştir
        var dest = mapper.Map<ImmutableSrc, ImmutableDest>(src);

        // 3. Doğrulamalar
        Assert.NotNull(dest);
        Assert.Equal("Ahmet (Ctor)", dest.FullName);
        Assert.Equal(30, dest.Age);
    }

    [Fact]
    public void ValueTransformers_GlobalVeProfilSeviyesinde_Calismali()
    {
        // 1. Yapılandırma - Global ve Profil düzeyinde transformer'lar eklenmiş
        var config = new MapperConfiguration(cfg =>
        {
            cfg.ValueTransformers.Add<string>(val => val != null ? val.Trim() : val!);
            cfg.AddProfile(new TestTransformerProfile());
        });

        var mapper = new Mapper(config);

        var src = new TransformerSrc { Text = "  Hello World  ", Value = 5 };

        // 2. Mapping işlemini gerçekleştir
        var dest = mapper.Map<TransformerSrc, TransformerDest>(src);

        // 3. Doğrulamalar
        Assert.NotNull(dest);
        Assert.Equal("Hello World", dest.Text); // Global transformer tarafından trim edildi
        Assert.Equal(10, dest.Value); // Profil transformer tarafından 2 ile çarpıldı
    }

    [Fact]
    public void OpenGenerics_BasitGenericTipleri_BasariylaEslemeli()
    {
        // 1. Yapılandırma - Open Generic CreateMap(Type, Type) çağrısı
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap(typeof(GenericSrc<>), typeof(GenericDest<>));
        });

        var mapper = new Mapper(config);

        var sourceString = new GenericSrc<string> { Value = "Velox" };
        var sourceInt = new GenericSrc<int> { Value = 42 };

        // 2. Mapping
        var destString = mapper.Map<GenericSrc<string>, GenericDest<string>>(sourceString);
        var destInt = mapper.Map<GenericSrc<int>, GenericDest<int>>(sourceInt);

        // 3. Doğrulama
        Assert.NotNull(destString);
        Assert.Equal("Velox", destString.Value);

        Assert.NotNull(destInt);
        Assert.Equal(42, destInt.Value);
    }

    [Fact]
    public void ConvertUsing_LambdaOverload_Ile_BasitDonusumleri_Yapabilmeli()
    {
        // 1. Yapılandırma - ConvertUsing lambda kullanımı
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<TransformerSrc, TransformerDest>()
               .ConvertUsing(src => new TransformerDest 
               { 
                   Text = src != null ? src.Text + " (Lambda)" : "",
                   Value = src != null ? src.Value + 100 : 0
               });
        });

        var mapper = new Mapper(config);

        var src = new TransformerSrc { Text = "Velox", Value = 10 };

        // 2. Mapping
        var dest = mapper.Map<TransformerSrc, TransformerDest>(src);

        // 3. Doğrulamalar
        Assert.NotNull(dest);
        Assert.Equal("Velox (Lambda)", dest.Text);
        Assert.Equal(110, dest.Value);
    }

    [Fact]
    public void UseDestinationValue_MevcutHedefReferanslarini_Ve_Koleksiyonlari_Korumali()
    {
        // 1. Yapılandırma - UseDestinationValue etkinleştirilmiş
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<NestedSrc, NestedDest>();
            cfg.CreateMap<UseDestValueSrc, UseDestValueDest>()
               .ForMember(dest => dest.Nested, opt => opt.UseDestinationValue())
               .ForMember(dest => dest.Ints, opt => opt.UseDestinationValue());
        });

        var mapper = new Mapper(config);

        var src = new UseDestValueSrc
        {
            Nested = new NestedSrc { Name = "New Name" },
            Ints = new List<int> { 4, 5, 6 }
        };

        var existingNested = new NestedDest { Name = "Old Name" };
        var existingInts = new List<int> { 1, 2, 3 };

        var dest = new UseDestValueDest
        {
            Nested = existingNested,
            Ints = existingInts
        };

        // 2. Mapping
        mapper.Map(src, dest);

        // 3. Doğrulamalar
        Assert.Same(existingNested, dest.Nested); // Nested referansı korunmuş olmalı!
        Assert.Equal("New Name", dest.Nested?.Name); // Değeri güncellenmiş olmalı

        Assert.Same(existingInts, dest.Ints); // Koleksiyon referansı korunmuş olmalı!
        Assert.Equal(3, dest.Ints.Count);
        Assert.Equal(4, dest.Ints[0]);
        Assert.Equal(5, dest.Ints[1]);
        Assert.Equal(6, dest.Ints[2]);
    }

    [Fact]
    public void As_EslemeSonucunu_BaskaHedefTipe_Yonlendirmeli()
    {
        // 1. Yapılandırma - As<T> kullanımı
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<AsSource, BaseDest>()
               .As<DerivedDest>();
            cfg.CreateMap<AsSource, DerivedDest>();
        });

        var mapper = new Mapper(config);

        var src = new AsSource { BaseProp = "Base", DerivedProp = "Derived" };

        // 2. Mapping
        var result = mapper.Map<AsSource, BaseDest>(src);

        // 3. Doğrulamalar
        Assert.NotNull(result);
        var derivedResult = Assert.IsType<DerivedDest>(result);
        Assert.Equal("Base", derivedResult.BaseProp);
        Assert.Equal("Derived", derivedResult.DerivedProp);
    }

    [Fact]
    public void SetMappingOrder_PropertyEslemeSirasini_Belirlemeli()
    {
        // 1. Yapılandırma - MappingOrder kuralları
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<OrderSource, OrderDest>()
               .ForMember(dest => dest.Prop1, opt => {
                   opt.MapFrom(src => src.Prop1);
                   opt.SetMappingOrder(2);
               })
               .ForMember(dest => dest.Prop2, opt => {
                   opt.MapFrom(src => src.Prop2);
                   opt.SetMappingOrder(1);
               });
        });

        var mapper = new Mapper(config);

        var src = new OrderSource { Prop1 = "Value1", Prop2 = "Value2" };

        // 2. Mapping
        var dest = mapper.Map<OrderSource, OrderDest>(src);

        // 3. Doğrulama
        Assert.NotNull(dest);
        Assert.Equal(2, dest.ExecutionOrder.Count);
        Assert.Equal("Prop2", dest.ExecutionOrder[0]);
        Assert.Equal("Prop1", dest.ExecutionOrder[1]);
    }
}

#region Test Modelleri 2

public class Animal
{
    public string Name { get; set; } = "";
}

public class Dog : Animal
{
    public string Breed { get; set; } = "";
}

public class AnimalDto
{
    public string Name { get; set; } = "";
}

public class DogDto : AnimalDto
{
    public string Breed { get; set; } = "";
}

public class SubDest
{
    public string City { get; set; } = "";
    public string Street { get; set; } = "";
}

public class MainDest
{
    public string Name { get; set; } = "";
    public SubDest? Sub { get; set; }
}

public class MainSrc
{
    public string Name { get; set; } = "";
    public string SourceCity { get; set; } = "";
    public string SourceStreet { get; set; } = "";
}

public record ImmutableDest(string FullName, int Age);

public class ImmutableSrc
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

public class TransformerSrc
{
    public string Text { get; set; } = "";
    public int Value { get; set; }
}

public class TransformerDest
{
    public string Text { get; set; } = "";
    public int Value { get; set; }
}

public class TestTransformerProfile : VeloxProfile
{
    public TestTransformerProfile()
    {
        ValueTransformers.Add<int>(val => val * 2);
        CreateMap<TransformerSrc, TransformerDest>();
    }
}

public class GenericSrc<T>
{
    public T Value { get; set; } = default!;
}

public class GenericDest<T>
{
    public T Value { get; set; } = default!;
}

public class NestedSrc
{
    public string Name { get; set; } = "";
}

public class NestedDest
{
    public string Name { get; set; } = "";
}

public class UseDestValueSrc
{
    public NestedSrc? Nested { get; set; }
    public List<int> Ints { get; set; } = new();
}

public class UseDestValueDest
{
    public NestedDest? Nested { get; set; }
    public List<int> Ints { get; set; } = new();
}

public class AsSource
{
    public string BaseProp { get; set; } = "";
    public string DerivedProp { get; set; } = "";
}

public class BaseDest
{
    public string BaseProp { get; set; } = "";
}

public class DerivedDest : BaseDest
{
    public string DerivedProp { get; set; } = "";
}

public class OrderSource
{
    public string Prop1 { get; set; } = "";
    public string Prop2 { get; set; } = "";
}

public class OrderDest
{
    public List<string> ExecutionOrder { get; } = new();

    private string _prop1 = "";
    public string Prop1
    {
        get => _prop1;
        set
        {
            _prop1 = value;
            ExecutionOrder.Add("Prop1");
        }
    }

    private string _prop2 = "";
    public string Prop2
    {
        get => _prop2;
        set
        {
            _prop2 = value;
            ExecutionOrder.Add("Prop2");
        }
    }
}

#endregion

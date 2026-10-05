// AutoMapper'ın belgelenmiş davranışlarının VeloxMapper'da aynı sonucu verdiğini doğrular.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VeloxMapper;
using VeloxMapper.Configuration.Annotations;
using VeloxMapper.Exceptions;
using VeloxMapper.QueryableExtensions;

namespace VeloxMapper.Tests.Migration.Behavior;

#region Modeller

public abstract class Animal { public string Name { get; set; } = ""; }
public class Dog : Animal { public bool Barks { get; set; } }
public class Cat : Animal { public int Lives { get; set; } }
public abstract class AnimalDto { public string Name { get; set; } = ""; public string Kind { get; set; } = ""; }
public class DogDto : AnimalDto { public bool Barks { get; set; } }
public class CatDto : AnimalDto { public int Lives { get; set; } }

public record PersonRecord(string FirstName, string LastName, int Age);
public class PersonSource { public string FirstName { get; set; } = ""; public string LastName { get; set; } = ""; public int Age { get; set; } public string? Nick { get; set; } }

public class Node { public string Name { get; set; } = ""; public Node? Parent { get; set; } public List<Node> Children { get; set; } = new(); }
public class NodeDto { public string Name { get; set; } = ""; public NodeDto? Parent { get; set; } public List<NodeDto> Children { get; set; } = new(); }

public class Primitives { public int Number { get; set; } public string Text { get; set; } = ""; public double Ratio { get; set; } public string Guid { get; set; } = ""; public int? Maybe { get; set; } public string Flag { get; set; } = ""; }
public class PrimitivesDto { public string Number { get; set; } = ""; public int Text { get; set; } public int Ratio { get; set; } public Guid Guid { get; set; } public int Maybe { get; set; } public bool Flag { get; set; } }

public class Paged<T> { public List<T> Items { get; set; } = new(); public int Total { get; set; } }
public class PagedDto<T> { public List<T> Items { get; set; } = new(); public int Total { get; set; } }

public class Holder { public List<string> Tags { get; set; } = new(); public Dictionary<string, int> Scores { get; set; } = new(); public string[] Codes { get; set; } = Array.Empty<string>(); }
public class HolderDto
{
    public ObservableCollection<string> Tags { get; } = new();             // setter'ı yok: yerinde doldurulur
    public IReadOnlyDictionary<string, long> Scores { get; set; } = new Dictionary<string, long>();
    public HashSet<string> Codes { get; set; } = new();
}

public class Envelope { public Inner Inner { get; set; } = new(); public int Id { get; set; } }
public class Inner { public string Title { get; set; } = ""; public string Body { get; set; } = ""; }
public class FlatEnvelope { public int Id { get; set; } public string Title { get; set; } = ""; public string Body { get; set; } = ""; }

public class SnakeSource { public string first_name { get; set; } = ""; public int user_age { get; set; } }
public class PascalDest { public string FirstName { get; set; } = ""; public int UserAge { get; set; } }

public class SnakeProfile : Profile
{
    public SnakeProfile()
    {
        SourceMemberNamingConvention = new LowerUnderscoreNamingConvention();
        DestinationMemberNamingConvention = new PascalCaseNamingConvention();
        CreateMap<SnakeSource, PascalDest>();
    }
}

public class UpdateUserDto { public string? Name { get; set; } public string? Email { get; set; } public int? Age { get; set; } }
public class User { public string Name { get; set; } = "eski"; public string Email { get; set; } = "eski@x"; public int Age { get; set; } = 30; }

[AutoMap(typeof(Primitives))]
public class AttributeDto
{
    [SourceMember(nameof(Primitives.Text))]
    public string Caption { get; set; } = "";

    [Ignore]
    public string Secret { get; set; } = "korunur";

    public int Number { get; set; }
}

public class Money { public decimal Value { get; set; } public static implicit operator decimal(Money m) => m.Value; }
public class PriceSource { public Money Price { get; set; } = new(); }
public class PriceDest { public decimal Price { get; set; } }

#endregion

public class AutoMapperBehaviorParityTests
{
    [Fact]
    public void Kalitim_Include_ve_IncludeBase_polimorfik_esler()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Animal, AnimalDto>()
                .ForMember(d => d.Kind, o => o.MapFrom(s => s.GetType().Name))
                .Include<Dog, DogDto>()
                .Include<Cat, CatDto>();
            cfg.CreateMap<Dog, DogDto>();
            cfg.CreateMap<Cat, CatDto>().IncludeBase<Animal, AnimalDto>();
        });
        config.AssertConfigurationIsValid();
        var mapper = config.CreateMapper();

        var animals = new List<Animal> { new Dog { Name = "Karabaş", Barks = true }, new Cat { Name = "Tekir", Lives = 9 } };
        var dtos = mapper.Map<List<AnimalDto>>(animals);

        var dog = Assert.IsType<DogDto>(dtos[0]);
        var cat = Assert.IsType<CatDto>(dtos[1]);
        Assert.True(dog.Barks);
        Assert.Equal("Dog", dog.Kind);       // taban ForMember kuralı türetilmiş eşleştirmeye devredildi
        Assert.Equal(9, cat.Lives);
        Assert.Equal("Cat", cat.Kind);
    }

    [Fact]
    public void Record_kurucu_ile_ve_ForCtorParam()
    {
        var config = new MapperConfiguration(cfg =>
            cfg.CreateMap<PersonSource, PersonRecord>()
                .ForCtorParam(nameof(PersonRecord.LastName), o => o.MapFrom(s => s.LastName.ToUpperInvariant())));
        config.AssertConfigurationIsValid();

        var person = config.CreateMapper().Map<PersonRecord>(new PersonSource { FirstName = "Ada", LastName = "Lovelace", Age = 36 });
        Assert.Equal(new PersonRecord("Ada", "LOVELACE", 36), person);
    }

    [Fact]
    public void Dongusel_grafik_PreserveReferences_otomatik_etkinlesir()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<Node, NodeDto>());
        var root = new Node { Name = "kök" };
        var child = new Node { Name = "çocuk", Parent = root };
        root.Children.Add(child);

        var dto = config.CreateMapper().Map<NodeDto>(root);

        Assert.Equal("çocuk", dto.Children[0].Name);
        Assert.Same(dto, dto.Children[0].Parent); // döngü korunur, StackOverflow oluşmaz
    }

    [Fact]
    public void Yerlesik_tur_donusumleri()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<Primitives, PrimitivesDto>());
        config.AssertConfigurationIsValid();
        var guid = Guid.NewGuid();

        var dto = config.CreateMapper().Map<PrimitivesDto>(new Primitives { Number = 42, Text = "17", Ratio = 2.5, Guid = guid.ToString(), Maybe = null, Flag = "true" });

        Assert.Equal("42", dto.Number);
        Assert.Equal(17, dto.Text);
        Assert.Equal(2, dto.Ratio);            // System.Convert (banker's rounding) — AutoMapper ile aynı
        Assert.Equal(guid, dto.Guid);
        Assert.Equal(0, dto.Maybe);
        Assert.True(dto.Flag);
    }

    [Fact]
    public void Kullanici_tanimli_donusum_operatoru_kullanilir()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<PriceSource, PriceDest>());
        Assert.Equal(9.99m, config.CreateMapper().Map<PriceDest>(new PriceSource { Price = new Money { Value = 9.99m } }).Price);
    }

    [Fact]
    public void Open_generic_esleme()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap(typeof(Paged<>), typeof(PagedDto<>)).ReverseMap();
            cfg.CreateMap<Animal, AnimalDto>().Include<Dog, DogDto>().ForMember(d => d.Kind, o => o.Ignore());
            cfg.CreateMap<Dog, DogDto>();
        });
        var mapper = config.CreateMapper();

        var dto = mapper.Map<PagedDto<string>>(new Paged<string> { Items = { "a", "b" }, Total = 2 });
        var back = mapper.Map<Paged<string>>(dto);
        var dogs = mapper.Map<PagedDto<DogDto>>(new Paged<Dog> { Items = { new Dog { Name = "x", Barks = true } }, Total = 1 });

        Assert.Equal(2, dto.Total);
        Assert.Equal(new[] { "a", "b" }, back.Items);
        Assert.True(dogs.Items[0].Barks);
    }

    [Fact]
    public void Koleksiyon_turleri_sozluk_ve_setter_siz_koleksiyon()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<Holder, HolderDto>());
        var dto = config.CreateMapper().Map<HolderDto>(new Holder
        {
            Tags = { "a", "b" },
            Scores = { ["x"] = 1 },
            Codes = new[] { "c1", "c1", "c2" }
        });

        Assert.Equal(new[] { "a", "b" }, dto.Tags);
        Assert.Equal(1L, dto.Scores["x"]);
        Assert.Equal(2, dto.Codes.Count);
    }

    [Fact]
    public void IncludeMembers_alt_nesneyi_duzlestirir()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Envelope, FlatEnvelope>().IncludeMembers(s => s.Inner);
            cfg.CreateMap<Inner, FlatEnvelope>(MemberList.None);
        });
        config.AssertConfigurationIsValid();

        var flat = config.CreateMapper().Map<FlatEnvelope>(new Envelope { Id = 1, Inner = new Inner { Title = "Başlık", Body = "Metin" } });
        Assert.Equal("Başlık", flat.Title);
        Assert.Equal("Metin", flat.Body);
    }

    [Fact]
    public void Profil_duzeyinde_isimlendirme_kurali()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<SnakeProfile>());
        config.AssertConfigurationIsValid();
        var dto = config.CreateMapper().Map<PascalDest>(new SnakeSource { first_name = "Linus", user_age = 55 });
        Assert.Equal("Linus", dto.FirstName);
        Assert.Equal(55, dto.UserAge);
    }

    [Fact]
    public void PATCH_ForAllMembers_Condition_null_degerleri_atlar()
    {
        var config = new MapperConfiguration(cfg =>
            cfg.CreateMap<UpdateUserDto, User>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null)));
        var user = new User();

        config.CreateMapper().Map(new UpdateUserDto { Email = "yeni@x" }, user);

        Assert.Equal("eski", user.Name);
        Assert.Equal("yeni@x", user.Email);
        Assert.Equal(30, user.Age);
    }

    [Fact]
    public void AutoMap_ozniteligi_ve_uye_anotasyonlari()
    {
        var config = new MapperConfiguration(cfg => cfg.AddMaps(typeof(AttributeDto)));
        var dto = config.CreateMapper().Map<AttributeDto>(new Primitives { Text = "metin", Number = 3 });

        Assert.Equal("metin", dto.Caption);
        Assert.Equal("korunur", dto.Secret);
        Assert.Equal(3, dto.Number);
    }

    [Fact]
    public void ConvertUsingEnumMapping_ve_ReverseMap()
    {
        var config = new MapperConfiguration(cfg =>
            cfg.CreateMap<OrderStatus, OrderStatusDto>()
                .ConvertUsingEnumMapping(o => o.MapByName().MapValue(OrderStatus.Pending, OrderStatusDto.Shipped))
                .ReverseMap());
        var mapper = config.CreateMapper();

        Assert.Equal(OrderStatusDto.Delivered, mapper.Map<OrderStatusDto>(OrderStatus.Delivered));
        Assert.Equal(OrderStatusDto.Shipped, mapper.Map<OrderStatusDto>(OrderStatus.Pending));
        Assert.Equal(OrderStatus.Pending, mapper.Map<OrderStatus>(OrderStatusDto.Shipped));
    }

    [Fact]
    public void Global_ayarlar_ignore_onek_transformer_ve_AllowNullCollections()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AllowNullCollections = true;
            cfg.AddGlobalIgnore("Audit");
            cfg.RecognizePrefixes("src");
            cfg.ValueTransformers.Add<string>(s => s.Trim());
            cfg.CreateMap<PrefixedSource, PrefixedDest>();
        });
        var dto = config.CreateMapper().Map<PrefixedDest>(new PrefixedSource { srcTitle = "  baslik  ", Values = null });

        Assert.Equal("baslik", dto.Title);
        Assert.Null(dto.Values);
        Assert.Equal("dokunulmadı", dto.AuditTrail);
    }

    [Fact]
    public void ForAllMaps_ve_ForAllPropertyMaps()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.RecognizePrefixes("src");
            cfg.CreateMap<PrefixedSource, PrefixedDest>();
            cfg.ForAllMaps((typeMap, map) => map.ForMember(nameof(PrefixedDest.AuditTrail), o => o.Ignore()));
            cfg.ForAllPropertyMaps(pm => pm.DestinationType == typeof(string) && pm.DestinationName == nameof(PrefixedDest.Title),
                (pm, o) => o.NullSubstitute("boş"));
        });
        config.AssertConfigurationIsValid();
        var dto = config.CreateMapper().Map<PrefixedDest>(new PrefixedSource { srcTitle = null! });
        Assert.Equal("boş", dto.Title);
    }

    [Fact]
    public void Dogrulama_eslenmemis_uyeleri_ve_kaynak_uyelerini_raporlar()
    {
        var destination = new MapperConfiguration(cfg => cfg.CreateMap<Primitives, AttributeDto>());
        var ex = Assert.Throws<VeloxValidationException>(() => destination.AssertConfigurationIsValid());
        Assert.Contains("Caption", ex.Message);

        var source = new MapperConfiguration(cfg => cfg.CreateMap<PersonSource, PascalDest>(MemberList.Source).ForMember(d => d.FirstName, o => o.MapFrom(s => s.FirstName)));
        var sourceEx = Assert.Throws<VeloxValidationException>(() => source.AssertConfigurationIsValid());
        Assert.Contains("LastName", sourceEx.Message);
    }

    [Fact]
    public void ProjectTo_parametre_ve_ExplicitExpansion()
    {
        string? currentUser = null;
        var config = new MapperConfiguration(cfg =>
            cfg.CreateMap<PersonSource, ProjectedPerson>()
                .ForMember(d => d.Viewer, o => o.MapFrom(s => currentUser))
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.FirstName + " " + s.LastName))
                .ForMember(d => d.Nick, o => o.ExplicitExpansion()));
        var people = new[] { new PersonSource { FirstName = "Ada", LastName = "L", Nick = "countess" } }.AsQueryable();

        var plain = people.ProjectTo<ProjectedPerson>(config, new { currentUser = "admin" }).Single();
        var expanded = people.ProjectTo<ProjectedPerson>(config, (object?)null, d => d.Nick).Single();

        Assert.Equal("admin", plain.Viewer);
        Assert.Equal("Ada L", plain.FullName);
        Assert.Null(plain.Nick);
        Assert.Equal("countess", expanded.Nick);
    }

    [Fact]
    public void BuildExecutionPlan_ve_CompileMappings()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<PersonSource, PascalDest>(MemberList.None));
        config.CompileMappings();
        var plan = config.BuildExecutionPlan(typeof(PersonSource), typeof(PascalDest));
        Assert.Equal(typeof(PascalDest), plan.ReturnType);
    }

    [Fact]
    public void Kurucu_secimi_en_cok_parametreli_cozulebilen_kurucu()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<PersonSource, CtorDto>());
        var dto = config.CreateMapper().Map<CtorDto>(new PersonSource { FirstName = "Ada", Age = 3 });
        Assert.Equal("ctor:Ada:3", dto.Origin);
    }

    [Fact]
    public void As_ve_ConstructUsing()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Animal, AnimalDto>().As<DogDto>();
            cfg.CreateMap<Animal, DogDto>().ConstructUsing(src => new DogDto { Barks = true }).ForMember(d => d.Kind, o => o.Ignore());
        });
        var dto = config.CreateMapper().Map<AnimalDto>(new Cat { Name = "x" });
        var dog = Assert.IsType<DogDto>(dto);
        Assert.True(dog.Barks);
        Assert.Equal("x", dog.Name);
    }

    [Fact]
    public void Adi_Proxy_ile_biten_normal_sinif_yanlislikla_cozulmez()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<PaymentProxy, PaymentDto>());
        Assert.Equal(10, config.CreateMapper().Map<PaymentDto>(new PaymentProxy { Amount = 10 }).Amount);
    }
}

public class PrefixedSource { public string srcTitle { get; set; } = ""; public List<int>? Values { get; set; } public string AuditTrail { get; set; } = "kaynak"; }
public class PrefixedDest { public string? Title { get; set; } public List<int>? Values { get; set; } public string AuditTrail { get; set; } = "dokunulmadı"; }
public class ProjectedPerson { public string? Viewer { get; set; } public string FullName { get; set; } = ""; public string? Nick { get; set; } }
public class CtorDto
{
    public CtorDto() => Origin = "default";
    public CtorDto(string firstName, int age) => Origin = $"ctor:{firstName}:{age}";
    public string Origin { get; }
}
public class PaymentProxy { public int Amount { get; set; } }
public class PaymentDto { public int Amount { get; set; } }

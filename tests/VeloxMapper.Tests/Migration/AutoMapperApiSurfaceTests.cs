// AutoMapper API yüzeyinin daha az kullanılan ama geçişte karşılaşılan parçaları.

using System;
using System.Collections.Generic;
using System.Linq;
using VeloxMapper;
using VeloxMapper.Exceptions;

namespace VeloxMapper.Tests.Migration.Surface;

public class Src { public int Id { get; set; } public string Name { get; set; } = ""; public Sub Sub { get; set; } = new(); public decimal Price { get; set; } public string Ünvan { get; set; } = ""; }
public class Sub { public string Code { get; set; } = ""; public int Level { get; set; } }
public struct PointDto { public int Id { get; set; } public string Name { get; set; } }
public class FieldDto { public int Id; public string Label = ""; }
public class Dst { public int Id { get; set; } public string Code { get; set; } = ""; public string Display { get; set; } = ""; public string Price { get; set; } = ""; public string Unvan { get; set; } = ""; }
public class Wrapper<T> { public T Value { get; set; } = default!; }
public class WrapperDto<T> { public T Value { get; set; } = default!; public string Origin { get; set; } = ""; }
public class WrapperConverter<T> : ITypeConverter<Wrapper<T>, WrapperDto<T>>
{
    public WrapperDto<T> Convert(Wrapper<T> source, WrapperDto<T> destination, ResolutionContext context) => new() { Value = source.Value, Origin = "converter" };
}
public interface IHasName { string Name { get; } }
public class Named : IHasName { public string Name { get; set; } = ""; }
public class Tree { public string Name { get; set; } = ""; public Tree? Child { get; set; } }
public class TreeDto { public string Name { get; set; } = ""; public TreeDto? Child { get; set; } }
public static class SrcExtensions { public static string GetDisplay(this Src src) => src.Name + "#" + src.Id; }
public class PriceConverter : IValueConverter<decimal, string> { public string Convert(decimal sourceMember, ResolutionContext context) => sourceMember.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); }
public class CodeResolver : IMemberValueResolver<Src, Dst, Sub, string> { public string Resolve(Src source, Dst destination, Sub sourceMember, string destMember, ResolutionContext context) => sourceMember.Code + "!"; }
public class OnlyDefaultCtor { public OnlyDefaultCtor() { } public OnlyDefaultCtor(int id) => Id = id * 100; public int Id { get; set; } }

public class InlineValidatedProfile : Profile
{
    public InlineValidatedProfile() => CreateMap<Src, Dst>(MemberList.None);
}

public class AutoMapperApiSurfaceTests
{
    [Fact]
    public void Struct_ve_public_field_hedefler()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Src, PointDto>();
            cfg.CreateMap<Src, FieldDto>().ForMember(d => d.Label, o => o.MapFrom(s => s.Name));
        });
        config.AssertConfigurationIsValid();
        var mapper = config.CreateMapper();

        Assert.Equal("a", mapper.Map<PointDto>(new Src { Id = 1, Name = "a" }).Name);
        Assert.Equal("b", mapper.Map<FieldDto>(new Src { Name = "b" }).Label);
    }

    [Fact]
    public void String_tabanli_ForMember_ve_MapFrom_yolu()
    {
        var config = new MapperConfiguration(cfg =>
            cfg.CreateMap<Src, Dst>(MemberList.None)
                .ForMember("Code", o => o.MapFrom("Sub.Code")));
        Assert.Equal("X1", config.CreateMapper().Map<Dst>(new Src { Sub = new Sub { Code = "X1" } }).Code);
    }

    [Fact]
    public void Value_converter_ayni_adli_uye_ve_member_value_resolver_ad_ile()
    {
        var config = new MapperConfiguration(cfg =>
            cfg.CreateMap<Src, Dst>(MemberList.None)
                .ForMember(d => d.Price, o => o.ConvertUsing<PriceConverter, decimal>())
                .ForMember(d => d.Code, o => o.MapFrom<CodeResolver, Sub>("Sub")));
        var dto = config.CreateMapper().Map<Dst>(new Src { Price = 3.14m, Sub = new Sub { Code = "Z" } });
        Assert.Equal("3.1", dto.Price);
        Assert.Equal("Z!", dto.Code);
    }

    [Fact]
    public void Open_generic_tip_donusturucu()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap(typeof(Wrapper<>), typeof(WrapperDto<>)).ConvertUsing(typeof(WrapperConverter<>)));
        var dto = config.CreateMapper().Map<WrapperDto<int>>(new Wrapper<int> { Value = 5 });
        Assert.Equal(5, dto.Value);
        Assert.Equal("converter", dto.Origin);
    }

    [Fact]
    public void Arayuz_kaynak_turu()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<IHasName, Dst>(MemberList.None).ForMember(d => d.Display, o => o.MapFrom(s => s.Name)));
        IHasName source = new Named { Name = "n" };
        Assert.Equal("n", config.CreateMapper().Map<IHasName, Dst>(source).Display);
        Assert.Equal("n", config.CreateMapper().Map<Dst>(source).Display); // çalışma zamanı türü Named → arayüz kaydı bulunur
    }

    [Fact]
    public void MaxDepth_derin_grafigi_keser()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<Tree, TreeDto>().MaxDepth(2));
        var tree = new Tree { Name = "1", Child = new Tree { Name = "2", Child = new Tree { Name = "3" } } };
        var dto = config.CreateMapper().Map<TreeDto>(tree);
        Assert.Equal("2", dto.Child!.Name);
        Assert.Null(dto.Child.Child);
    }

    [Fact]
    public void Extension_metot_ve_ReplaceMemberName()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.IncludeSourceExtensionMethods(typeof(SrcExtensions));
            cfg.ReplaceMemberName("Ü", "U");
            cfg.CreateMap<Src, Dst>(MemberList.None);
        });
        var dto = config.CreateMapper().Map<Dst>(new Src { Id = 2, Name = "a", Ünvan = "Dr" });
        Assert.Equal("a#2", dto.Display);
        Assert.Equal("Dr", dto.Unvan);
    }

    [Fact]
    public void CreateProfile_ve_profil_bazli_dogrulama()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateProfile("Satır içi", p => p.CreateMap<Sub, Dst>(MemberList.None));
            cfg.AddProfile<InlineValidatedProfile>();
        });
        config.AssertConfigurationIsValid("Satır içi");
        config.AssertConfigurationIsValid<InlineValidatedProfile>();
        Assert.Equal("c", config.CreateMapper().Map<Dst>(new Sub { Code = "c" }).Code);
    }

    [Fact]
    public void DisableConstructorMapping_ve_AllowNullDestinationValues()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.DisableConstructorMapping();
            cfg.AllowNullDestinationValues = false;
            cfg.CreateMap<Src, OnlyDefaultCtor>();
            cfg.CreateMap<Tree, TreeDto>();
        });
        var mapper = config.CreateMapper();
        Assert.Equal(4, mapper.Map<OnlyDefaultCtor>(new Src { Id = 4 }).Id);    // parametreli kurucu kullanılmadı
        Assert.NotNull(mapper.Map<TreeDto>(new Tree()).Child);                  // null yerine boş nesne
    }

    [Fact]
    public void Ayni_tur_cifti_iki_kez_tanimlanirsa_hata()
    {
        var ex = Assert.Throws<VeloxConfigurationException>(() => new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Src, Dst>();
            cfg.CreateMap<Src, Dst>();
        }));
        Assert.Contains("birden fazla", ex.Message);
    }

    [Fact]
    public void ForMember_ic_ice_yol_ForPath_onerir()
    {
        var ex = Assert.Throws<ArgumentException>(() => new MapperConfiguration(cfg =>
            cfg.CreateMap<Dst, Src>().ForMember(d => d.Sub.Code, o => o.MapFrom(s => s.Code))));
        Assert.Contains("ForPath", ex.Message);

        var config = new MapperConfiguration(cfg => cfg.CreateMap<Dst, Src>(MemberList.None).ForPath(d => d.Sub.Code, o => o.MapFrom(s => s.Code)));
        Assert.Equal("q", config.CreateMapper().Map<Src>(new Dst { Code = "q" }).Sub.Code);
    }

    [Fact]
    public void Eslenemeyen_tur_icin_anlasilir_hata()
    {
        var mapper = new MapperConfiguration(_ => { }).CreateMapper();
        var ex = Assert.ThrowsAny<VeloxException>(() => mapper.Map<Guid>(new Src()));
        Assert.Contains("eşlenemiyor", ex.Message);
    }

    [Fact]
    public void Koleksiyonlar_null_kaynakta_bos_liste_ve_ozel_listeler()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<Src, Dst>(MemberList.None));
        var mapper = config.CreateMapper();
        Assert.Empty(mapper.Map<List<Dst>>(null));
        Assert.Empty(mapper.Map<Dst[]>(null));
        var readOnly = mapper.Map<IReadOnlyList<Dst>>(new[] { new Src { Id = 1 } });
        Assert.Equal(1, readOnly.Single().Id);
    }
}

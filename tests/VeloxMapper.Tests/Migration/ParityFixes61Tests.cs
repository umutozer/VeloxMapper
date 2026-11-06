using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using VeloxMapper;
using VeloxMapper.Exceptions;
using Xunit;

namespace VeloxMapper.Tests.Migration;

/// <summary>6.1.0 parite düzeltmeleri (AutoMapper stres testinden çıkan bulgular).</summary>
public class ParityFixes61Tests
{
    public enum Color { Red, Green, Blue }

    public class WithGetPrefix { public int GetName { get; set; } public string GetTitle() => "T"; }
    public class WithoutPrefix { public int Name { get; set; } public string Title { get; set; } = ""; }

    [Fact]
    public void Default_Get_prefix_applies_to_source_properties_and_methods()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<WithGetPrefix, WithoutPrefix>());
        config.AssertConfigurationIsValid();
        var dto = config.CreateMapper().Map<WithoutPrefix>(new WithGetPrefix { GetName = 7 });
        Assert.Equal(7, dto.Name);
        Assert.Equal("T", dto.Title);
    }

    public class EnumSrc { public string? ColorText { get; set; } public string? Plain { get; set; } }
    public class EnumDst { public Color? ColorText { get; set; } public int? Plain { get; set; } }

    [Fact]
    public void Null_string_maps_to_null_nullable_enum_and_nullable_int()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<EnumSrc, EnumDst>()).CreateMapper();
        var dst = mapper.Map<EnumDst>(new EnumSrc { ColorText = null, Plain = null });
        Assert.Null(dst.ColorText);
        Assert.Null(dst.Plain);
        var dst2 = mapper.Map<EnumDst>(new EnumSrc { ColorText = "Blue", Plain = "5" });
        Assert.Equal(Color.Blue, dst2.ColorText);
        Assert.Equal(5, dst2.Plain);
    }

    public class Foo { public int Bar { get; set; } public string Baz { get; set; } = ""; public Inner Nested { get; set; } = new(); }
    public class Inner { public int V { get; set; } }

    [Fact]
    public void ExpandoObject_maps_to_class_without_configuration()
    {
        var mapper = new MapperConfiguration(cfg => { }).CreateMapper();
        dynamic expando = new ExpandoObject();
        expando.Bar = 5;
        expando.Baz = "b";
        var foo = mapper.Map<Foo>(expando);
        Assert.Equal(5, foo.Bar);
        Assert.Equal("b", foo.Baz);
    }

    [Fact]
    public void Class_maps_to_ExpandoObject_and_string_object_dictionaries()
    {
        var mapper = new MapperConfiguration(cfg => { }).CreateMapper();
        var foo = new Foo { Bar = 3, Baz = "x", Nested = new Inner { V = 9 } };

        var dictionary = mapper.Map<Dictionary<string, object>>(foo);
        Assert.Equal(3, dictionary["Bar"]);
        Assert.Equal("x", dictionary["Baz"]);
        Assert.Same(foo.Nested, dictionary["Nested"]);

        var iDictionary = mapper.Map<IDictionary<string, object>>(foo);
        Assert.Equal(3, iDictionary["Bar"]);

        dynamic expando = mapper.Map<ExpandoObject>(foo);
        Assert.Equal(3, (int)expando.Bar);
        Assert.Equal("x", (string)expando.Baz);
    }

    public class Source { public string Name { get; set; } = ""; public InnerSource InnerSource { get; set; } = new(); }
    public class InnerSource { public string Description { get; set; } = ""; }
    public class Destination { public string Name { get; set; } = ""; public string Description { get; set; } = ""; }

    [Fact]
    public void IncludeMembers_is_reversed_by_ReverseMap()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Source, Destination>().IncludeMembers(s => s.InnerSource).ReverseMap();
            cfg.CreateMap<InnerSource, Destination>(MemberList.None).ReverseMap();
        });
        config.AssertConfigurationIsValid();
        var mapper = config.CreateMapper();

        var forward = mapper.Map<Destination>(new Source { Name = "n", InnerSource = new InnerSource { Description = "d" } });
        Assert.Equal("d", forward.Description);

        var back = mapper.Map<Source>(new Destination { Name = "n", Description = "d" });
        Assert.Equal("n", back.Name);
        Assert.NotNull(back.InnerSource);
        Assert.Equal("d", back.InnerSource.Description);
    }

    [Fact]
    public void IMapper_is_assignable_to_IMapperBase()
    {
        IMapper mapper = new MapperConfiguration(cfg => cfg.CreateMap<Inner, Inner>()).CreateMapper();
        IMapperBase baseMapper = mapper;
        Assert.Equal(4, baseMapper.Map<Inner>(new Inner { V = 4 }).V);
    }
}

public class GetPrefixPrecedenceTests
{
    public class Src { public int Name { get; set; } public int GetName { get; set; } }
    public class Dst { public int Name { get; set; } }

    [Fact]
    public void Exact_name_wins_over_Get_prefixed_member()
    {
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Src, Dst>()).CreateMapper();
        Assert.Equal(1, mapper.Map<Dst>(new Src { Name = 1, GetName = 2 }).Name);
    }
}

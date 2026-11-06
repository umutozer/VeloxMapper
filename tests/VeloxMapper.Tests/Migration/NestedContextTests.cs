using System;
using System.Collections.Generic;
using System.Linq;
using VeloxMapper;
using Xunit;

namespace VeloxMapper.Tests.Migration;

/// <summary>
/// AutoMapper'da bir resolver içinden <c>context.Mapper.Map(...)</c> çağrıldığında Items/State aynı bağlamda taşınır.
/// </summary>
public class NestedContextTests
{
    public class Parent { public string Name { get; set; } = ""; public Child Child { get; set; } = new(); public List<Child> Children { get; set; } = new(); }
    public class Child { public string Code { get; set; } = ""; }
    public class ParentDto { public string Name { get; set; } = ""; public ChildDto Child { get; set; } = new(); public List<ChildDto> Children { get; set; } = new(); }
    public class ChildDto { public string Code { get; set; } = ""; }

    public class ChildViaMapperResolver : IValueResolver<Parent, ParentDto, ChildDto>
    {
        public ChildDto Resolve(Parent source, ParentDto destination, ChildDto destMember, ResolutionContext context)
            => context.Mapper.Map<ChildDto>(source.Child);
    }

    public class ChildrenViaMapperResolver : IValueResolver<Parent, ParentDto, List<ChildDto>>
    {
        public List<ChildDto> Resolve(Parent source, ParentDto destination, List<ChildDto> destMember, ResolutionContext context)
            => context.Mapper.Map<List<ChildDto>>(source.Children);
    }

    public class CodeResolver : IValueResolver<Child, ChildDto, string>
    {
        public string Resolve(Child source, ChildDto destination, string destMember, ResolutionContext context)
            => context.Items.TryGetValue("prefix", out var p) ? p + source.Code : "NO-PREFIX:" + source.Code + ":" + (context.State ?? "null");
    }

    [Fact]
    public void Items_and_State_flow_into_nested_context_Mapper_Map_calls()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Parent, ParentDto>()
                .ForMember(d => d.Child, o => o.MapFrom<ChildViaMapperResolver>())
                .ForMember(d => d.Children, o => o.MapFrom<ChildrenViaMapperResolver>());
            cfg.CreateMap<Child, ChildDto>()
                .ForMember(d => d.Code, o => o.MapFrom<CodeResolver>());
        });
        var mapper = config.CreateMapper();

        var src = new Parent { Name = "p", Child = new Child { Code = "c1" }, Children = { new Child { Code = "c2" } } };
        var dto = mapper.Map<ParentDto>(src, opt => { opt.Items["prefix"] = "T-"; opt.State = "S"; });

        Assert.Equal("T-c1", dto.Child.Code);
        Assert.Equal("T-c2", dto.Children.Single().Code);
    }

    [Fact]
    public void Nested_call_with_its_own_options_uses_those_options()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Child, ChildDto>().ForMember(d => d.Code, o => o.MapFrom<CodeResolver>());
        });
        var mapper = config.CreateMapper();
        var outer = mapper.Map<ChildDto>(new Child { Code = "x" }, o => o.Items["prefix"] = "A-");
        Assert.Equal("A-x", outer.Code);
        var plain = mapper.Map<ChildDto>(new Child { Code = "x" });
        Assert.Equal("NO-PREFIX:x:null", plain.Code);
    }
}

public class RuntimeMapperTests
{
    public class A { public string X { get; set; } = ""; }
    public class B { public string X { get; set; } = ""; }

    public class Resolver : IValueResolver<A, B, string>
    {
        public string Resolve(A source, B destination, string destMember, ResolutionContext context)
        {
            IRuntimeMapper runtime = context.Mapper;          // AutoMapper ile aynı tür
            IMapper plain = context.Mapper;                    // IMapper'a atanabilir
            Assert.Same(runtime, plain);
            return source.X + "!";
        }
    }

    [Fact]
    public void ResolutionContext_Mapper_is_IRuntimeMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<A, B>().ForMember(d => d.X, o => o.MapFrom<Resolver>()));
        var mapper = config.CreateMapper();
        Assert.IsAssignableFrom<IRuntimeMapper>(mapper);
        Assert.Equal("a!", mapper.Map<B>(new A { X = "a" }).X);
    }
}

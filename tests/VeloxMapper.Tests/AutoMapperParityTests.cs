using System;
using Xunit;
using VeloxMapper.Configuration;
using VeloxMapper.Abstractions;
using VeloxMapper.Attributes;

namespace VeloxMapper.Tests
{
    /// <summary>
    /// AutoMapper API paritesi için eklenen yeni overload'ların davranış testleri.
    /// </summary>
    public class AutoMapperParityTests
    {
        public class Src
        {
            public string A { get; set; } = "x";
            public string B { get; set; } = "y";
            public bool Flag { get; set; } = true;
        }

        public class Dst
        {
            public string Combined { get; set; } = "";
        }

        public class NestedSrc { public Inner Inner { get; set; } = new(); }
        public class Inner { public string City { get; set; } = "Istanbul"; }
        public class NestedDst { public string CityName { get; set; } = ""; }

        private static IVeloxMapper Build(Action<VeloxMapperOptions> cfg)
        {
            var config = new MapperConfiguration(cfg);
            config.AssertConfigurationIsValid();
            return new Mapper(config);
        }

        [Fact]
        public void MapFrom_TwoArg_SrcDest_Works()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o => o.MapFrom((s, d) => s.A + s.B)));

            var result = mapper.Map<Src, Dst>(new Src { A = "Hello", B = "World" });
            Assert.Equal("HelloWorld", result.Combined);
        }

        [Fact]
        public void MapFrom_ThreeArg_UsesDestMember()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o => o.MapFrom((s, d, destMember) => (destMember ?? "") + s.A)));

            var result = mapper.Map<Src, Dst>(new Src { A = "Z" });
            Assert.Equal("Z", result.Combined); // destMember başlangıçta ""
        }

        [Fact]
        public void MapFrom_FourArg_UsesContextItems()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o => o.MapFrom((s, d, destMember, ctx) =>
                    s.A + (ctx.Items.TryGetValue("suffix", out var v) ? v : "") )));

            var result = mapper.Map<Src, Dst>(new Src { A = "P" }, ctx => ctx.Items["suffix"] = "!");
            Assert.Equal("P!", result.Combined);
        }

        [Fact]
        public void MapFrom_String_NestedPath_Works()
        {
            var mapper = Build(cfg => cfg.CreateMap<NestedSrc, NestedDst>()
                .ForMember(d => d.CityName, o => o.MapFrom("Inner.City")));

            var result = mapper.Map<NestedSrc, NestedDst>(new NestedSrc());
            Assert.Equal("Istanbul", result.CityName);
        }

        [Fact]
        public void Condition_SingleArg_Src()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o =>
                {
                    o.MapFrom(s => s.A);
                    o.Condition(s => s.Flag);
                }));

            Assert.Equal("x", mapper.Map<Src, Dst>(new Src { A = "x", Flag = true }).Combined);
            Assert.Equal("", mapper.Map<Src, Dst>(new Src { A = "x", Flag = false }).Combined);
        }

        [Fact]
        public void Condition_TwoArg_SrcDest()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o =>
                {
                    o.MapFrom(s => s.A);
                    o.Condition((s, d) => s.A.Length > 2);
                }));

            Assert.Equal("long", mapper.Map<Src, Dst>(new Src { A = "long" }).Combined);
            Assert.Equal("", mapper.Map<Src, Dst>(new Src { A = "ab" }).Combined);
        }

        // ─── Batch 2/3: CreateMap zinciri & config ──────────────────────────

        public class SrcX { public string Name { get; set; } = "n"; }
        public class DstX { public string Name { get; set; } = ""; public string Extra { get; set; } = ""; }
        public class SrcExtra { public string Name { get; set; } = "n"; public string Secret { get; set; } = "s"; }
        public class DstName { public string Name { get; set; } = ""; }
        public class DstPriv { public string Name { get; set; } = ""; public string Computed { get; private set; } = ""; }

        [Fact]
        public void ForMember_StringName_Works()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember("Combined", o => o.MapFrom(s => s.A)));
            Assert.Equal("hi", mapper.Map<Src, Dst>(new Src { A = "hi" }).Combined);
        }

        [Fact]
        public void ForSourceMember_Compiles_And_Maps()
        {
            var mapper = Build(cfg => cfg.CreateMap<SrcExtra, DstName>()
                .ForSourceMember(s => s.Secret, opt => opt.DoNotValidate()));
            Assert.Equal("n", mapper.Map<SrcExtra, DstName>(new SrcExtra()).Name);
        }

        [Fact]
        public void ValidateMemberList_None_SkipsValidation()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<SrcX, DstX>().ValidateMemberList(MemberList.None));
            config.AssertConfigurationIsValid(); // Extra eşlenmemiş olsa da hata vermemeli
            var m = new Mapper(config);
            Assert.Equal("n", m.Map<SrcX, DstX>(new SrcX()).Name);
        }

        [Fact]
        public void DisableCtorValidation_OnlySkipsConstructorValidation()
        {
            // AutoMapper ile aynı: DisableCtorValidation yalnızca kurucu doğrulamasını kapatır; eşlenmemiş üyeler yine raporlanır.
            var config = new MapperConfiguration(cfg => cfg.CreateMap<SrcX, DstX>().DisableCtorValidation());
            var ex = Assert.Throws<VeloxMapper.Exceptions.VeloxValidationException>(() => config.AssertConfigurationIsValid());
            Assert.Contains("Extra", ex.Message);
        }

        [Fact]
        public void CreateMap_MemberList_None_Overload()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<SrcX, DstX>(MemberList.None));
            config.AssertConfigurationIsValid();
            Assert.Equal("n", new Mapper(config).Map<SrcX, DstX>(new SrcX()).Name);
        }

        [Fact]
        public void IgnoreAllPropertiesWithAnInaccessibleSetter_Works()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<SrcX, DstPriv>()
                .IgnoreAllPropertiesWithAnInaccessibleSetter());
            config.AssertConfigurationIsValid(); // private setter'lı Computed yok sayılmalı
            Assert.Equal("n", new Mapper(config).Map<SrcX, DstPriv>(new SrcX()).Name);
        }

        [Fact]
        public void ConfigurationProvider_AssertAndCompile_Work()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o => o.MapFrom(s => s.A)));

            IVeloxMapper mapper = config.CreateMapper(); // AutoMapper stili

            // AutoMapper stili: mapper.ConfigurationProvider.AssertConfigurationIsValid()
            mapper.ConfigurationProvider.AssertConfigurationIsValid();
            mapper.ConfigurationProvider.CompileMappings();

            Assert.Equal("ok", mapper.Map<Src, Dst>(new Src { A = "ok" }).Combined);
        }

        [Fact]
        public void Map_ObjectSource_WithContextItems()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o => o.MapFrom((s, d, m, ctx) =>
                    s.A + (ctx.Items.TryGetValue("suffix", out var v) ? v : ""))));
            object src = new Src { A = "P" };
            var result = mapper.Map<Dst>(src, ctx => ctx.Items["suffix"] = "!");
            Assert.Equal("P!", result.Combined);
        }

        [Fact]
        public void CreateProjection_WorksLikeCreateMap()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateProjection<SrcX, DstName>());
            config.AssertConfigurationIsValid();
            Assert.Equal("n", new Mapper(config).Map<SrcX, DstName>(new SrcX()).Name);
        }

        [Fact]
        public void BeforeAfterMap_WithContext_Works()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o => o.MapFrom(s => s.A))
                .BeforeMap((s, d, ctx) => ctx.Items["before"] = true)
                .AfterMap((s, d, ctx) => d.Combined = d.Combined + (ctx.Items.ContainsKey("before") ? "-ok" : "")));
            Assert.Equal("x-ok", mapper.Map<Src, Dst>(new Src { A = "x" }).Combined);
        }

        [Fact]
        public void ExactMatchNamingConvention_Available()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.SourceMemberNamingConvention = ExactMatchNamingConvention.Instance;
                cfg.CreateMap<SrcX, DstName>();
            });
            config.AssertConfigurationIsValid();
            Assert.Equal("n", new Mapper(config).Map<SrcX, DstName>(new SrcX()).Name);
        }

        [Fact]
        public void MemberAcceptOptions_Compile()
        {
            // MapAtRuntime / ExplicitExpansion / AllowNull / DoNotAllowNull derlenip çalışmalı
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o => { o.MapFrom(s => s.A); o.MapAtRuntime(); o.AllowNull(); }));
            Assert.Equal("x", mapper.Map<Src, Dst>(new Src { A = "x" }).Combined);
        }

        [Fact]
        public void AllowNullDestinationValues_IsSettable()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AllowNullDestinationValues = true;
                cfg.CreateMap<SrcX, DstX>(MemberList.None);
            });
            Assert.NotNull(new Mapper(config));
        }

        // ─── Motor arity: ConstructUsing ctx, Condition 4/5, PreCondition ctx, ConvertUsing<T> ───

        public class DstCtor { public string Combined { get; } public DstCtor(string c) { Combined = c; } }

        public class ToDstConverter : IVeloxTypeConverter<Src, Dst>
        {
            public Dst Convert(Src? source) => new Dst { Combined = (source?.A ?? "") + "-conv" };
        }

        [Fact]
        public void ConstructUsing_WithContext()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, DstCtor>()
                .ConstructUsing((s, ctx) => new DstCtor(s.A + (ctx.Items.TryGetValue("x", out var v) ? v : ""))));
            var r = mapper.Map<Src, DstCtor>(new Src { A = "P" }, ctx => ctx.Items["x"] = "!");
            Assert.Equal("P!", r.Combined);
        }

        [Fact]
        public void ConvertUsing_GenericTypeConverter()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>().ConvertUsing<ToDstConverter>());
            Assert.Equal("x-conv", mapper.Map<Src, Dst>(new Src { A = "x" }).Combined);
        }

        [Fact]
        public void Condition_FourArg_SrcDestMembers()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o =>
                {
                    o.MapFrom(s => s.A);
                    o.Condition((s, d, srcMember, destMember) => srcMember.Length > 2);
                }));
            Assert.Equal("long", mapper.Map<Src, Dst>(new Src { A = "long" }).Combined);
            Assert.Equal("", mapper.Map<Src, Dst>(new Src { A = "ab" }).Combined);
        }

        [Fact]
        public void Condition_FiveArg_WithContext()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o =>
                {
                    o.MapFrom(s => s.A);
                    o.Condition((s, d, srcMember, destMember, ctx) => (bool)ctx.Items["allow"]);
                }));
            Assert.Equal("x", mapper.Map<Src, Dst>(new Src { A = "x" }, ctx => ctx.Items["allow"] = true).Combined);
            Assert.Equal("", mapper.Map<Src, Dst>(new Src { A = "x" }, ctx => ctx.Items["allow"] = false).Combined);
        }

        [Fact]
        public void PreCondition_WithContext()
        {
            var mapper = Build(cfg => cfg.CreateMap<Src, Dst>()
                .ForMember(d => d.Combined, o =>
                {
                    o.MapFrom(s => s.A);
                    o.PreCondition((s, ctx) => (bool)ctx.Items["pre"]);
                }));
            Assert.Equal("x", mapper.Map<Src, Dst>(new Src { A = "x" }, ctx => ctx.Items["pre"] = true).Combined);
            Assert.Equal("", mapper.Map<Src, Dst>(new Src { A = "x" }, ctx => ctx.Items["pre"] = false).Combined);
        }

        // ─── IncludeMembers & [AutoMap] ───────────────────────────────────────

        public class IncInner { public string City { get; set; } = "Istanbul"; }
        public class IncSource { public IncInner Inner { get; set; } = new(); public string Top { get; set; } = "t"; }
        public class IncDest { public string Top { get; set; } = ""; public string City { get; set; } = ""; }

        [Fact]
        public void IncludeMembers_FlattensChildMembers()
        {
            var mapper = Build(cfg => cfg.CreateMap<IncSource, IncDest>()
                .IncludeMembers(s => s.Inner));
            var r = mapper.Map<IncSource, IncDest>(new IncSource());
            Assert.Equal("t", r.Top);          // doğrudan
            Assert.Equal("Istanbul", r.City);  // Inner'dan IncludeMembers ile
        }

        public class AmSource { public string Name { get; set; } = "auto"; public int Age { get; set; } = 42; }

        [AutoMap(typeof(AmSource))]
        public class AmDest { public string Name { get; set; } = ""; public int Age { get; set; } }

        [Fact]
        public void AutoMap_Attribute_CreatesRuntimeMap()
        {
            var config = new MapperConfiguration(cfg =>
                cfg.AddProfilesFromAssembly(typeof(AutoMapperParityTests).Assembly));
            var mapper = new Mapper(config);
            var r = mapper.Map<AmSource, AmDest>(new AmSource());
            Assert.Equal("auto", r.Name);
            Assert.Equal(42, r.Age);
        }
    }
}

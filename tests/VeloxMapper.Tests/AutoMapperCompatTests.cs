using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using VeloxMapper.Configuration;
using VeloxMapper.Exceptions;

namespace VeloxMapper.Tests
{
    public class AutoMapperCompatTests
    {
        // 1. DoNotValidate Testi
        public class SourceWithUnmapped
        {
            public string Name { get; set; } = "Umut";
        }

        public class DestWithUnmapped
        {
            public string Name { get; set; } = "";
            public string Extra { get; set; } = "";
        }

        [Fact]
        public void DoNotValidate_ShouldPassValidation()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<SourceWithUnmapped, DestWithUnmapped>()
                    .ForMember(d => d.Extra, opt => opt.DoNotValidate());
            });

            // Normalde Extra property'si eşleşmediği için validation hata verirdi.
            // DoNotValidate kullandığımız için başarılı bir şekilde doğrulama geçmeli.
            config.AssertConfigurationIsValid();

            var mapper = new Mapper(config);
            var src = new SourceWithUnmapped { Name = "Umut" };
            var dest = mapper.Map<SourceWithUnmapped, DestWithUnmapped>(src);

            Assert.Equal("Umut", dest.Name);
            Assert.Equal("", dest.Extra);
        }

        // 2. ShouldMapProperty ve ShouldMapField Testi
        public class FieldSource
        {
            public string publicField = "field-val";
            public string Name { get; set; } = "prop-val";
        }

        public class FieldDest
        {
            public string publicField = "";
            public string Name { get; set; } = "";
        }

        [Fact]
        public void ShouldMapField_WhenEnabled_ShouldMapFields()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.ShouldMapField = f => f.IsPublic;
                cfg.CreateMap<FieldSource, FieldDest>();
            });

            config.AssertConfigurationIsValid();

            var mapper = new Mapper(config);
            var src = new FieldSource { publicField = "field-changed", Name = "prop-changed" };
            var dest = mapper.Map<FieldSource, FieldDest>(src);

            Assert.Equal("field-changed", dest.publicField);
            Assert.Equal("prop-changed", dest.Name);
        }

        // 3. CompileMappings Testi
        [Fact]
        public void CompileMappings_ShouldPrecompileAllDelegates()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<SourceWithUnmapped, DestWithUnmapped>()
                    .ForMember(d => d.Extra, opt => opt.Ignore());
            });

            config.AssertConfigurationIsValid();

            // Mappings startup'ta ön-derlenir
            config.CompileMappings();

            var mapper = new Mapper(config);
            var src = new SourceWithUnmapped { Name = "Ali" };
            var dest = mapper.Map<SourceWithUnmapped, DestWithUnmapped>(src);

            Assert.Equal("Ali", dest.Name);
        }

        // 4. Unflattening Testi
        public class Order
        {
            public Customer Customer { get; set; } = new Customer();
        }

        public class Customer
        {
            public string Name { get; set; } = "";
        }

        public class OrderDto
        {
            public string CustomerName { get; set; } = "";
        }

        [Fact]
        public void ReverseMap_ShouldAutomaticallyUnflattenProperties()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Order, OrderDto>().ReverseMap();
            });

            config.AssertConfigurationIsValid();

            var mapper = new Mapper(config);
            
            // DTO -> Entity (Unflattening)
            var dto = new OrderDto { CustomerName = "Velox" };
            var order = mapper.Map<OrderDto, Order>(dto);

            Assert.NotNull(order.Customer);
            Assert.NotNull(order.Customer);
            Assert.Equal("Velox", order.Customer.Name);
        }

        // 5. Dictionary Mapping Testi
        public class DictDest
        {
            public string Name { get; set; } = "";
            public int Age { get; set; }
            public string City = "";
        }

        [Fact]
        public void DictionaryMapping_ShouldMapToClass()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.ShouldMapField = f => f.IsPublic;
                cfg.CreateMap<Dictionary<string, object>, DictDest>();
            });

            config.AssertConfigurationIsValid();

            var mapper = new Mapper(config);
            var dict = new Dictionary<string, object>
            {
                { "name", "Umut" },
                { "age", 30 },
                { "city", "Istanbul" }
            };

            var dest = mapper.Map<Dictionary<string, object>, DictDest>(dict);

            Assert.Equal("Umut", dest.Name);
            Assert.Equal(30, dest.Age);
            Assert.Equal("Istanbul", dest.City);
        }

        // 6. ForAllOtherMembers Testi
        public class ForAllOtherDest
        {
            public string Keep1 { get; set; } = "";
            public string Ignore1 { get; set; } = "";
            public string Ignore2 { get; set; } = "";
        }

        [Fact]
        public void ForAllOtherMembers_ShouldApplyIgnore()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<SourceWithUnmapped, ForAllOtherDest>()
                    .ForMember(d => d.Keep1, opt => opt.MapFrom(s => s.Name))
                    .ForAllOtherMembers(opt => opt.Ignore());
            });

            config.AssertConfigurationIsValid();

            var mapper = new Mapper(config);
            var src = new SourceWithUnmapped { Name = "Data" };
            var dest = mapper.Map<SourceWithUnmapped, ForAllOtherDest>(src);

            Assert.Equal("Data", dest.Keep1);
            Assert.Equal("", dest.Ignore1);
            Assert.Equal("", dest.Ignore2);
        }

        // 7. RecognizeDestinationPostfixes ve ClearPrefixes Testi
        public class PrefixPostfixSource
        {
            public string Name { get; set; } = "Umut";
        }

        public class PrefixPostfixDest
        {
            public string NameDto { get; set; } = "";
        }

        [Fact]
        public void RecognizeDestinationPostfixes_ShouldMap()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.RecognizeDestinationPostfixes("Dto");
                cfg.CreateMap<PrefixPostfixSource, PrefixPostfixDest>();
            });

            config.AssertConfigurationIsValid();

            var mapper = new Mapper(config);
            var src = new PrefixPostfixSource { Name = "Umut" };
            var dest = mapper.Map<PrefixPostfixSource, PrefixPostfixDest>(src);

            Assert.Equal("Umut", dest.NameDto);
        }

        // 8. Naming Conventions Testi
        public class SnakeSource
        {
            public string first_name { get; set; } = "Umut";
        }

        public class PascalDest
        {
            public string FirstName { get; set; } = "";
        }

        [Fact]
        public void NamingConventions_ShouldMapSnakeToPascal()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.SourceMemberNamingConvention = LowerUnderscoreNamingConvention.Instance;
                cfg.DestinationMemberNamingConvention = PascalCaseNamingConvention.Instance;
                cfg.CreateMap<SnakeSource, PascalDest>();
            });

            config.AssertConfigurationIsValid();

            var mapper = new Mapper(config);
            var src = new SnakeSource { first_name = "Umut" };
            var dest = mapper.Map<SnakeSource, PascalDest>(src);

            Assert.Equal("Umut", dest.FirstName);
        }

        // 9. ConvertUsingEnumMapping Testi
        public enum SourceEnum { A, B }
        public enum DestEnum { X, Y }

        [Fact]
        public void EnumMapping_ShouldMapCustomValues()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<SourceEnum, DestEnum>()
                    .ConvertUsingEnumMapping(opt => opt
                        .MapValue(SourceEnum.A, DestEnum.Y)
                        .MapValue(SourceEnum.B, DestEnum.X));
            });

            config.AssertConfigurationIsValid();

            var mapper = new Mapper(config);
            Assert.Equal(DestEnum.Y, mapper.Map<SourceEnum, DestEnum>(SourceEnum.A));
            Assert.Equal(DestEnum.X, mapper.Map<SourceEnum, DestEnum>(SourceEnum.B));
        }
    }
}

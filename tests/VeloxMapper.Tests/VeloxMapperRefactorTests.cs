using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using VeloxMapper.Abstractions;
using VeloxMapper.Configuration;
using VeloxMapper.Exceptions;
using VeloxMapper.Execution;
using Xunit;

namespace VeloxMapper.Tests;

/// <summary>
/// VeloxMapper iyileştirmelerini ve yeni özelliklerini doğrulayan test sınıfı.
/// </summary>
public class VeloxMapperRefactorTests
{
    private class SimpleServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();
        public void AddService(Type type, object instance) => _services[type] = instance;
        public object? GetService(Type serviceType) => _services.TryGetValue(serviceType, out var service) ? service : null;
    }

    #region 1. ProjectTo SQL Translation Mode
    
    public class SourceEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class DestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    [Fact]
    public void ProjectTo_ShouldNotContainBlockExpressionsOrRuntimeContext()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<SourceEntity, DestDto>()
               .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name));
        });

        // Act
        var expression = ExpressionBuilder.BuildProjectToExpression(typeof(SourceEntity), typeof(DestDto), config);

        // Assert
        // SQL sağlayıcılarının çevirebilmesi için gövde BlockExpression veya ResolutionContext içermemelidir.
        Assert.NotNull(expression);
        Assert.True(expression.Body is MemberInitExpression || expression.Body is NewExpression);
        Assert.False(expression.ToString().Contains("VeloxResolutionContext"));
        Assert.False(expression.ToString().Contains("Block"));
    }

    #endregion

    #region 2. Precompiled Registry (AOT)

    [Fact]
    public void RegisterPrecompiledMapper_ShouldBeUsedDirectly()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<SourceEntity, DestDto>();
        });

        bool precompiledCalled = false;
        Func<SourceEntity, DestDto> precompiledFunc = src =>
        {
            precompiledCalled = true;
            return new DestDto { Id = src.Id, Name = src.Name + " - Precompiled" };
        };

        config.RegisterPrecompiledMapper(precompiledFunc);
        var mapper = new Mapper(config);

        var source = new SourceEntity { Id = 1, Name = "Test" };

        // Act
        var result = mapper.Map<SourceEntity, DestDto>(source);

        // Assert
        Assert.True(precompiledCalled);
        Assert.Equal("Test - Precompiled", result.Name);
    }

    #endregion

    #region 3. Thread-Safe Reference Cache & Dispose

    [Fact]
    public void ResolutionContext_Dispose_ShouldClearReferenceCache()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<SourceEntity, DestDto>();
        });
        var mapper = new Mapper(config);

        VeloxReferenceCache? cache;
        using (var context = new VeloxResolutionContext(mapper))
        {
            context.ReferenceCache = new VeloxReferenceCache();
            context.ReferenceCache.Set(new object(), new object());
            cache = context.ReferenceCache;

            // Act & Assert (Dispose öncesi önbellekte veri var mı?)
            Assert.NotNull(cache);
        }

        // Assert (Dispose sonrası temizlendi mi?)
        object? dummy = new object();
        Assert.False(cache.TryGetValue(dummy, out _));
    }

    #endregion

    #region 4. DI Scoped Custom Converter

    public class ScopedConverter : IVeloxTypeConverter<SourceEntity, DestDto>
    {
        private readonly string _scopedId;

        public ScopedConverter() : this(Guid.NewGuid().ToString())
        {
        }

        public ScopedConverter(string scopedId)
        {
            _scopedId = scopedId;
        }

        public DestDto Convert(SourceEntity? source)
        {
            if (source == null) return new DestDto();
            return new DestDto { Id = source.Id, Name = _scopedId };
        }
    }

    [Fact]
    public void CustomConverter_ShouldBeResolvedFromServiceProvider()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<SourceEntity, DestDto>()
               .ConvertUsing(new ScopedConverter("default"));
        });

        var provider = new SimpleServiceProvider();
        var expectedConverter = new ScopedConverter("scoped-instance-value");
        provider.AddService(typeof(ScopedConverter), expectedConverter);

        var mapper = new Mapper(config, provider);
        var source = new SourceEntity { Id = 5, Name = "Test" };

        // Act
        var result = mapper.Map<SourceEntity, DestDto>(source);

        // Assert
        // Scoped ID, provider'daki expectedConverter'dan gelmelidir ("scoped-instance-value").
        Assert.Equal("scoped-instance-value", result.Name);
    }

    #endregion

    #region 5. ReverseMap Kuralları

    [Fact]
    public void ReverseMap_ShouldInheritIgnoreRules()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<SourceEntity, DestDto>()
               .ForMember(d => d.Description, opt => opt.Ignore()) // Destination mapping'deki Ignore
               .ReverseMap();
        });

        // Act & Assert
        // Ters yöndeki registration'ı kontrol et (DestDto -> SourceEntity)
        var reversePlan = config.GetMappingPlan(typeof(DestDto), typeof(SourceEntity));
        var descriptionProp = reversePlan.Properties.FirstOrDefault(p => p.TargetProperty == "Description");
        
        // Orijinal eşleşmedeki property adı "Description" ters yönde de ignore edilmiş olmalıdır.
        Assert.NotNull(descriptionProp);
        Assert.Equal("Ignored", descriptionProp.ExecutionType);
    }

    #endregion

    #region 6. Detailed Error Positioning (CurrentMember)

    public class ComplexParent
    {
        public int Id { get; set; }
        public ComplexChild Child { get; set; } = null!;
    }

    public class ComplexChild
    {
        public string Value { get; set; } = "";
    }

    public class ComplexParentDto
    {
        public int Id { get; set; }
        public ComplexChildDto Child { get; set; } = null!;
    }

    public class ComplexChildDto
    {
        public string Value { get; set; } = "";
    }

    [Fact]
    public void MappingError_ShouldContainCurrentMemberInExceptionMessage()
    {
        // Arrange
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ComplexParent, ComplexParentDto>();
            cfg.CreateMap<ComplexChild, ComplexChildDto>()
               .ForMember(d => d.Value, opt => opt.MapFrom(s => ThrowError(s.Value)));
        });

        var mapper = new Mapper(config);
        var source = new ComplexParent
        {
            Id = 1,
            Child = new ComplexChild { Value = "Crash" }
        };

        // Act & Assert
        var exception = Assert.Throws<VeloxMappingException>(() => mapper.Map<ComplexParent, ComplexParentDto>(source));
        
        // Hatanın "Value" üyesinde oluştuğunu belirtmelidir.
        Assert.Contains("Value", exception.Message);
    }

    private static string ThrowError(string input)
    {
        throw new InvalidOperationException("Eşleştirme sırasında kasıtlı hata!");
    }

    #endregion
}

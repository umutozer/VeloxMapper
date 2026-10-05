using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using VeloxMapper.Abstractions;
using VeloxMapper.Configuration;

namespace VeloxMapper.Tests;

#region Test Modelleri ve Eylemleri

public class ActionSrc
{
    public string Message { get; set; } = "Merhaba";
    public int Value { get; set; } = 10;
}

public class ActionDst
{
    public string Log { get; set; } = "";
    public int Result { get; set; }
}

/// <summary>
/// Log takibi için kullanılan test servisi.
/// </summary>
public interface ILoggerService
{
    void LogMessage(string msg);
    IReadOnlyList<string> GetLogs();
}

public class FakeLoggerService : ILoggerService
{
    private readonly List<string> _logs = new();
    public void LogMessage(string msg) => _logs.Add(msg);
    public IReadOnlyList<string> GetLogs() => _logs;
}

/// <summary>
/// DI bağımlı BeforeMap action sınıfı.
/// </summary>
public class CustomBeforeMapAction : IVeloxMappingAction<ActionSrc, ActionDst>
{
    private readonly ILoggerService _logger;

    public CustomBeforeMapAction(ILoggerService logger)
    {
        _logger = logger;
    }

    public void Process(ActionSrc source, ActionDst destination, VeloxResolutionContext context)
    {
        _logger.LogMessage("BeforeMap çalıştı: " + source.Message);
        destination.Log += "[BeforeDI]";
    }
}

/// <summary>
/// DI bağımlı AfterMap action sınıfı.
/// </summary>
public class CustomAfterMapAction : IVeloxMappingAction<ActionSrc, ActionDst>
{
    private readonly ILoggerService _logger;

    public CustomAfterMapAction(ILoggerService logger)
    {
        _logger = logger;
    }

    public void Process(ActionSrc source, ActionDst destination, VeloxResolutionContext context)
    {
        _logger.LogMessage("AfterMap çalıştı: " + source.Message);
        destination.Log += "[AfterDI]";
    }
}

#endregion

public class VeloxMapperV5ActionTests
{
    [Fact]
    public void Inline_BeforeMap_Ve_AfterMap_Eylemleri_Basariyla_Calismali()
    {
        // 1. Yapılandırma
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ActionSrc, ActionDst>()
                .BeforeMap((src, dest) =>
                {
                    dest.Log += "[BeforeInline:" + src.Message + "]";
                })
                .AfterMap((src, dest) =>
                {
                    dest.Log += "[AfterInline:" + src.Value + "]";
                    dest.Result = src.Value * 2;
                });
        });

        var mapper = new Mapper(config);

        // 2. Mapping
        var source = new ActionSrc { Message = "Velox", Value = 5 };
        var dest = mapper.Map<ActionDst>(source);

        // 3. Doğrulamalar
        Assert.NotNull(dest);
        
        // BeforeMap -> MapProperties -> AfterMap sırasını doğrula
        Assert.Equal("[BeforeInline:Velox][AfterInline:5]", dest.Log);
        Assert.Equal(10, dest.Result);
    }

    [Fact]
    public void DI_Destekli_Mapping_Action_Eylemleri_Basariyla_Calismali()
    {
        // 1. DI yapılandırması
        var services = new ServiceCollection();
        
        var logger = new FakeLoggerService();
        services.AddSingleton<ILoggerService>(logger);
        
        services.AddVeloxMapper(cfg =>
        {
            cfg.CreateMap<ActionSrc, ActionDst>()
                .BeforeMap<CustomBeforeMapAction>()
                .AfterMap<CustomAfterMapAction>();
        });

        var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IVeloxMapper>();

        // 2. Mapping
        var source = new ActionSrc { Message = "Sistem", Value = 100 };
        var dest = mapper.Map<ActionSrc, ActionDst>(source);

        // 3. Doğrulamalar
        Assert.NotNull(dest);
        Assert.Equal("[BeforeDI][AfterDI]", dest.Log);

        // Logger çağrılarını doğrula
        var logs = logger.GetLogs();
        Assert.Equal(2, logs.Count);
        Assert.Equal("BeforeMap çalıştı: Sistem", logs[0]);
        Assert.Equal("AfterMap çalıştı: Sistem", logs[1]);
    }
}

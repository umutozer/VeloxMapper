using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using VeloxMapper.Configuration;
using VeloxMapper.Exceptions;
using VeloxMapper.Attributes;
using VeloxMapper.Diagnostics;
using VeloxMapper.Caching;
using VeloxMapper.Extensions;
using VeloxMapper.Abstractions;

namespace VeloxMapper.Tests;

#region Test Modelleri

public class TestSource
{
    public string Name { get; set; } = "TestName";
    public int Age { get; set; } = 30;
    public string? Email { get; set; } = "test@velox.dev";
}

public class TestDestination
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// Aynı parametre sayısına sahip iki constructor — belirsizlik yaratır.
/// </summary>
public class AmbiguousDest
{
    public string? Value { get; set; }
    public AmbiguousDest(string a) { Value = a; }
    public AmbiguousDest(int b) { Value = b.ToString(); }
}

/// <summary>
/// [VeloxConstructor] ile belirsizlik çözülmüş hedef tür.
/// </summary>
public class VeloxConstructorDest
{
    public string Name { get; }
    public int Age { get; }

    public VeloxConstructorDest(string name) { Name = name; Age = 0; }

    [VeloxConstructor]
    public VeloxConstructorDest(string name, int age) { Name = name; Age = age; }
}

/// <summary>
/// Tek parametresiz constructor — sorunsuz eşleşmeli.
/// </summary>
public class SimpleTarget
{
    public string Name { get; set; } = "";
}

/// <summary>
/// Record türü — immutable destek testi.
/// </summary>
public record RecordSource(string Title, int Count);
public record RecordDest(string Title, int Count);

/// <summary>
/// Özel tip dönüştürücü test implementasyonu.
/// </summary>
public class StringToIntConverter : IVeloxTypeConverter<string, int>
{
    public int Convert(string? source)
    {
        return source != null ? int.Parse(source) : 0;
    }
}

#endregion

public class MapTests
{
    [Fact]
    public void Map_BasitOzellikler_BasariylaEslestirilmeli()
    {
        // Arrange
        var config = new MapperConfiguration(_ => { });
        var mapper = new Mapper(config);
        var source = new TestSource();

        // Act
        var dest = mapper.Map<TestDestination>(source);

        // Assert
        Assert.NotNull(dest);
        Assert.Equal("TestName", dest.Name);
        Assert.Equal(30, dest.Age);
        Assert.Equal("test@velox.dev", dest.Email);
    }

    [Fact]
    public void Map_NullKaynak_NullDondurmeli_AutoMapperUyumlu()
    {
        var config = new MapperConfiguration(_ => { });
        var mapper = new Mapper(config);

        var result = mapper.Map<TestDestination>(null!);
        Assert.Null(result);
    }

    [Fact]
    public void Map_AyniKaynakIkiKez_AyniSonucuDondermeli_Deterministik()
    {
        var config = new MapperConfiguration(_ => { });
        var mapper = new Mapper(config);
        var source = new TestSource { Name = "Deterministic", Age = 42 };

        var dest1 = mapper.Map<TestDestination>(source);
        var dest2 = mapper.Map<TestDestination>(source);

        Assert.Equal(dest1.Name, dest2.Name);
        Assert.Equal(dest1.Age, dest2.Age);
    }

    [Fact]
    public void Map_RecordTipleri_BasariylaEslestirilmeli()
    {
        var config = new MapperConfiguration(_ => { });
        var mapper = new Mapper(config);
        var source = new RecordSource("VeloxTitle", 99);

        var dest = mapper.Map<RecordDest>(source);

        Assert.Equal("VeloxTitle", dest.Title);
        Assert.Equal(99, dest.Count);
    }
}

public class PatchTests
{
    [Fact]
    public void Patch_NullDegerlerHedefeYazilmali_VarsayilanDavranis()
    {
        // IgnoreNullValues = false (varsayılan) — null üzerine yazar
        var config = new MapperConfiguration(_ => { });
        var mapper = new Mapper(config);
        var source = new TestSource { Name = null!, Age = 55 };
        var dest = new TestDestination { Name = "OrijinalIsim", Age = 25 };

        mapper.Map(source, dest);

        Assert.Null(dest.Name); // null üzerine yazıldı
        Assert.Equal(55, dest.Age);
    }

    [Fact]
    public void Patch_IgnoreNullValues_NullDegerlerAtlanmali()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.PatchMapping.IgnoreNullValues = true;
        });
        var mapper = new Mapper(config);
        var source = new TestSource { Name = null!, Age = 55 };
        var dest = new TestDestination { Name = "OrijinalIsim", Age = 25 };

        mapper.Map(source, dest);

        Assert.Equal("OrijinalIsim", dest.Name); // null atlanmalı
        Assert.Equal(55, dest.Age); // value type güncellenmeli
    }
}

public class ConstructorTests
{
    [Fact]
    public void Map_BelirsizConstructor_VeloxAmbiguousConstructorException_Firlatmali()
    {
        var config = new MapperConfiguration(_ => { });
        var mapper = new Mapper(config);

        Assert.Throws<VeloxAmbiguousConstructorException>(
            () => mapper.Map<AmbiguousDest>(new TestSource()));
    }

    [Fact]
    public void Map_VeloxConstructorAttribute_DogruKurucuyuSecmeli()
    {
        var config = new MapperConfiguration(_ => { });
        var mapper = new Mapper(config);
        var source = new TestSource { Name = "CtorTest", Age = 77 };

        var dest = mapper.Map<VeloxConstructorDest>(source);

        Assert.NotNull(dest);
        Assert.Equal("CtorTest", dest.Name);
        Assert.Equal(77, dest.Age);
    }
}

public class ProjectionTests
{
    [Fact]
    public void ProjectTo_BasitProjeksiyon_BasariylaCalismali()
    {
        var config = new MapperConfiguration(_ => { });
        var mapper = new Mapper(config);
        var source = new[] { new TestSource { Name = "Projection", Age = 99 } }.AsQueryable();

        var projected = source.ProjectTo<TestDestination>(mapper);
        var list = projected.ToList();

        Assert.Single(list);
        Assert.Equal("Projection", list[0].Name);
        Assert.Equal(99, list[0].Age);
    }

    [Fact]
    public void ProjectTo_NullKaynak_ArgumentNullException_Firlatmali()
    {
        var config = new MapperConfiguration(_ => { });
        var mapper = new Mapper(config);

        Assert.Throws<ArgumentNullException>(
            () => ((IQueryable)null!).ProjectTo<TestDestination>(mapper));
    }
}

public class ThreadSafetyTests
{
    [Fact]
    public void Map_ParalelCagrilar_ThreadSafe_Olmali()
    {
        var config = new MapperConfiguration(_ => { });
        var mapper = new Mapper(config);

        // 100 paralel mapping isteği — hiçbir çakışma olmamalı
        Parallel.For(0, 100, i =>
        {
            var source = new TestSource { Name = $"Thread-{i}", Age = i };
            var dest = mapper.Map<TestDestination>(source);

            Assert.Equal($"Thread-{i}", dest.Name);
            Assert.Equal(i, dest.Age);
        });
    }
}

public class DiagnosticsTests
{
    [Fact]
    public void Hash_AyniPlan_HerZamanAyniHash_Deterministik()
    {
        var plan = new MappingPlanDef
        {
            SourceType = "A",
            DestinationType = "B",
            Mode = MappingMode.Map,
            Properties =
            [
                new MappedPropertyDesc { SourceProperty = "X", TargetProperty = "Y", ExecutionType = "Assigned" }
            ]
        };

        var hash1 = MappingPlanReport.GenerateHash(plan);
        var hash2 = MappingPlanReport.GenerateHash(plan);

        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
        Assert.Equal(64, hash1.Length); // SHA-256 = 64 hex karakter
    }

    [Fact]
    public void Hash_FarkliPlan_FarkliHash_Uretmeli()
    {
        var plan1 = new MappingPlanDef
        {
            SourceType = "A", DestinationType = "B", Mode = MappingMode.Map,
            Properties = [new MappedPropertyDesc { SourceProperty = "X", TargetProperty = "Y", ExecutionType = "Assigned" }]
        };

        var plan2 = new MappingPlanDef
        {
            SourceType = "A", DestinationType = "C", Mode = MappingMode.Map,
            Properties = [new MappedPropertyDesc { SourceProperty = "X", TargetProperty = "Z", ExecutionType = "Assigned" }]
        };

        Assert.NotEqual(MappingPlanReport.GenerateHash(plan1), MappingPlanReport.GenerateHash(plan2));
    }

    [Fact]
    public void Hash_PlanSchemaVersionDegisince_HashKirilmali()
    {
        // planSchemaVersion hash'e dahildir — schema değişince CI snapshot kırılmalı
        var plan1 = new MappingPlanDef
        {
            PlanSchemaVersion = "1.0",
            SourceType = "A", DestinationType = "B", Mode = MappingMode.Map,
            Properties = [new MappedPropertyDesc { SourceProperty = "X", TargetProperty = "Y", ExecutionType = "Assigned" }]
        };

        var plan2 = new MappingPlanDef
        {
            PlanSchemaVersion = "2.0", // Şema değişti
            SourceType = "A", DestinationType = "B", Mode = MappingMode.Map,
            Properties = [new MappedPropertyDesc { SourceProperty = "X", TargetProperty = "Y", ExecutionType = "Assigned" }]
        };

        Assert.NotEqual(MappingPlanReport.GenerateHash(plan1), MappingPlanReport.GenerateHash(plan2));
    }

    [Fact]
    public void Hash_VeloxVersionDegisince_HashKirilmamali()
    {
        // veloxVersion hash'e dahil DEĞİLDİR — minor/patch'te CI snapshot kırılmamalı
        var plan1 = new MappingPlanDef
        {
            VeloxMapperVersion = "4.0.0",
            SourceType = "A", DestinationType = "B", Mode = MappingMode.Map,
            Properties = [new MappedPropertyDesc { SourceProperty = "X", TargetProperty = "Y", ExecutionType = "Assigned" }]
        };

        var plan2 = new MappingPlanDef
        {
            VeloxMapperVersion = "4.1.0", // Kütüphane güncellendi ama şema aynı
            SourceType = "A", DestinationType = "B", Mode = MappingMode.Map,
            Properties = [new MappedPropertyDesc { SourceProperty = "X", TargetProperty = "Y", ExecutionType = "Assigned" }]
        };

        Assert.Equal(MappingPlanReport.GenerateHash(plan1), MappingPlanReport.GenerateHash(plan2));
    }

    [Fact]
    public void MappingPlanDef_VarsayilanDegerler_DogruOlmali()
    {
        var plan = new MappingPlanDef();

        Assert.Equal(VeloxVersion.Current, plan.VeloxMapperVersion);
        Assert.Equal(VeloxVersion.PlanSchemaVersion, plan.PlanSchemaVersion);
        Assert.Equal(VeloxVersion.Current, plan.VeloxMapperVersion);
        Assert.Equal("1.0", plan.PlanSchemaVersion);
    }

    [Fact]
    public void TextReport_VersiyonBilgisi_Icermeli()
    {
        var plan = new MappingPlanDef
        {
            SourceType = "SourceType", DestinationType = "DestType", Mode = MappingMode.Map,
            Properties = [new MappedPropertyDesc { SourceProperty = "A", TargetProperty = "B", ExecutionType = "Assigned" }]
        };

        var text = MappingPlanReport.GenerateTextReport(plan);

        // [META] satırı zorunlu — versiyon bilgileri
        Assert.Contains("[META]", text);
        Assert.Contains($"VeloxMapper v{VeloxVersion.Current}", text);
        Assert.Contains("PlanSchema v1.0", text);

        // Mevcut severity kontrolleri
        Assert.Contains("[INFO]", text);
        Assert.Contains("[DETAIL]", text);
        Assert.Contains("SourceType", text);
    }

    [Fact]
    public void JsonReport_VersiyonAlanlari_Icermeli()
    {
        var plan = new MappingPlanDef
        {
            SourceType = "Src", DestinationType = "Dst", Mode = MappingMode.ProjectTo,
            Properties = [new MappedPropertyDesc { SourceProperty = "P1", TargetProperty = "P2", ExecutionType = "Complex" }]
        };

        var json = MappingPlanReport.GenerateJsonReport(plan);

        // camelCase JSON çıktısında versiyon alanları zorunlu
        Assert.Contains("\"veloxMapperVersion\"", json);
        Assert.Contains("\"planSchemaVersion\"", json);
        Assert.Contains($"\"{VeloxVersion.Current}\"", json);
        Assert.Contains("\"1.0\"", json);

        // Mevcut alan kontrolleri
        Assert.Contains("\"sourceType\"", json);
        Assert.Contains("\"P1\"", json);
    }
}

public class ConfigurationTests
{
    [Fact]
    public void Configuration_NullConfigure_ArgumentNullException_Firlatmali()
    {
        Assert.Throws<ArgumentNullException>(() => new MapperConfiguration((Action<VeloxMapperOptions>)null!));
    }

    [Fact]
    public void Configuration_AyniConverterIkiKez_VeloxConfigurationException_Firlatmali()
    {
        Assert.Throws<VeloxConfigurationException>(() =>
        {
            _ = new MapperConfiguration(cfg =>
            {
                cfg.AddCustomConverter(new StringToIntConverter());
                cfg.AddCustomConverter(new StringToIntConverter()); // Fail-fast
            });
        });
    }
}

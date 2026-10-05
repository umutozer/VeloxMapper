---
title: "Teşhis & Mapping Planı"
description: "Eşleme planlarını raporlayın, CI'da snapshot testleriyle şema değişikliklerini yakalayın ve teşhis olaylarını loglayın."
section: validation-performance
order: 30
---

# Teşhis & Mapping Planı

Bir hedef üyenin değerinin nereden geldiğini görmek, bir model değişikliğinin hangi eşlemeleri etkilediğini fark etmek ve `ProjectTo`'nun atladığı üyeleri izlemek için VeloxMapper üç araç sunar: üye bazında mapping planı, plan raporları (metin, JSON, hash) ve teşhis olayları için bir log hedefi.

## Mapping planı

`MapperConfiguration.GetMappingPlan`, bir tür çifti için her hedef üyenin nasıl doldurulduğunu listeler:

```csharp
using VeloxMapper;
using VeloxMapper.Diagnostics;

var config = new MapperConfiguration(cfg =>
    cfg.CreateMap<Order, OrderDto>()
        .ForMember(d => d.Quantity, o => o.MapFrom(s => s.Quantity ?? 0))
        .ForMember(d => d.Tags, o => o.Ignore()));

MappingPlanDef plan = config.GetMappingPlan(typeof(Order), typeof(OrderDto));
Console.WriteLine(MappingPlanReport.GenerateTextReport(plan));
```

```text
[META] VeloxMapper v6.0.0 | PlanSchema v1.0
[INFO] Mapping Plan: MyApp.Order -> MyApp.OrderDto [Map]
  [DETAIL] Customer => Customer (Assigned)
  [DETAIL] CustomerName => CustomerName (Flattened)
  [DETAIL] Id => Id (Assigned)
  [DETAIL] Note => Note (Assigned)
  [DETAIL] s => (s.Quantity ?? 0) => Quantity (Complex)
  [DETAIL] Ignored => Tags (Ignored)
```

Satırlar hedef üye adına göre sıralıdır. Her satır `kaynak => hedef (yürütme türü)` biçimindedir.

| Yürütme türü | Anlamı |
| --- | --- |
| `Assigned` | Aynı adlı (veya isimlendirme kuralına göre eşleşen) kaynak üyeden. |
| `Flattened` | İç içe bir kaynak yolundan (`Customer.Name` → `CustomerName`). |
| `IncludedMember` | `IncludeMembers` ile eklenen bir alt nesneden. |
| `Complex` | `MapFrom`, resolver, converter veya `ForPath` kuralından. Kaynak sütununda ifade veya tür adı yer alır. |
| `Ignored` | `Ignore()` veya `AddGlobalIgnore` ile yok sayılmış. |
| `Unmapped` | Kaynağı bulunamadı. Doğrulama bu üye için hata verir. |

`GetMappingPlan` bir `CreateMap` kaydı gerektirmez; konvansiyonla örtük eşlenen tür çiftleri için de plan üretir. Üçüncü parametre (`MappingMode mode = MappingMode.Map`, `VeloxMapper.Caching` namespace'i) yalnızca rapordaki etikettir; planın içeriğini değiştirmez.

`GetMappingPlan` ve `MappingPlanReport` VeloxMapper'a özgüdür ve `IConfigurationProvider` arayüzünde değil `MapperConfiguration` sınıfında tanımlıdır.

## Rapor biçimleri

`VeloxMapper.Diagnostics.MappingPlanReport` statik sınıfı üç çıktı üretir:

| Metot | Çıktı | Kullanım |
| --- | --- | --- |
| `GenerateTextReport(plan)` | Okunabilir metin (yukarıdaki gibi) | Kod incelemesi, log, snapshot dosyası |
| `GenerateJsonReport(plan)` | Girintili, camelCase JSON | Araçlarla işleme, artefakt olarak saklama |
| `GenerateHash(plan)` | 64 karakterlik küçük harfli SHA-256 hex | CI'da tek satırlık karşılaştırma |

JSON çıktısı (kısaltılmış):

```json
{
  "veloxMapperVersion": "6.0.0",
  "planSchemaVersion": "1.0",
  "profileName": "",
  "sourceType": "MyApp.Order",
  "destinationType": "MyApp.OrderDto",
  "mode": 0,
  "properties": [
    { "sourceProperty": "Id", "targetProperty": "Id", "executionType": "Assigned" }
  ]
}
```

`mode` alanı sayısal değerdir (`0` = `Map`, `1` = `Patch`, `2` = `ProjectTo`). `profileName`, eşleme bir profilde tanımlıysa profil adını içerir.

### Hash neye bağlıdır

`GenerateHash`; plan şema sürümü, kaynak ve hedef tür adları, mod ve hedef üye adına göre sıralanmış `hedef:kaynak:yürütme türü` üçlülerinden hesaplanır. Kütüphane sürümü (`veloxMapperVersion`) hash'e dahil değildir; VeloxMapper'ı güncellemek, plan şeması değişmedikçe hash'i değiştirmez.

Hash şu durumlarda değişir:

- Hedef türe üye eklendiğinde, silindiğinde veya yeniden adlandırıldığında
- Bir üyenin kaynağı değiştiğinde (ör. konvansiyondan `MapFrom`'a geçildiğinde veya `Ignore` eklendiğinde)
- Bir `MapFrom` ifadesinin metni değiştiğinde (lambda parametresinin adı dahil)
- `VeloxVersion.PlanSchemaVersion` arttığında

## CI'da snapshot testleri

API sözleşmesi olan DTO'larda, bir eşlemenin farkında olmadan değişmesini yakalamak için plan raporunu sürüm kontrolünde saklayın ve testte karşılaştırın:

```csharp title="MappingPlanSnapshotTests.cs"
using VeloxMapper;
using VeloxMapper.Diagnostics;
using Xunit;

public class MappingPlanSnapshotTests
{
    private static readonly MapperConfiguration Config =
        new(cfg => cfg.AddMaps(typeof(OrderProfile).Assembly));

    [Theory]
    [InlineData(typeof(Order), typeof(OrderDto))]
    [InlineData(typeof(Customer), typeof(CustomerDto))]
    public void Mapping_plan_matches_snapshot(Type source, Type destination)
    {
        var plan = Config.GetMappingPlan(source, destination);
        var actual = MappingPlanReport.GenerateTextReport(plan);

        var path = Path.Combine("Snapshots", $"{source.Name}-{destination.Name}.plan.txt");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory("Snapshots");
            File.WriteAllText(path, actual);
            Assert.Fail($"Snapshot oluşturuldu: {path}. Dosyayı inceleyip depoya ekleyin.");
        }

        Assert.Equal(File.ReadAllText(path), actual);
    }
}
```

Snapshot dosyalarını test projesine kopyalanacak şekilde (`CopyToOutputDirectory`) ekleyin. Bir eşleme bilinçli olarak değiştiğinde dosyayı silip testi yeniden çalıştırarak güncelleyin; değişiklik kod incelemesinde fark (diff) olarak görünür.

Yalnızca değişip değişmediğini izlemek istiyorsanız metin yerine hash'i saklayabilirsiniz:

```csharp
const string expected = "514b0603d15081cac0cb8c964db128590f9104cfe2d21b8e9faffadb6af47ed5"; // önceki çalıştırmadan kopyalanan değer
Assert.Equal(expected, MappingPlanReport.GenerateHash(Config.GetMappingPlan(typeof(Order), typeof(OrderDto))));
```

Metin snapshot'ı neyin değiştiğini gösterir; hash yalnızca bir şeyin değiştiğini söyler. İnceleme kolaylığı için metin snapshot'ı tercih edin.

## Tüm eşlemeleri listelemek

`GetAllTypeMaps()` yapılandırmadaki açık eşlemeleri (`TypeMap`: `SourceType`, `DestinationType`, `ProfileName`) döndürür. Snapshot testini her eşleme için otomatik üretmek için kullanabilirsiniz:

```csharp
public static IEnumerable<object[]> AllMaps() =>
    Config.GetAllTypeMaps().Select(m => new object[] { m.SourceType, m.DestinationType });

[Theory]
[MemberData(nameof(AllMaps))]
public void Mapping_plan_matches_snapshot(Type source, Type destination) { /* ... */ }
```

`RegistrationCount` aynı listenin sayısını verir. Open generic tanımlar listeye dahil değildir.

## DiagnosticsSink

Çalışma zamanı motoru, teşhis olaylarını `IVeloxDiagnosticsSink` arayüzüne yazar:

```csharp
namespace VeloxMapper.Abstractions;

public interface IVeloxDiagnosticsSink
{
    void Log(string message, string severity = "Information", string? sourceFile = null, int? lineNumber = null);
}
```

| Olay | `severity` | Örnek mesaj |
| --- | --- | --- |
| Bir tür çifti için eşleme ifadesi üretildi | `Debug` | `Map ifadesi üretiliyor: Order -> OrderDto` |
| Mevcut nesneye eşleme ifadesi üretildi | `Debug` | `Patch ifadesi üretiliyor: Order -> OrderDto` |
| Projeksiyon ifadesi üretildi | `Debug` | `ProjectTo ifadesi üretiliyor: Customer -> CustomerDto` |
| `ProjectTo` bir üyeyi atladı | `Warning` | `ProjectTo: 'Segment' üyesi özel resolver/converter kullandığı için projeksiyona dahil edilmedi.` |

Olaylar ifade üretimi sırasında, yani her tür çifti ve çağrı biçimi için bir kez yazılır; her `Map` çağrısında yazılmaz.

Kendi hedefinizi yazıp yapılandırmaya verin:

```csharp title="WarningCollectorSink.cs"
using System.Collections.Concurrent;
using VeloxMapper.Abstractions;

public sealed class WarningCollectorSink : IVeloxDiagnosticsSink
{
    public ConcurrentQueue<string> Warnings { get; } = new();

    public void Log(string message, string severity = "Information", string? sourceFile = null, int? lineNumber = null)
    {
        if (severity == "Warning") Warnings.Enqueue(message);
    }
}
```

```csharp
var sink = new WarningCollectorSink();
var config = new MapperConfiguration(cfg =>
{
    cfg.DiagnosticsSink = sink;
    cfg.AddMaps(typeof(CustomerProfile).Assembly);
});
```

Bir testte tüm projeksiyonları üretip `sink.Warnings`'in boş olduğunu doğrulamak, `ProjectTo`'nun sessizce atladığı üyeleri CI'da yakalamanın bir yoludur.

## ILogger entegrasyonu

`DiagnosticsSink` verilmemişse ve bir `ILoggerFactory` mevcutsa olaylar `VeloxMapper` kategorisine loglanır. `severity` değerleri `LogLevel`'a eşlenir: `Debug` → `Debug`, `Warning` → `Warning`, `Error` → `Error`, diğerleri → `Information`.

| Kurulum | Logger nasıl bağlanır |
| --- | --- |
| `services.AddVeloxMapper(...)` | DI'da kayıtlı `ILoggerFactory` otomatik kullanılır. ASP.NET Core ve Generic Host bunu varsayılan olarak kaydeder. |
| `new MapperConfiguration(cfg => ..., loggerFactory)` | Kurucuya verilen fabrika kullanılır (AutoMapper 15 ile aynı imza). |
| `cfg.DiagnosticsSink = ...` | Verilen hedef kullanılır; `ILoggerFactory` yok sayılır. |

ASP.NET Core'da varsayılan log seviyesi `Information` olduğu için `Debug` olayları görünmez. `ProjectTo` uyarıları ise varsayılan ayarlarla loglanır. İfade üretimini izlemek için kategoriyi açın:

```json title="appsettings.Development.json"
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "VeloxMapper": "Debug"
    }
  }
}
```

## Üretilen ifadeyi incelemek

Bir eşlemenin tam olarak hangi kodu çalıştırdığını görmek için `config.BuildExecutionPlan(typeof(Order), typeof(OrderDto))` ile ifade ağacını alın ve hata ayıklayıcıda **DebugView** görünümünü inceleyin. Ayrıntılar için [Performans & Source Generator](./performance.md#üretilen-ifadeyi-incelemek-buildexecutionplan) sayfasına bakın. `ProjectTo` ifadesi için `query.Expression.ToString()` veya EF Core'un `ToQueryString()` metodunu kullanın; bkz. [Üretilen SQL'i incelemek](./projection.md#üretilen-sqli-incelemek).

## AutoMapper uyumluluğu

`BuildExecutionPlan` ve `MapperConfiguration(Action<...>, ILoggerFactory)` kurucusu AutoMapper ile aynıdır. `GetMappingPlan`, `MappingPlanReport` ve `IVeloxDiagnosticsSink` VeloxMapper'a özgüdür. `GetAllTypeMaps()` AutoMapper'da `config.Internal().GetAllTypeMaps()` ile erişilir; VeloxMapper'da doğrudan `MapperConfiguration` üzerindedir.

---
title: "Performans & Source Generator"
description: "VeloxMapper'ın eşlemeleri nasıl derleyip önbelleklediğini, başlangıçta ısıtmayı, Source Generator katmanını ve ölçüm yöntemini öğrenin."
section: validation-performance
order: 20
---

# Performans & Source Generator

VeloxMapper iki katmanlı bir motorla çalışır: çalışma zamanında derlenip önbelleklenen ifade ağaçları ve isteğe bağlı olarak derleme zamanında üretilen eşleme metotları. Bu sayfa her iki katmanın nasıl çalıştığını, maliyetin ne zaman ödendiğini ve uygulamanızda nasıl ölçeceğinizi anlatır.

## Motor nasıl çalışır

Bir tür çifti ilk kez eşlendiğinde VeloxMapper şu adımları izler:

1. Yapılandırmadaki kuralları (konvansiyonlar, `ForMember`, kurucu seçimi, koşullar, transformer'lar) birleştirerek bir `System.Linq.Expressions` ifade ağacı üretir.
2. İfadeyi `LambdaExpression.Compile()` ile bir delegeye derler.
3. Delegeyi `MapperConfiguration` üzerindeki eşzamanlı (thread-safe) bir önbelleğe koyar.

Sonraki çağrılar doğrudan önbellekteki delegeyi çalıştırır; reflection veya kural yorumlama yapılmaz. Üretilen kod, elle yazacağınız atama koduna benzer: `dest.Name = source.Name` gibi doğrudan üye erişimleri.

| Önbellek anahtarı | Açıklama |
| --- | --- |
| Kaynak tür + hedef tür + çağrı biçimi | Yeni nesne oluşturan (`Map<TDestination>`) ve mevcut nesneye eşleyen (`Map(source, destination)`) çağrılar için ayrı delegeler; generic ve `Type` parametreli çağrılar için ayrı sarmalayıcılar. |
| Kaynak tür + hedef tür + genişletilen üyeler | `ProjectTo` ifadeleri. Bkz. [ProjectTo & EF Core](./projection.md#performans). |

İç içe karmaşık üyeler, kendi tür çiftlerinin önbellekteki delegesi çağrılarak eşlenir. Böylece aynı iç tür (ör. `Address → AddressDto`) farklı eşlemelerde tekrar derlenmez.

Her `Map` çağrısının bağlam nesnesi (`ResolutionContext`) iş parçacığı başına küçük bir havuzdan alınır; `Items` sözlüğü yalnızca kullanıldığında oluşturulur.

## Yaşam döngüsü: MapperConfiguration ve IMapper

| Nesne | Maliyet | Önerilen ömür |
| --- | --- | --- |
| `MapperConfiguration` | Oluşturulurken tüm profiller işlenir; derlenmiş delegeler bu nesnede tutulur. | Uygulama başına bir tane (singleton). |
| `IMapper` (`Mapper`) | Yalnızca yapılandırmaya ve `IServiceProvider`'a bir referans tutar. | Transient; istediğiniz kadar oluşturabilirsiniz. |

`AddVeloxMapper` bu ömürleri sizin yerinize ayarlar: yapılandırma singleton, `IMapper` transient. DI kullanmıyorsanız `MapperConfiguration`'ı statik bir alanda veya kendi singleton'ınızda tutun.

> [!WARNING]
> Her istekte veya her eşlemede `new MapperConfiguration(...)` oluşturmayın. Her yeni yapılandırmanın önbelleği boştur; tüm eşlemeler yeniden derlenir.

## Başlangıçta ısıtma: CompileMappings

İlk çağrıdaki derleme maliyetini istek yolundan çıkarmak için uygulama başlarken `CompileMappings()` çağırın:

```csharp title="Program.cs"
var app = builder.Build();

app.Services.GetRequiredService<IConfigurationProvider>().CompileMappings();

app.Run();
```

`CompileMappings()`, yapılandırmadaki her açık `CreateMap` kaydı için yeni nesne oluşturan `Map` delegesini derler. Şunları önceden derlemez:

- Mevcut nesneye eşleme (`Map(source, destination)`) delegeleri
- `ProjectTo` ifadeleri
- Open generic tanımlar ve `CreateMap` ile tanımlanmamış (örtük) tür çiftleri

Bunlar ilk kullanımda derlenir. Isıtma, derleme süresini başlangıca taşır; toplam işi azaltmaz. Soğuk başlangıç süresinin kritik olduğu ortamlarda (sunucusuz fonksiyonlar) ısıtmanın başlangıca etkisini ölçün.

## Üretilen ifadeyi incelemek: BuildExecutionPlan

`BuildExecutionPlan(sourceType, destinationType)`, bir tür çifti için üretilen ifade ağacını derlemeden döndürür:

```csharp
LambdaExpression plan = config.BuildExecutionPlan(typeof(Order), typeof(OrderDto));
```

`ToString()` çıktısı blok gövdeli ifadelerde ayrıntı göstermez (`(source, context) => {var dest; ... }`). İfadenin tamamını görmek için Visual Studio veya Rider hata ayıklayıcısında `plan` değişkeninin **DebugView** görünümünü kullanın. Üye bazında özet bir rapor için [GetMappingPlan](./diagnostics.md) daha uygundur.

## Sıcak yol önerileri

- **Generic overload'ları tercih edin.** `Map<TSource, TDestination>(source)` doğrudan önbellekteki tipli delegeyi çağırır. `Map<TDestination>(object)` ve `Map(object, Type, Type)` önce kaynağın çalışma zamanı türünü bulur, ardından tipli delegeyi saran ek bir delege çağırır.
- **`PreserveReferences`'ı yalnızca gerektiğinde açın.** Her eşlenen nesne için bir sözlük araması ekler. Döngüsel tür grafiklerinde otomatik açılır; bkz. [Döngüsel Referanslar](./circular-references.md).
- **Okuma sorgularında `ProjectTo` kullanın.** Varlıkları belleğe alıp `Map` etmek yerine yalnızca gereken kolonları sorgulamak çoğu senaryoda eşleme maliyetinden çok daha belirleyicidir.
- **Resolver'ları hafif tutun.** Tür olarak verilen resolver, converter ve mapping action'lar her eşlemede DI'dan çözülür. Durumsuz ve sık kullanılan bir resolver için örnek verebilirsiniz: `o.MapFrom(new MyResolver())`.

## Source Generator

VeloxMapper paketi bir Roslyn source generator içerir. `[VeloxMap]` attribute'u ile işaretlediğiniz tür çiftleri için derleme zamanında düz C# eşleme metotları üretir. Üretilen kod reflection veya ifade derlemesi kullanmaz.

### Kullanım

```csharp title="MappingDeclarations.cs"
using VeloxMapper.Attributes;

[assembly: VeloxMap(typeof(MyApp.Catalog.Product), typeof(MyApp.Catalog.ProductDto))]
```

Attribute bir sınıf üzerinde de kullanılabilir ve birden çok kez uygulanabilir:

```csharp
[VeloxMap(typeof(Product), typeof(ProductDto))]
[VeloxMap(typeof(Product), typeof(ProductListItemDto))]
public static partial class CatalogMappings { }
```

Generator, `VeloxMapper.Generated.GeneratedMappers` sınıfında her çift için bir extension metot üretir. Metot adı `MapTo` ve hedef türün noktasız tam adından oluşur:

```csharp
// Üretilen kod (özet)
namespace VeloxMapper.Generated
{
    public static partial class GeneratedMappers
    {
        public static MyApp.Catalog.ProductDto MapToMyAppCatalogProductDto(this MyApp.Catalog.Product source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new MyApp.Catalog.ProductDto
            {
                Id = source.Id,
                Name = source.Name,
                Status = (global::MyApp.Catalog.ProductStatusDto)(source.Status)
            };
        }
    }
}
```

```csharp
using VeloxMapper.Generated;

ProductDto dto = product.MapToMyAppCatalogProductDto();
```

### Generator'ın eşledikleri

Generator yalnızca basit, birebir eşlemeler üretir. Şu kurallarla çalışır:

| Kural | Davranış |
| --- | --- |
| Üye eşleşmesi | Kaynak ve hedefteki public, okunabilir property'ler adla (büyük/küçük harf duyarsız) eşleşir. Taban sınıfların property'leri dahildir. |
| Aynı tür | Doğrudan atama |
| `T?` → `T` (`Nullable<T>`) | `source.X ?? default` |
| Enum → enum | Sayısal değere göre tür dönüşümü (cast) |
| Diğer tür farkları | Üye atlanır |
| İç içe nesneler, koleksiyonlar, flattening | Desteklenmez; üye atlanır (aynı türdeyse referans kopyalanır) |
| `Profile` kuralları (`ForMember`, `Ignore`, koşullar...) | Kullanılmaz |
| Hedef oluşturma | Parametresiz kurucu varsa nesne başlatıcı (`new T { ... }`); yoksa parametreleri kaynak property adlarından çözülebilen en geniş public kurucu (`new T(source.A, source.B) { ... }`) — pozisyonel record'lar desteklenir. Yalnızca public `set`/`init` erişimcisi olan ve kurucuya verilmeyen property'ler atanır. Kod üretilemeyen çiftler (soyut hedef, çözülemeyen kurucu) `VM003` uyarısıyla bildirilir ve çalışma zamanı motoruyla eşlenmeye devam eder. |
| `null` kaynak | `ArgumentNullException` |

Generator, `CreateMap` yapılandırmasından bağımsızdır: `[VeloxMap]` için `CreateMap` gerekmez ve üretilen metot profil kurallarını uygulamaz.

### IMapper ile birlikte kullanmak

Üretilen metodu `IMapper` çağrılarına bağlamak için `RegisterPrecompiledMapper` kullanın. Kayıttan sonra bu tür çifti için yeni nesne oluşturan `Map` çağrıları ifade ağacı yerine verdiğiniz fonksiyonu çalıştırır:

```csharp title="Program.cs"
using VeloxMapper.Generated;

var config = app.Services.GetRequiredService<MapperConfiguration>();
config.RegisterPrecompiledMapper<Product, ProductDto>(p => p.MapToMyAppCatalogProductDto());
```

- Kaydı uygulama başlangıcında, ilgili tür çifti ilk kez eşlenmeden önce yapın.
- Kayıt; kök `Map<Product, ProductDto>(p)` ve `Map<ProductDto>(p)` çağrılarını ve başka bir eşlemenin iç içe üyesi olarak eşlenen `Product` nesnelerini kapsar. `Map(source, destination)` ve `ProjectTo` çağrıları kayıtlı fonksiyonu kullanmaz. Kaydı, ilgili eşlemeler ilk kez kullanılmadan (ör. uygulama başlangıcında, `CompileMappings()` öncesinde) yapın.
- Kayıtlı fonksiyon, o tür çifti için tanımlı `ForMember`, `BeforeMap`/`AfterMap` ve diğer kuralların yerine geçer. Yalnızca konvansiyonla eşlenen çiftleri bu şekilde kaydedin.
- `RegisterPrecompiledMapper`, `IConfigurationProvider` arayüzünde değil `MapperConfiguration` sınıfında tanımlıdır. DI'dan `MapperConfiguration` olarak çözün.

Kayıt, generator ile üretilmemiş herhangi bir `Func<TSource, TDestination>` da kabul eder; elle yazılmış ve profilinizle aynı sonucu veren bir eşleme fonksiyonunu da bu yolla bağlayabilirsiniz.

### Analyzer kuralları

Paket, `[VeloxMap]` hedef türlerindeki kurucu belirsizliklerini derleme zamanında bildiren bir analyzer içerir:

| Kural | Önem | Açıklama |
| --- | --- | --- |
| `VM001` | Hata | Türde birden fazla `[VeloxConstructor]` attribute'u var. Yalnızca bir kurucuya uygulanmalıdır. |
| `VM002` | Uyarı | Türde en çok parametreye sahip birden fazla public kurucu var ve hiçbiri `[VeloxConstructor]` ile işaretlenmemiş. Çalışma zamanı motorunun hangi kurucuyu seçeceğini netleştirmek için birine `[VeloxConstructor]` ekleyin. |
| `VM003` | Uyarı | Bir `[VeloxMap]` çifti için derleme zamanı kodu üretilemedi (soyut hedef veya parametreleri kaynaktan çözülemeyen kurucu). Çift çalışma zamanı motoruyla eşlenmeye devam eder. |

## NativeAOT ve trimming

| Katman | NativeAOT / trimming |
| --- | --- |
| Source generator ile üretilen metotlar | Düz C# kodu; reflection ve dinamik kod üretimi kullanmaz. |
| Çalışma zamanı motoru (`Map`, `ProjectTo`) | Reflection ve `System.Linq.Expressions` kullanır. NativeAOT altında `LambdaExpression.Compile()` yorumlayıcı (interpreter) modunda çalışır; trimming, yalnızca reflection ile erişilen üyeleri kaldırabilir. |

VeloxMapper 6.0.0 paketi trimming veya AOT uyumlu olarak işaretlenmemiştir. NativeAOT ile yayımlayacağınız bir uygulamada:

1. Sıcak yollardaki eşlemeleri `[VeloxMap]` ile üretip üretilen metotları doğrudan çağırın.
2. Çalışma zamanı motorunu kullanmaya devam ettiğiniz eşlemeleri `PublishAot=true` ile yayımlanmış derlemede test edin ve derleyicinin trim/AOT uyarılarını inceleyin.

## Ölçüm

VeloxMapper kendi senaryonuz için performans iddiasında bulunmaz; modelleriniz, kurallarınız ve çalışma ortamınız sonucu belirler. Bir karar vermeden önce [BenchmarkDotNet](https://benchmarkdotnet.org) ile ölçün:

```csharp title="MappingBenchmarks.cs"
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using VeloxMapper;
using VeloxMapper.Generated;

[MemoryDiagnoser]
public class MappingBenchmarks
{
    private IMapper _mapper = null!;
    private Product _product = null!;

    [GlobalSetup]
    public void Setup()
    {
        var config = new MapperConfiguration(cfg => cfg.CreateMap<Product, ProductDto>());
        config.CompileMappings(); // derleme süresini ölçümden çıkar
        _mapper = config.CreateMapper();
        _product = new Product { Id = 1, Name = "Klavye", Price = 499.90m };
    }

    [Benchmark(Baseline = true)]
    public ProductDto Manual() => new() { Id = _product.Id, Name = _product.Name, Price = _product.Price };

    [Benchmark]
    public ProductDto Generated() => _product.MapToMyAppCatalogProductDto();

    [Benchmark]
    public ProductDto VeloxMapper_Map() => _mapper.Map<Product, ProductDto>(_product);
}

public static class Program
{
    public static void Main(string[] args) => BenchmarkRunner.Run<MappingBenchmarks>();
}
```

```bash
dotnet run -c Release
```

Ölçerken:

- Release yapılandırmasında ve hata ayıklayıcı bağlı olmadan çalıştırın.
- İlk çağrı maliyetini (derleme) ve kararlı durum maliyetini ayrı ayrı değerlendirin. Soğuk başlangıcı ölçmek için `[GlobalSetup]` içinde `CompileMappings()` çağırmayın ve BenchmarkDotNet'in `RunStrategy.ColdStart` seçeneğini kullanın.
- Gerçek modellerinizle ölçün: üye sayısı, iç içe nesneler, koleksiyon boyutları ve resolver'lar sonucu değiştirir.
- AutoMapper ile karşılaştırma yapacaksanız iki kütüphaneyi ayrı benchmark projelerinde, aynı modeller ve eşdeğer yapılandırmayla ölçün.

## AutoMapper uyumluluğu

`CompileMappings()` ve `BuildExecutionPlan()` AutoMapper ile aynı imzaya sahiptir. Source generator (`[VeloxMap]`), `RegisterPrecompiledMapper` ve analyzer kuralları VeloxMapper'a özgüdür.

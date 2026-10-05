---
title: "Sürüm Notları"
description: "VeloxMapper sürümlerindeki yenilikleri, kırıcı değişiklikleri ve 5.x'ten 6.0'a yükseltme adımlarını inceleyin."
section: resources
order: 30
---

# Sürüm Notları

Bu sayfa VeloxMapper sürümleri arasındaki değişiklikleri listeler. Sürüm numaraları [Semantic Versioning](https://semver.org) kurallarını izler: ana sürüm değişikliği kırıcı değişiklik içerir.

## 6.0.0 — 2026-10-05

Bu sürümün ana hedefi AutoMapper ile kaynak düzeyinde uyumluluktur: AutoMapper kullanan bir proje, paket ve `using` satırları değiştirilerek VeloxMapper ile derlenir. AutoMapper 13.0.1 ile yazılmış bir örnek uygulama, [geçiş betiği](./migration-script.md) çalıştırıldıktan sonra başka değişiklik yapılmadan derlendi ve aynı çıktıları üretti.

### Eklenenler

- **AutoMapper tür adları:** `IMapper`, `Mapper`, `Profile`, `ResolutionContext`, `IMappingOperationOptions`, `IValueResolver`, `IMemberValueResolver`, `IValueConverter`, `ITypeConverter`, `IMappingAction`, `IConfigurationProvider`, `MapperConfigurationExpression`, `TypeMap`, `PropertyMap` AutoMapper'daki adları ve imzalarıyla kullanılabilir. Tam liste: [API Eşleme Tablosu](./api-mapping.md).
- **`VeloxMapper.QueryableExtensions` namespace'i:** `ProjectTo` genişletme metotları AutoMapper ile aynı imzalarla bu namespace'tedir. `query.ProjectTo<T>(IMapper)` kısayolu eklendi.
- **`VeloxMapper.Configuration.Annotations` namespace'i:** `[Ignore]`, `[SourceMember]`, `[NullSubstitute]`, `[ValueResolver]`, `[ValueConverter]`, `[UseExistingValue]`, `[MappingOrder]`, `[MapAtRuntime]` öznitelikleri.
- **`[AutoMap]` özniteliği:** Hedef tür üzerinde `[AutoMap(typeof(Kaynak))]` ile eşleme tanımlama; `AddMaps` ve `AddVeloxMapper` taramasında kaydedilir.
- **`AddAutoMapper` takma adı:** `AddVeloxMapper` overload'larının tamamı `AddAutoMapper` adıyla da derlenir. `AddVeloxMapper((sp, cfg) => ...)` overload'u ve birden fazla çağrının tek yapılandırmada birleşmesi eklendi.
- **Yerleşik enum eşleme eklentisi:** `ConvertUsingEnumMapping(o => o.MapByName().MapValue(...))`; `AutoMapper.Extensions.EnumMapping` paketine gerek yoktur.
- **AutoMapper 15 imzaları:** `new MapperConfiguration(cfg => ..., ILoggerFactory)` ve `cfg.LicenseKey` (kabul edilir, yok sayılır).
- **Loglama:** `ILoggerFactory` verildiğinde (DI'da otomatik) teşhis olayları `VeloxMapper` kategorisine yazılır.
- **`MapFrom` ile farklı tür:** `MapFrom(s => s.Customer)` ifadesinin türü hedef üyeden farklı olabilir (`Customer` → `CustomerDto`, `List<OrderLine>` → `List<OrderLineDto>`); dönüşüm otomatik eşlenir.
- **Profil düzeyinde konvansiyonlar:** İsimlendirme kuralları, ön ekler, null ayarları ve `ShouldMap*` ayarları profil bazında verilebilir; profilde verilmeyen ayarlar global değerden gelir.
- **Geçiş betiği:** `tools/migrate-from-automapper.ps1`. Bkz. [Otomatik Geçiş Betiği](./migration-script.md).

### Değişenler (kırıcı)

| Değişiklik | 5.x | 6.0 |
| --- | --- | --- |
| `Map(source, destination)` dönüş değeri | `void` | Hedef nesneyi döndürür; iç nesneler ve koleksiyon örnekleri yerinde güncellenir |
| `Map` seçenek parametresi | `Action<VeloxResolutionContext>` | `Action<IMappingOperationOptions<...>>` |
| `ReverseMap()` dönüş değeri | İleri yönlü ifade | Ters yönlü ifade (zincirlenen kurallar ters yöne uygulanır) |
| `ReverseMap()` ve `Ignore` | Ignore kuralları ters çevrilirdi | Ignore kuralları ters çevrilmez |
| `DoNotValidate()` | Üyeyi yok sayardı | Yalnızca doğrulamadan muaf tutar; üye konvansiyonla eşlenmeye devam eder |
| `DisableCtorValidation()` | — | Yalnızca kurucu doğrulamasını kapatır |
| `ForAllMembers` parametresi | — | `IMemberConfigurationExpression<TSource, TDestination, object>` |
| Tek parametreli `ConvertUsing` | `Func<TSource?, TDestination>` | `Expression<Func<TSource, TDestination>>` (ProjectTo'da da çalışır) |
| `ValueTransformers.Add<T>` | `Func<T, T>` | `Expression<Func<T, T>>` |
| `MapFrom<TResolver>()` | Tür kısıtları vardı | Kısıt yok; `IValueResolver` veya `IVeloxValueResolver` uygulaması yapılandırmada doğrulanır |
| `MapperConfiguration`, `VeloxMapperOptions` | `VeloxMapper.Configuration` | `VeloxMapper` |
| DI uzantıları | `VeloxMapper.DependencyInjection` | `Microsoft.Extensions.DependencyInjection` |
| `ProjectTo` | `VeloxMapper.Extensions` | `VeloxMapper.QueryableExtensions` |
| DI kaydı | `IVeloxMapper` singleton | `IMapper` ve `IVeloxMapper` transient (derlenmiş kod `MapperConfiguration` üzerinde paylaşılır) |
| `ShouldMapField` varsayılanı | — | Public field'lar eşlenir (AutoMapper ile aynı) |
| `AddGlobalIgnore` | Tam ad | Ön ek eşleşmesi (`"Audit"` → `AuditTrail` de yok sayılır) |
| `Mapper.GetUnproxiedType` | Adı `Proxy` ile biten türleri de çözerdi | Yalnızca `Castle.Proxies` ve `System.Data.Entity.DynamicProxies` namespace'lerindeki türleri çözer |

### 5.x'ten yükseltme

1. Paket sürümünü güncelleyin:

   ```bash
   dotnet add package VeloxMapper --version 6.0.0
   ```

2. `using VeloxMapper.DependencyInjection;` satırlarını silin. `AddVeloxMapper` artık `Microsoft.Extensions.DependencyInjection` namespace'indedir.
3. `ProjectTo` kullanan dosyalara `using VeloxMapper.QueryableExtensions;` ekleyin. Eski `VeloxMapper.Extensions.QueryableExtensions.ProjectTo(IVeloxMapper)` metodu gizli olarak korunur ancak yeni kodda kullanmayın.
4. `using VeloxMapper.Configuration;` satırını yalnızca `MapperConfiguration` veya `VeloxMapperOptions` için kullanıyorsanız `using VeloxMapper;` ile değiştirin; bu türler artık `VeloxMapper` namespace'indedir.
5. Derleyin ve hataları düzeltin:
   - **Blok gövdeli `ConvertUsing(s => { ... })`:** İfade gövdeli bir lambdaya çevirin veya `ConvertUsing((s, d) => { ... })` overload'unu kullanın.
   - **Blok gövdeli `ValueTransformers.Add<T>(v => { ... })`:** İfade gövdeli lambdaya çevirin.
   - **`Map` seçenek lambdaları:** `opt => opt.Items["key"] = value` biçimi aynen derlenir. `VeloxResolutionContext`'e özgü başka üyeler kullanıyorsanız `IMappingOperationOptions` üyelerine (`Items`, `State`, `BeforeMap`, `AfterMap`, `ConstructServicesUsing`) çevirin.
6. Davranış değişikliklerini gözden geçirin:
   - `DoNotValidate()` ile bir üyenin **atanmamasını** bekliyorsanız `Ignore()` kullanın.
   - `ReverseMap()` öncesinde tanımlanan `Ignore` kurallarının ters yönde de geçerli olmasını bekliyorsanız ters yöne ayrıca `Ignore` ekleyin. `ReverseMap()` artık ters ifadeyi döndürdüğü için sonrasına zincirlediğiniz kuralların ters yöne uygulandığını kontrol edin.
   - Public field içeren türlerde field'lar artık eşlenir. İstemiyorsanız `cfg.ShouldMapField = f => false;` ayarlayın.
   - `AddGlobalIgnore("X")` artık `X` ile başlayan tüm üyeleri yok sayar.
   - `IMapper` örneğini singleton olarak saklayan kodunuz varsa kaldırın; `IMapper`'ı enjekte edin.
7. `AssertConfigurationIsValid()` testlerinizi ve tüm test paketinizi çalıştırın.

### Düzeltilenler

- `ProjectTo` ifadelerinde string birleştirme ve metot çağrıları artık reddedilmiyor.
- Enum → enum eşlemesi ada göre yapılıyor (bulunamazsa sayısal değere göre); AutoMapper ile aynı.
- `IncludeBase` ile birleştirilen eşlemelerde `ConstructUsing`, tür dönüştürücü ve `IncludeMembers` tanımlarının kaybolması düzeltildi.
- `ForPath` ile tanımlanan fonksiyon tabanlı `MapFrom` kuralları çalışıyor.
- Döngüsel nesne grafiklerinde `StackOverflowException` oluşmuyor; döngüsel tür grafiklerinde `PreserveReferences` otomatik etkinleşir.
- `ForMember` ile field hedef üyeleri yapılandırılabiliyor.
- `IVeloxMapper` singleton kaydedildiği için resolver'lara scoped bağımlılık enjekte edilememesi sorunu giderildi.
- Adı `Proxy` ile biten normal sınıfların yanlışlıkla proxy olarak çözülmesi düzeltildi.

## 5.2.0 — 2026-07-07

### Kaldırılanlar

- Benchmark projesi (`VeloxMapper.Benchmarks`), ASP.NET Core örnek projesi (`VeloxMapper.WebSample`) ve konsol demosu (`VeloxMapperDemo`) depodan kaldırıldı. Çözüm yalnızca çekirdek kütüphane, ana kütüphane, Source Generator ve birim testlerini içerir.
- Eski NuGet paket çıktıları temizlendi.

Kütüphane API'sinde değişiklik yoktur.

## 5.0.0 — 2026-05-29

### Eklenenler

- `As<T>()`: eşleme sonucunu türetilmiş bir türe yönlendirme.
- `SetMappingOrder()`: üyelerin eşlenme sırasını belirleme.
- `IncludeAllDerived()`: tanımlı tüm türetilmiş eşlemeleri polimorfik eşlemeye dahil etme.
- Open generic eşlemeler: `CreateMap(typeof(Source<>), typeof(Dest<>))`.
- `ConvertUsing(Func<...>)` lambda overload'u.
- `UseDestinationValue()`: iç nesne ve koleksiyonlarda mevcut hedef örneğini koruma.
- Source Generator, taban sınıflardaki public property'leri de tarar.

### Değişenler

- `VeloxMapper.Core` ve `VeloxMapper` paketleri tek bir `VeloxMapper` paketinde birleştirildi.

### Düzeltilenler

- `VeloxVersion.Current` sabitinin eski sürümü göstermesi düzeltildi.
- `ProjectTo` çağrılarındaki `Queryable` metot aramaları önbelleğe alındı.
- `ResolutionContext` gereksinimi kontrolü önbelleğe alındı.
- Yinelenen `ReferenceComparer` sınıfı tekilleştirildi.

## 4.1.2 ve öncesi

- `ForPath()`, `Include()`, `MaxDepth()`, `PreserveReferences()`.
- Temel dependency injection entegrasyonu (`AddVeloxMapper`).
- `IValueResolver`, `IMemberValueResolver` ve `IValueConverter` desteği.

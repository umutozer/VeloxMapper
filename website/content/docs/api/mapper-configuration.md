---
title: "MapperConfiguration"
description: "Eşleme yapılandırmasını oluşturan, doğrulayan ve derlenmiş eşlemeleri tutan MapperConfiguration sınıfı ile yapılandırma seçeneklerinin referansı."
section: api
order: 20
---

# MapperConfiguration

`MapperConfiguration`, profillerden ve yapılandırma delegesinden oluşturulan değişmez (immutable) eşleme yapılandırmasıdır. Derlenmiş eşleme delegeleri ve projeksiyon ifadeleri bu nesnede önbelleklenir; uygulama boyunca tek örnek (singleton) olarak kullanılmalıdır.

```csharp
namespace VeloxMapper;

public sealed class MapperConfiguration : IConfigurationProvider
```

```csharp
var config = new MapperConfiguration(cfg =>
{
    cfg.AddProfile<OrderProfile>();
    cfg.CreateMap<Customer, CustomerDto>();
});

config.AssertConfigurationIsValid();
IMapper mapper = config.CreateMapper();
```

## Kurucular

```csharp
public MapperConfiguration(Action<VeloxMapperOptions> configure);
public MapperConfiguration(Action<VeloxMapperOptions> configure, ILoggerFactory? loggerFactory);
public MapperConfiguration(VeloxMapperOptions options);
public MapperConfiguration(VeloxMapperOptions options, ILoggerFactory? loggerFactory);
public MapperConfiguration(params Assembly[] assemblies);
```

| Kurucu | Kullanım |
| --- | --- |
| `(Action<VeloxMapperOptions>)` | En yaygın biçim. Delege parametresi `IMapperConfigurationExpression` uygular; AutoMapper'daki `cfg => ...` kodu değişmeden derlenir. |
| `(Action<...>, ILoggerFactory?)` | AutoMapper 15 imzası. Fabrika verilirse teşhis olayları `VeloxMapper` kategorisinde loglanır. |
| `(VeloxMapperOptions)` | Önceden doldurulmuş bir ifadeden; `new MapperConfigurationExpression()` ile oluşturulan AutoMapper tarzı kod için. |
| `(params Assembly[])` | Verilen assembly'lerdeki profilleri ve `[AutoMap]` türlerini tarar (`cfg.AddMaps(...)` kısayolu). |

Kurucu, yapılandırma hatalarını (aynı tür çiftinin iki kez tanımlanması gibi) hemen `VeloxConfigurationException` ile bildirir. Eşleme ifadeleri ise ilk kullanımda veya `CompileMappings()` çağrısında üretilir.

## IConfigurationProvider üyeleri

`IConfigurationProvider`, `IMapper.ConfigurationProvider` ve DI üzerinden erişilen arayüzdür. AutoMapper'daki aynı adlı arayüzle uyumludur.

### AssertConfigurationIsValid

```csharp
void AssertConfigurationIsValid();
void AssertConfigurationIsValid<TProfile>() where TProfile : VeloxProfile;
void AssertConfigurationIsValid(string profileName);
```

Tüm eşlemeleri (veya yalnızca verilen profildekileri) doğrular. Sorun varsa tüm hataları listeleyen bir `VeloxValidationException` fırlatır. Ayrıntılar ve örnek çıktı için [Yapılandırma Doğrulama](../configuration-validation.md).

AutoMapper ile aynı; istisna türü farklıdır.

### CompileMappings

```csharp
void CompileMappings();
```

Tüm açık `CreateMap` kayıtları için yeni nesne oluşturan eşleme delegelerini derler. İlk çağrıdaki derleme gecikmesini başlangıca taşımak için kullanılır. Bkz. [Performans](../performance.md#başlangıçta-ısıtma-compilemappings).

AutoMapper ile aynı.

### CreateMapper

```csharp
IVeloxMapper CreateMapper();
IVeloxMapper CreateMapper(Func<Type, object> serviceCtor);
```

Bu yapılandırmayı kullanan yeni bir mapper döndürür. `serviceCtor` verilirse resolver, converter ve mapping action örnekleri bu fabrikayla oluşturulur. `MapperConfiguration` üzerinde dönüş türü `IVeloxMapper`'dır (`IMapper`'dan türer); `IConfigurationProvider` üzerinden çağrıldığında `IMapper` döner.

AutoMapper ile aynı.

### BuildExecutionPlan

```csharp
LambdaExpression BuildExecutionPlan(Type sourceType, Type destinationType);
```

Bir tür çifti için üretilen eşleme ifadesini derlemeden döndürür. Hata ayıklayıcıdaki **DebugView** görünümüyle incelenir.

AutoMapper ile aynı.

## VeloxMapper'a özgü üyeler

Bu üyeler `MapperConfiguration` sınıfındadır; `IConfigurationProvider` arayüzünde yoktur. DI'da `MapperConfiguration` türü de singleton olarak kayıtlıdır.

### GetMappingPlan

```csharp
public MappingPlanDef GetMappingPlan(Type source, Type destination, MappingMode mode = MappingMode.Map);
```

Bir tür çifti için her hedef üyenin kaynağını ve eşleme biçimini (`Assigned`, `Flattened`, `Complex`, `Ignored`, `Unmapped`...) listeleyen planı döndürür. `MappingPlanReport` ile metin, JSON veya hash'e çevrilir. Bkz. [Teşhis & Mapping Planı](../diagnostics.md).

### RegisterPrecompiledMapper

```csharp
public void RegisterPrecompiledMapper<TSource, TDestination>(Func<TSource, TDestination> mapFunc);
```

Bir tür çifti için yeni nesne oluşturan kök `Map` çağrılarında ifade ağacı yerine verilen fonksiyonu kullanır. Source generator ile üretilen metotları bağlamak için tasarlanmıştır. Bkz. [Source Generator](../performance.md#source-generator).

### GetAllTypeMaps ve RegistrationCount

```csharp
public IReadOnlyCollection<TypeMap> GetAllTypeMaps();
public int RegistrationCount { get; }
```

Açık eşleme kayıtlarını (`ReverseMap` ile üretilenler ve ilk kullanımda kapatılan open generic eşlemeler dahil; open generic tanımların kendisi hariç) döndürür.

### Diğer özellikler

| Özellik | Açıklama |
| --- | --- |
| `IVeloxDiagnosticsSink? DiagnosticsSink { get; }` | Etkin teşhis hedefi (`cfg.DiagnosticsSink` veya `ILoggerFactory` üzerinden oluşturulan). |
| `bool IgnoreNullValues { get; }` | `cfg.PatchMapping.IgnoreNullValues` değeri. |

## Yapılandırma seçenekleri (cfg)

Kurucuya verilen delegenin parametresi `VeloxMapperOptions` türündedir. Bu sınıf AutoMapper'ın `IMapperConfigurationExpression` arayüzünü uygular. `MapperConfigurationExpression` sınıfı `VeloxMapperOptions`'tan türer ve AutoMapper'daki aynı adlı sınıfın karşılığıdır.

### Eşleme tanımları ve profiller

| Üye | Açıklama |
| --- | --- |
| `CreateMap<TSource, TDestination>()`, `CreateMap<,>(MemberList)` | Global (profil dışı) eşleme tanımlar. Bkz. [Profile](./profile.md#createmap). |
| `CreateMap(Type, Type)`, `CreateMap(Type, Type, MemberList)` | Tür parametreli veya open generic eşleme. |
| `CreateProjection<TSource, TDestination>()` | `ProjectTo` amaçlı eşleme; VeloxMapper'da `CreateMap` ile aynı kaydı oluşturur. |
| `AddProfile<TProfile>()`, `AddProfile(Profile)`, `AddProfile(Type)`, `AddProfiles(IEnumerable<VeloxProfile>)` | Profil ekler. `AddProfile<T>()` ve `AddProfile(Type)` aynı profil türünü ikinci kez eklemez. |
| `AddMaps(params Assembly[])`, `AddMaps(IEnumerable<Assembly>)` | Assembly'deki tüm `Profile` alt sınıflarını (parametresiz kurucusu olan) ve `[AutoMap]` türlerini ekler. |
| `AddMaps(params Type[])`, `AddMaps(IEnumerable<Type>)` | Verilen türlerin bulunduğu assembly'leri tarar. |
| `AddMaps(params string[])`, `AddMaps(IEnumerable<string>)` | Adı verilen assembly'leri yükleyip tarar. |
| `CreateProfile(string name, Action<IProfileExpression>)` | Satır içi, adlandırılmış bir profil oluşturur. |
| `ConstructServicesUsing(Func<Type, object>)` | Resolver, converter ve action örnekleri için varsayılan fabrika. |

### Konvansiyonlar ve global ayarlar

Bu üyeler `IProfileExpression` arayüzünden gelir ve profillerde de aynı adla bulunur. Global yapılandırmada verilen değerler tüm profillerin varsayılanıdır; profilde verilen değer o profil için globali ezer. Liste ayarları (önekler, global ignore'lar, extension metot türleri) birleştirilir.

| Üye | Varsayılan | Açıklama |
| --- | --- | --- |
| `AllowNullDestinationValues` | `true` | Bkz. [Null Yönetimi](../null-handling.md). |
| `AllowNullCollections` | `false` | Bkz. [Null Yönetimi](../null-handling.md). |
| `EnableNullPropagationForQueryMapping` | `true` | `ProjectTo` ifadelerinde ara üye null kontrolleri. |
| `SourceMemberNamingConvention`, `DestinationMemberNamingConvention` | `null` | Bkz. [İsimlendirme Kuralları](../naming-conventions.md). |
| `RecognizePrefixes`, `RecognizePostfixes`, `RecognizeDestinationPrefixes`, `RecognizeDestinationPostfixes`, `ClearPrefixes` | `Get` metot öneki tanınır | Önek/sonek kuralları. |
| `ReplaceMemberName(string, string)` | | Kaynak üye adlarında karşılaştırma öncesi metin değiştirme. |
| `AddGlobalIgnore(string)` | | Verilen metinle başlayan hedef üyeleri tüm eşlemelerde yok sayar. `VeloxMapperOptions` ayrıca `params string[]` overload'u sunar. |
| `ShouldMapProperty`, `ShouldMapField`, `ShouldMapMethod` | Public getter/setter; public field; parametresiz metotlar | Üye keşfi. |
| `ShouldUseConstructor` | Tüm kurucular | Hedef nesne oluşturmada kullanılabilecek kurucuları filtreler. |
| `DisableConstructorMapping()` | | Kurucu parametresi eşlemesini kapatır; parametresiz kurucu kullanılır. |
| `IncludeSourceExtensionMethods(Type)` | | Statik sınıftaki extension metotları kaynak üye olarak kullanır. |
| `ValueTransformers` | | Bkz. [Value Transformer](../value-transformers.md). |
| `ForAllMaps(Action<TypeMap, IMappingExpression>)` | | Tüm eşlemelere ortak yapılandırma. |
| `ForAllPropertyMaps(Func<PropertyMap, bool>, Action<PropertyMap, IMemberConfigurationExpression>)` | | Koşulu sağlayan tüm hedef üyelere ortak yapılandırma. |

#### ForAllMaps ve ForAllPropertyMaps

```csharp
var config = new MapperConfiguration(cfg =>
{
    cfg.AddMaps(typeof(OrderProfile).Assembly);

    // Tüm eşlemelerde "RowVersion" hedef üyesini yok say
    cfg.ForAllMaps((typeMap, map) =>
    {
        if (typeMap.DestinationType.GetProperty("RowVersion") != null)
            map.ForMember("RowVersion", o => o.Ignore());
    });

    // Tüm string hedef üyelerinde null yerine boş string yaz
    cfg.ForAllPropertyMaps(
        pm => pm.DestinationType == typeof(string),
        (pm, o) => o.NullSubstitute(string.Empty));
});
```

`TypeMap` özellikleri: `SourceType`, `DestinationType`, `ProfileName`. `PropertyMap` özellikleri: `TypeMap`, `DestinationMember`, `DestinationName`, `DestinationType`, `SourceMember`, `SourceType` (konvansiyonla eşleşen kaynak üye; yoksa `null`).

### VeloxMapper'a özgü seçenekler

| Üye | Açıklama |
| --- | --- |
| `PatchMapping.IgnoreNullValues` | `true` ise `Map(source, destination)` çağrılarında `null` kaynak değerler hedefe yazılmaz. Bkz. [Null Yönetimi](../null-handling.md#patch-için-null-değerleri-atlamak). |
| `DiagnosticsSink` | Teşhis olayları için log hedefi. Bkz. [Teşhis](../diagnostics.md#diagnosticssink). |
| `NamingConvention` | Taraf bazlı kural verilmemişse her iki tarafa uygulanan isimlendirme kuralı. |
| `AddCustomConverter<TSource, TDestination>(ITypeConverter<,> \| IVeloxTypeConverter<,>)` | `CreateMap<TSource, TDestination>().ConvertUsing(converter)` kısayolu. |
| `AddProfilesFromAssembly(Assembly)`, `AddProfilesFromAssemblyOf<T>()`, `AddProfilesFromAssemblies(params Assembly[])` | 5.x adları; `AddMaps` ile aynı. |
| `ForAllMaps(Action<MappingRegistration>)` | 5.x tarzı toplu yapılandırma; yeni kodda `ForAllMaps(Action<TypeMap, IMappingExpression>)` kullanın. |

### LicenseKey

```csharp
string? LicenseKey { get; set; }
```

AutoMapper 15+ ile derleme uyumluluğu için kabul edilir ve yok sayılır. VeloxMapper MIT lisanslıdır; lisans anahtarı gerekmez. IntelliSense'te gizlidir.

## AutoMapper uyumluluğu

`MapperConfiguration` kurucuları (`ILoggerFactory` alan AutoMapper 15 imzası dahil), `IConfigurationProvider` üyeleri ve `cfg` üzerindeki tüm yapılandırma yöntemleri AutoMapper ile aynı adlara sahiptir. `cfg.Internal()` desteklenmez; `GetAllTypeMaps()` doğrudan `MapperConfiguration` üzerindedir.

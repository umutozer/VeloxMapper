---
title: "Yapılandırma ve Profiller"
description: "MapperConfiguration ve Profile ile eşleme kurallarını tanımlayın, düzenleyin ve global ile profil düzeyindeki ayarların nasıl birleştiğini öğrenin."
section: core-concepts
order: 10
---

# Yapılandırma ve Profiller

Tüm eşleme kuralları bir `MapperConfiguration` nesnesinde toplanır. Kuralları gruplamak için `Profile` sınıflarını kullanırsınız; `IMapper` örnekleri bu yapılandırmadan oluşturulur.

## MapperConfiguration

`MapperConfiguration`, eşleme kurallarını tutan ve derlenmiş eşleme kodunu önbelleğe alan nesnedir. Uygulama başına **bir kez** oluşturun ve saklayın; her istekte yeniden oluşturmak, ilk eşleme maliyetini her seferinde yeniden ödemek demektir.

```csharp
using VeloxMapper;

var configuration = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<Customer, CustomerDto>();
    cfg.AddProfile<OrderProfile>();
});

IMapper mapper = configuration.CreateMapper();
```

DI kullanıyorsanız `AddVeloxMapper` bu nesneyi singleton olarak oluşturur (bkz. [Dependency Injection](./dependency-injection.md)).

### Kurucular

| Kurucu | Kullanım |
| --- | --- |
| `new MapperConfiguration(cfg => ...)` | En yaygın kullanım. |
| `new MapperConfiguration(cfg => ..., loggerFactory)` | AutoMapper 15 imzası. Teşhis olayları `VeloxMapper` log kategorisine yazılır. |
| `new MapperConfiguration(params Assembly[] assemblies)` | Assembly'lerdeki profilleri ve `[AutoMap]` özniteliklerini tarar. |
| `new MapperConfiguration(MapperConfigurationExpression expression)` | Önceden doldurulmuş bir yapılandırma ifadesinden oluşturur. |

Yapılandırma oluşturulduktan sonra değiştirilemez. `MapperConfiguration` ve ondan oluşturulan `IMapper` örnekleri iş parçacığı güvenlidir (thread-safe).

## Profiller

Profil, ilişkili eşlemeleri bir arada tutan bir sınıftır. `Profile` sınıfından türetin ve kuralları kurucu metotta tanımlayın:

```csharp title="CustomerProfile.cs"
using VeloxMapper;

public class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        CreateMap<Customer, CustomerDto>()
            .ForMember(d => d.FullName, opt => opt.MapFrom(s => s.FirstName + " " + s.LastName));

        CreateMap<Address, AddressDto>().ReverseMap();
    }
}
```

Profiller genellikle bir özellik alanına (feature) veya bir sınırlı bağlama (bounded context) göre düzenlenir: `OrderProfile`, `CustomerProfile`, `CatalogProfile` gibi.

### Profilleri ekleme

| Yöntem | Açıklama |
| --- | --- |
| `cfg.AddProfile<OrderProfile>()` | Tek bir profil türü ekler. |
| `cfg.AddProfile(new OrderProfile())` | Bir profil örneği ekler (ör. kurucu parametresi gerekiyorsa). |
| `cfg.AddProfile(typeof(OrderProfile))` | Türü çalışma zamanında bilinen profil. |
| `cfg.AddProfiles(profiles)` | Profil örneklerinden oluşan bir koleksiyon. |
| `cfg.AddMaps(typeof(OrderProfile))` | Türün assembly'sindeki tüm profilleri ve `[AutoMap]` özniteliklerini tarar. |
| `cfg.AddMaps(assembly1, assembly2)` | Assembly'leri doğrudan tarar. Assembly adları (`string`) da kabul edilir. |
| `cfg.CreateProfile("Raporlar", p => p.CreateMap<...>())` | Ayrı sınıf yazmadan, adlandırılmış bir profil oluşturur. |

Aynı profil türü birden fazla kez eklenirse (ör. hem taranır hem de `AddProfile<T>()` ile eklenirse) yalnızca bir kez işlenir.

### Satır içi yapılandırma

Küçük uygulamalarda veya testlerde kuralları doğrudan yapılandırma delegesinde tanımlayabilirsiniz. Bu kurallar `Global` adlı varsayılan profile aittir:

```csharp
var configuration = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<OrderLine, OrderLineDto>();
    cfg.CreateMap<Order, OrderDto>();
});
```

## CreateMap

`CreateMap<TSource, TDestination>()` bir tür çifti için eşleme tanımlar ve kuralları zincirlemenizi sağlayan bir `IMappingExpression<TSource, TDestination>` döndürür.

```csharp
CreateMap<Order, OrderDto>()
    .ForMember(d => d.CustomerName, opt => opt.MapFrom(s => s.Customer.FirstName))
    .ForMember(d => d.InternalNote, opt => opt.Ignore());
```

Her tür çifti yalnızca **bir kez** tanımlanabilir. Aynı çift ikinci kez tanımlanırsa (aynı veya farklı profilde) yapılandırma oluşturulurken `VeloxConfigurationException` fırlatılır.

### Doğrulanacak üye listesi

`CreateMap` isteğe bağlı bir `MemberList` parametresi alır. Bu değer, [`AssertConfigurationIsValid()`](./configuration-validation.md) çağrısının hangi tarafı denetleyeceğini belirler:

| Değer | Anlamı |
| --- | --- |
| `MemberList.Destination` (varsayılan) | Her hedef üyenin bir kaynağı olmalı. |
| `MemberList.Source` | Her kaynak üyenin bir hedefte kullanılması gerekir. Komut/istek nesnelerinden varlıklara eşlemede kullanışlıdır. |
| `MemberList.None` | Üye doğrulaması yapılmaz. |

```csharp
CreateMap<UpdateCustomerCommand, Customer>(MemberList.Source);
```

### Çalışma zamanı türleriyle ve open generic

Türler derleme zamanında bilinmiyorsa `Type` alan overload'u kullanın. Bu overload open generic türleri de destekler:

```csharp
CreateMap(typeof(PagedResult<>), typeof(PagedResultDto<>));
```

Ayrıntılar için [Open Generics](./open-generics.md) sayfasına bakın.

## Global ve profil düzeyindeki ayarlar

Konvansiyon ve null ayarları hem yapılandırma delegesinde (global) hem de bir profilin kurucusunda tanımlanabilir:

```csharp title="LegacyImportProfile.cs"
using VeloxMapper;

public class LegacyImportProfile : Profile
{
    public LegacyImportProfile()
    {
        // Yalnızca bu profildeki eşlemeleri etkiler
        SourceMemberNamingConvention = new LowerUnderscoreNamingConvention();
        DestinationMemberNamingConvention = new PascalCaseNamingConvention();
        RecognizePrefixes("tbl");

        CreateMap<LegacyCustomerRow, Customer>();
    }
}
```

Birleştirme kuralları AutoMapper ile aynıdır:

- Profilde tanımlanan bir ayar **yalnızca o profilin** eşlemelerine uygulanır.
- Profilde tanımlanmayan bir ayar global değerden gelir.
- Liste ayarları (`RecognizePrefixes`, `AddGlobalIgnore`, `ReplaceMemberName`, `ValueTransformers` gibi) global liste ile birleşir.

### Ayar listesi

| Ayar | Varsayılan | Açıklama |
| --- | --- | --- |
| `AllowNullDestinationValues` | `true` | `false` ise null kaynak nesne için hedefte boş bir nesne oluşturulur. |
| `AllowNullCollections` | `false` | `false` ise null kaynak koleksiyon boş koleksiyona eşlenir; `true` ise null kalır. |
| `EnableNullPropagationForQueryMapping` | `true` | `ProjectTo` ifadelerinde ara üyeleri null-güvenli okur. AutoMapper'da varsayılan `false`'tur. |
| `SourceMemberNamingConvention`, `DestinationMemberNamingConvention` | yok | Ad dönüştürme kuralı. Bkz. [Konvansiyonlar](./conventions.md#adlandırma-kuralları). |
| `RecognizePrefixes`, `RecognizePostfixes` | yok | Kaynak üye adlarındaki ön/son ekleri yok sayar. |
| `RecognizeDestinationPrefixes`, `RecognizeDestinationPostfixes` | yok | Hedef üye adlarındaki ön/son ekleri yok sayar. |
| `ClearPrefixes()` | — | Tanımlı ön ekleri ve varsayılan `Get` metot ön ekini temizler. |
| `ReplaceMemberName(original, newValue)` | yok | Kaynak üye adında karakter/metin değişimi yapar (ör. `"Ü"` → `"U"`). |
| `AddGlobalIgnore(prefix)` | yok | Adı bu metinle başlayan hedef üyeleri yok sayar. |
| `ShouldMapProperty` | getter veya setter'ı public olanlar | Hangi property'lerin eşleneceğini belirler. |
| `ShouldMapField` | public field'lar | Hangi field'ların eşleneceğini belirler. |
| `ShouldMapMethod` | özel adlı olmayan metotlar | Parametresiz kaynak metotlarından hangilerinin kullanılacağını belirler. |
| `ShouldUseConstructor` | tüm public kurucular | Hangi kurucuların değerlendirileceğini belirler. |
| `DisableConstructorMapping()` | — | Parametreli kurucu eşlemesini kapatır. Bkz. [Constructor ve Record](./constructors.md). |
| `IncludeSourceExtensionMethods(typeof(T))` | — | `T` içindeki extension metotları kaynak üye olarak kullanılabilir hale getirir. |
| `ValueTransformers.Add<T>(v => ...)` | — | `T` türündeki her değere uygulanan dönüşüm. Bkz. [Value Transformers](./value-transformers.md). |

## Toplu kurallar

### ForAllMaps

Tüm tür eşlemelerine aynı kuralı uygulamak için `ForAllMaps` kullanın. Delege her eşleme için `TypeMap` (kaynak ve hedef tür bilgisi) ve `IMappingExpression` alır:

```csharp
cfg.ForAllMaps((typeMap, map) =>
{
    if (typeMap.DestinationType.GetProperty("RowVersion") is not null)
    {
        map.ForMember("RowVersion", opt => opt.Ignore());
    }
});
```

`ForMember(string, ...)` hedefte olmayan bir üye adı verildiğinde `ArgumentException` fırlatır; bu nedenle üyenin varlığını önce denetleyin.

### ForAllPropertyMaps

Belirli bir koşulu sağlayan tüm üye eşlemelerine kural uygular:

```csharp
cfg.ForAllPropertyMaps(
    pm => pm.DestinationType == typeof(string) && pm.DestinationName.EndsWith("Note"),
    (pm, opt) => opt.NullSubstitute(""));
```

`PropertyMap`; `DestinationName`, `DestinationType`, `SourceMember`, `SourceType` ve `TypeMap` özelliklerini sunar.

## Yapılandırmayı doğrulama ve ısıtma

```csharp
configuration.AssertConfigurationIsValid();                  // tüm eşlemeler
configuration.AssertConfigurationIsValid<OrderProfile>();    // yalnızca bir profil
configuration.AssertConfigurationIsValid("Raporlar");         // adıyla bir profil
configuration.CompileMappings();                             // tüm eşlemeleri önceden derler
```

`AssertConfigurationIsValid()` çağrısını bir birim testine koyun. `CompileMappings()`, ilk eşleme çağrısının maliyetini uygulama başlangıcına taşır. Ayrıntılar için [Yapılandırma Doğrulama](./configuration-validation.md) sayfasına bakın.

## İlgili sayfalar

- [Eşleme Konvansiyonları](./conventions.md)
- [Üye Yapılandırması](./member-configuration.md)
- [MapperConfiguration API referansı](./api/mapper-configuration.md)
- [Profile API referansı](./api/profile.md)

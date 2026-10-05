---
title: "Profile"
description: "Eşleme tanımlarını ve profil düzeyindeki konvansiyonları gruplayan Profile sınıfının ve IProfileExpression arayüzünün referansı."
section: api
order: 50
---

# Profile

`Profile`, eşleme tanımlarını ve bu tanımlara uygulanacak konvansiyonları bir arada tutan taban sınıftır. Eşlemelerinizi özellik veya modül bazında profillere bölmek, büyük yapılandırmaları yönetilebilir tutar.

```csharp
namespace VeloxMapper;

public abstract class Profile : VeloxProfile
public abstract class VeloxProfile : IProfileExpression
```

Yeni kodda AutoMapper ile aynı ada sahip `Profile` sınıfından türetin. `VeloxProfile`, 5.x sürümleriyle uyumluluk için korunur ve tüm üyeleri sağlar; `Profile` ek bir üye tanımlamaz.

```csharp title="OrderProfile.cs"
using VeloxMapper;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        AllowNullCollections = true;
        RecognizePrefixes("str");

        CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.Customer.Name));
        CreateMap<OrderLine, OrderLineDto>().ReverseMap();
    }
}
```

## Profilleri yüklemek

| Yöntem | Örnek |
| --- | --- |
| DI ile assembly taraması | `services.AddVeloxMapper(typeof(OrderProfile));` |
| Assembly taraması | `cfg.AddMaps(typeof(OrderProfile).Assembly);` |
| Tür ile | `cfg.AddProfile<OrderProfile>();` veya `cfg.AddProfile(typeof(OrderProfile));` |
| Örnek ile | `cfg.AddProfile(new OrderProfile(settings));` |
| Satır içi | `cfg.CreateProfile("Raporlama", p => p.CreateMap<Order, OrderReportRow>());` |

Assembly taraması, parametresiz kurucusu (public veya non-public) olan, soyut olmayan tüm `Profile` alt sınıflarını yükler. Kurucusu parametre alan profilleri örnek olarak ekleyin.

## Kurucular

```csharp
protected Profile();
protected Profile(string profileName);
protected Profile(string profileName, Action<IProfileExpression> configurationAction);
```

| Kurucu | Açıklama |
| --- | --- |
| `Profile()` | Profil adı, sınıfın tam adıdır (`MyApp.Mapping.OrderProfile`). |
| `Profile(string)` | Profil adını verir. Doğrulama raporlarında ve `AssertConfigurationIsValid(string)` çağrısında bu ad kullanılır. |
| `Profile(string, Action<IProfileExpression>)` | Adı verir ve yapılandırma eylemini hemen uygular. |

AutoMapper ile aynı.

## ProfileName

```csharp
public virtual string ProfileName { get; }
```

Profilin adı. Varsayılan olarak türün tam adıdır.

## CreateMap

```csharp
IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>();
IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>(MemberList memberList);
IMappingExpression CreateMap(Type sourceType, Type destinationType);
IMappingExpression CreateMap(Type sourceType, Type destinationType, MemberList memberList);
```

Bir tür çifti için eşleme tanımlar ve kuralları zincirlemek için bir [IMappingExpression](./mapping-expression.md) döndürür. `memberList` doğrulanacak tarafı belirler; varsayılan `MemberList.Destination`. Tür parametreli overload'lar open generic türleri kabul eder.

Aynı tür çifti yapılandırmanın tamamında (tüm profiller dahil) yalnızca bir kez tanımlanabilir; ikinci tanım `MapperConfiguration` oluşturulurken `VeloxConfigurationException` fırlatır.

AutoMapper ile aynı.

## CreateProjection

```csharp
IMappingExpression<TSource, TDestination> CreateProjection<TSource, TDestination>();
IMappingExpression<TSource, TDestination> CreateProjection<TSource, TDestination>(MemberList memberList);
```

`ProjectTo` amaçlı eşleme tanımlar. VeloxMapper'da `CreateMap` ile aynı kaydı oluşturur; eşleme `Map` çağrılarında da kullanılabilir.

AutoMapper'da `CreateProjection` ile tanımlanan eşleme yalnızca `ProjectTo` ile kullanılabilir; VeloxMapper bu kısıtlamayı uygulamaz.

## Profil düzeyindeki ayarlar

Aşağıdaki üyeler `IProfileExpression` arayüzündendir ve hem profillerde hem global yapılandırmada (`cfg`) bulunur. Profil kurucusunda verilen ayar yalnızca o profilin eşlemelerine uygulanır; verilmeyen ayar global değerden gelir. Liste ayarları (önekler, sonekler, `ReplaceMemberName`, global ignore'lar, extension metot türleri) global listeyle birleştirilir.

| Üye | Tür / imza | Bkz. |
| --- | --- | --- |
| `AllowNullDestinationValues` | `bool?` (etkin varsayılan `true`) | [Null Yönetimi](../null-handling.md) |
| `AllowNullCollections` | `bool?` (etkin varsayılan `false`) | [Null Yönetimi](../null-handling.md) |
| `EnableNullPropagationForQueryMapping` | `bool?` (etkin varsayılan `true`) | [ProjectTo](../projection.md#null-propagation) |
| `SourceMemberNamingConvention` | `ICustomNamingConvention?` | [İsimlendirme Kuralları](../naming-conventions.md) |
| `DestinationMemberNamingConvention` | `ICustomNamingConvention?` | [İsimlendirme Kuralları](../naming-conventions.md) |
| `ClearPrefixes()` | | [İsimlendirme Kuralları](../naming-conventions.md#önek-ve-sonekler) |
| `RecognizePrefixes(params string[])` | | |
| `RecognizePostfixes(params string[])` | | |
| `RecognizeDestinationPrefixes(params string[])` | | |
| `RecognizeDestinationPostfixes(params string[])` | | |
| `ReplaceMemberName(string original, string newValue)` | | |
| `AddGlobalIgnore(string propertyNameStartingWith)` | | [ForMember & MapFrom](../member-configuration.md#üyeleri-yok-saymak) |
| `ShouldMapProperty` | `Func<PropertyInfo, bool>?` | [Üye keşfi](../naming-conventions.md#üye-keşfi) |
| `ShouldMapField` | `Func<FieldInfo, bool>?` | |
| `ShouldMapMethod` | `Func<MethodInfo, bool>?` | |
| `ShouldUseConstructor` | `Func<ConstructorInfo, bool>?` | [Constructor & Record](../constructors.md) |
| `DisableConstructorMapping()` | | [Constructor & Record](../constructors.md) |
| `IncludeSourceExtensionMethods(Type)` | | [Üye keşfi](../naming-conventions.md#üye-keşfi) |
| `ValueTransformers` | `ValueTransformerCollection` | [Value Transformer](../value-transformers.md) |
| `ForAllMaps(Action<TypeMap, IMappingExpression>)` | | [MapperConfiguration](./mapper-configuration.md#forallmaps-ve-forallpropertymaps) |
| `ForAllPropertyMaps(Func<PropertyMap, bool>, Action<PropertyMap, IMemberConfigurationExpression>)` | | [MapperConfiguration](./mapper-configuration.md#forallmaps-ve-forallpropertymaps) |

Nullable (`bool?`) özellikler okunduğunda yalnızca o düzeyde açıkça verilmiş değeri döndürür; verilmemişse `null`'dır ve etkin değer üst düzeyden gelir.

### Profil düzeyinde ForAllMaps

Profil içindeki `ForAllMaps` yalnızca o profilin eşlemelerine uygulanır:

```csharp
public class ApiProfile : Profile
{
    public ApiProfile()
    {
        CreateMap<Order, OrderDto>();
        CreateMap<Customer, CustomerDto>();

        ForAllMaps((typeMap, map) => map.ForAllMembers(o => o.Condition((src, dest, value) => value != null)));
    }
}
```

## Profilleri test etmek

Her profili ayrı bir yapılandırmada doğrulamak, hatanın kaynağını netleştirir:

```csharp
[Fact]
public void OrderProfile_is_valid()
    => new MapperConfiguration(cfg => cfg.AddProfile<OrderProfile>()).AssertConfigurationIsValid();
```

Bkz. [Yapılandırma Doğrulama](../configuration-validation.md#profil-bazında-doğrulama).

## AutoMapper uyumluluğu

`Profile` sınıfı, kurucuları ve `IProfileExpression` üyeleri AutoMapper ile aynıdır; mevcut profiller yalnızca `using AutoMapper;` satırı değiştirilerek derlenir. Farklar:

- Özel isimlendirme kuralları `INamingConvention` yerine `ICustomNamingConvention` arayüzüyle yazılır.
- `CreateProjection` ile tanımlanan eşlemeler `Map` ile de kullanılabilir.

---
title: "Attribute ile Eşleme"
description: "Eşlemeleri profil yazmadan, hedef türün üzerindeki [AutoMap] ve üye attribute'larıyla tanımlayın."
section: advanced
order: 60
---

# Attribute ile Eşleme

Basit eşlemeler için profil yazmak yerine kuralları doğrudan hedef türün üzerinde attribute'larla tanımlayabilirsiniz. Assembly taraması sırasında her `[AutoMap]` attribute'u bir `CreateMap` kaydına dönüşür.

## Temel kullanım

```csharp title="ProductDto.cs"
using VeloxMapper;
using VeloxMapper.Configuration.Annotations;

[AutoMap(typeof(Product))]
public class ProductDto
{
    public int Id { get; set; }

    [SourceMember(nameof(Product.Title))]
    public string Name { get; set; } = "";

    [SourceMember("Category.Name")]
    public string CategoryName { get; set; } = "";

    [NullSubstitute("Açıklama yok")]
    public string? Description { get; set; }

    [Ignore]
    public bool IsSelected { get; set; }
}
```

Attribute'lar yalnızca assembly taranırken okunur. Taramayı şu yollardan biriyle başlatın:

```csharp
// DI
builder.Services.AddVeloxMapper(typeof(ProductDto));

// DI olmadan
var config = new MapperConfiguration(cfg => cfg.AddMaps(typeof(ProductDto).Assembly));
```

`[AutoMap]` ile tanımlanan bir tür çifti için aynı zamanda bir profilde `CreateMap` yazarsanız yapılandırma oluşturulurken "birden fazla kez tanımlanmış" hatası alırsınız. Bir tür çiftini tek bir yerde tanımlayın.

## [AutoMap] seçenekleri

`[AutoMap]` `VeloxMapper` namespace'indedir ve aynı türe birden çok kez uygulanabilir (farklı kaynak türleri için).

| Özellik | Karşılığı |
| --- | --- |
| `SourceType` (kurucu parametresi) | `CreateMap<SourceType, BuTür>()` |
| `ReverseMap = true` | `.ReverseMap()` |
| `MaxDepth = n` | `.MaxDepth(n)` |
| `PreserveReferences = true` | `.PreserveReferences()` |
| `IncludeAllDerived = true` | `.IncludeAllDerived()` |
| `DisableCtorValidation = true` | `.DisableCtorValidation()` |
| `TypeConverter = typeof(T)` | `.ConvertUsing(typeof(T))` |
| `ConstructUsingServiceLocator = true` | `.ConstructUsingServiceLocator()` |
| `AsProxy = true` | Kabul edilir, etkisi yoktur |

```csharp
[AutoMap(typeof(Category), ReverseMap = true, MaxDepth = 3)]
public class CategoryDto
{
    public string Name { get; set; } = "";
    public CategoryDto? Parent { get; set; }
}
```

## Üye attribute'ları

Üye attribute'ları `VeloxMapper.Configuration.Annotations` namespace'indedir ve property veya field üzerinde kullanılır.

| Attribute | Karşılığı |
| --- | --- |
| `[Ignore]` | `o.Ignore()` |
| `[SourceMember("Ad")]` veya `[SourceMember("A.B")]` | `o.MapFrom("Ad")` |
| `[NullSubstitute(değer)]` | `o.NullSubstitute(değer)` |
| `[ValueResolver(typeof(TResolver))]` | `o.MapFrom(typeof(TResolver))` |
| `[ValueConverter(typeof(TConverter))]` | `o.ConvertUsing<TConverter, ...>()` (aynı adlı veya `[SourceMember]` ile verilen kaynak üye) |
| `[UseExistingValue]` | `o.UseDestinationValue()` |
| `[MappingOrder(n)]` | `o.SetMappingOrder(n)` |
| `[MapAtRuntime]` | Kabul edilir, etkisi yoktur |

`[ValueConverter]` ile `[SourceMember]` birlikte kullanıldığında converter, `[SourceMember]` ile belirtilen kaynak üyeye uygulanır:

```csharp
[AutoMap(typeof(Order))]
public class OrderSummaryDto
{
    [SourceMember(nameof(Order.Total))]
    [ValueConverter(typeof(CurrencyConverter))]
    public string TotalText { get; set; } = "";
}
```

## Ne zaman attribute, ne zaman profil

| Attribute uygun | Profil uygun |
| --- | --- |
| Konvansiyona yakın, birkaç istisnası olan eşlemeler | Hesaplanmış `MapFrom` ifadeleri, koşullar, `ForPath` |
| DTO ile eşleme kuralının yan yana durması isteniyorsa | Domain/DTO katmanlarının birbirine bağımlı olmaması gerekiyorsa |
| | Profil düzeyinde ayarlar (isimlendirme kuralları, transformer'lar) |

Attribute'lar `[AutoMap]` uygulanan türün bağlı olduğu kaynak türü bilmesini gerektirir. Hedef türlerinizi içeren assembly kaynak türlere referans veremiyorsa profil kullanın.

## Source Generator attribute'u

`[VeloxMap]` (`VeloxMapper.Attributes` namespace'i) `[AutoMap]`'ten farklıdır: çalışma zamanı eşlemesi tanımlamaz, derleme zamanında bir eşleme metodu üretir. Ayrıntılar için [Performans & Source Generator](./performance.md#source-generator) sayfasına bakın.

## AutoMapper uyumluluğu

`[AutoMap]` ve tüm üye attribute'ları AutoMapper ile aynı adlara ve davranışa sahiptir; yalnızca namespace değişir (`AutoMapper.Configuration.Annotations` → `VeloxMapper.Configuration.Annotations`). Tam liste için [Attribute'lar API](./api/attributes.md) sayfasına bakın.

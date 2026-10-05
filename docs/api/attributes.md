---
title: "Attribute'lar"
description: "[AutoMap], üye anotasyonları, Source Generator için [VeloxMap] ve kurucu seçimi için [VeloxConstructor] attribute'larının referansı."
section: api
order: 70
---

# Attribute'lar

VeloxMapper'daki attribute'lar üç gruba ayrılır: çalışma zamanı eşlemesini model üzerinde tanımlayan `[AutoMap]` ve üye anotasyonları, derleme zamanında kod üreten `[VeloxMap]` ve kurucu seçimini belirleyen `[VeloxConstructor]`.

| Attribute | Namespace | Hedef |
| --- | --- | --- |
| `[AutoMap]` | `VeloxMapper` | Sınıf, struct, arayüz |
| `[Ignore]`, `[SourceMember]`, `[NullSubstitute]`, `[ValueResolver]`, `[ValueConverter]`, `[UseExistingValue]`, `[MappingOrder]`, `[MapAtRuntime]` | `VeloxMapper.Configuration.Annotations` | Property, field |
| `[VeloxMap]` | `VeloxMapper.Attributes` | Assembly, sınıf |
| `[VeloxConstructor]` | `VeloxMapper.Attributes` | Kurucu |

Kullanım rehberi için [Attribute ile Eşleme](../attribute-mapping.md) sayfasına bakın.

## AutoMap

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class AutoMapAttribute : Attribute
{
    public AutoMapAttribute(Type sourceType);

    public Type SourceType { get; }
    public bool ReverseMap { get; set; }
    public bool ConstructUsingServiceLocator { get; set; }
    public int MaxDepth { get; set; }
    public bool PreserveReferences { get; set; }
    public bool DisableCtorValidation { get; set; }
    public bool IncludeAllDerived { get; set; }
    public Type? TypeConverter { get; set; }
    public bool AsProxy { get; set; }
}
```

Uygulandığı türü hedef, `sourceType`'ı kaynak kabul eden bir `CreateMap` kaydı oluşturur. Kayıt yalnızca assembly taraması sırasında (`AddMaps`, `AddVeloxMapper(typeof(...))`, `new MapperConfiguration(assemblies)`) yapılır.

| Özellik | Varsayılan | Karşılığı |
| --- | --- | --- |
| `ReverseMap` | `false` | `.ReverseMap()` |
| `ConstructUsingServiceLocator` | `false` | `.ConstructUsingServiceLocator()` |
| `MaxDepth` | `0` (sınırsız) | `.MaxDepth(n)` |
| `PreserveReferences` | `false` | `.PreserveReferences()` |
| `DisableCtorValidation` | `false` | `.DisableCtorValidation()` |
| `IncludeAllDerived` | `false` | `.IncludeAllDerived()` |
| `TypeConverter` | `null` | `.ConvertUsing(typeof(T))` |
| `AsProxy` | `false` | Kabul edilir, etkisi yoktur. |

```csharp
[AutoMap(typeof(Order))]
[AutoMap(typeof(OrderSnapshot))]
public class OrderDto { /* ... */ }
```

AutoMapper ile aynı; namespace `VeloxMapper`'dır.

## Üye anotasyonları

Bu attribute'lar yalnızca `[AutoMap]` ile işaretlenmiş bir türün property veya field'larında okunur.

### Ignore

```csharp
public sealed class IgnoreAttribute : Attribute
```

Üyeyi eşlemeden ve doğrulamadan çıkarır. `o.Ignore()` karşılığı.

### SourceMember

```csharp
public sealed class SourceMemberAttribute : Attribute
{
    public SourceMemberAttribute(string name);
    public string Name { get; }
}
```

Değeri adı veya noktalı yolu verilen kaynak üyeden alır. `o.MapFrom("Name")` karşılığı. `[ValueConverter]` ile birlikte kullanıldığında converter'ın girdi üyesini belirler.

```csharp
[SourceMember("Customer.Address.City")]
public string? City { get; set; }
```

### NullSubstitute

```csharp
public sealed class NullSubstituteAttribute : Attribute
{
    public NullSubstituteAttribute(object? value);
    public object? Value { get; }
}
```

Çözülen değer `null` ise `Value` yazılır. `o.NullSubstitute(value)` karşılığı.

### ValueResolver

```csharp
public sealed class ValueResolverAttribute : Attribute
{
    public ValueResolverAttribute(Type type);
    public Type Type { get; }
}
```

Değeri `IValueResolver<,,>` uygulayan türle üretir. `o.MapFrom(typeof(TResolver))` karşılığı.

### ValueConverter

```csharp
public sealed class ValueConverterAttribute : Attribute
{
    public ValueConverterAttribute(Type type);
    public Type Type { get; }
}
```

Aynı adlı (veya `[SourceMember]` ile belirtilen) kaynak üyeyi `IValueConverter<,>` uygulayan türle dönüştürür.

```csharp
[SourceMember(nameof(Order.Total))]
[ValueConverter(typeof(CurrencyConverter))]
public string TotalText { get; set; } = "";
```

### UseExistingValue

```csharp
public sealed class UseExistingValueAttribute : Attribute
```

Hedef üyedeki mevcut nesneyi/koleksiyonu korur. `o.UseDestinationValue()` karşılığı.

### MappingOrder

```csharp
public sealed class MappingOrderAttribute : Attribute
{
    public MappingOrderAttribute(int value);
    public int Value { get; }
}
```

Atama sırası; küçük değerler önce atanır. `o.SetMappingOrder(value)` karşılığı.

### MapAtRuntime

```csharp
public sealed class MapAtRuntimeAttribute : Attribute
```

AutoMapper ile derleme uyumluluğu için kabul edilir; davranışı değiştirmez.

Tüm üye anotasyonları AutoMapper ile aynı ad ve davranışa sahiptir; namespace `AutoMapper.Configuration.Annotations` yerine `VeloxMapper.Configuration.Annotations`'dır.

## VeloxMap

```csharp
namespace VeloxMapper.Attributes;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class VeloxMapAttribute : Attribute
{
    public VeloxMapAttribute(Type sourceType, Type destinationType);
    public Type SourceType { get; }
    public Type DestinationType { get; }
}
```

Source generator'ın, verilen tür çifti için derleme zamanında `VeloxMapper.Generated.GeneratedMappers.MapTo{HedefTamAdı}(this TSource)` extension metodunu üretmesini sağlar. Çalışma zamanı yapılandırmasına kayıt eklemez; `CreateMap` ile ilişkisi yoktur.

```csharp
[assembly: VeloxMap(typeof(MyApp.Catalog.Product), typeof(MyApp.Catalog.ProductDto))]

var dto = product.MapToMyAppCatalogProductDto();
```

Üretilen kodun kapsamı ve `RegisterPrecompiledMapper` ile bağlanması için [Source Generator](../performance.md#source-generator) bölümüne bakın.

VeloxMapper'a özgü.

## VeloxConstructor

```csharp
namespace VeloxMapper.Attributes;

[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class VeloxConstructorAttribute : Attribute
```

Hedef türün birden çok kurucusu olduğunda, çalışma zamanı motorunun hangi kurucuyu kullanacağını açıkça belirler.

```csharp
using VeloxMapper.Attributes;

public class Money
{
    public Money(decimal amount) : this(amount, "TRY") { }

    [VeloxConstructor]
    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }
    public string Currency { get; }
}
```

Bir türde birden fazla kurucu `[VeloxConstructor]` ile işaretlenirse çalışma zamanında `VeloxAmbiguousConstructorException` fırlatılır; tür `[VeloxMap]` eşlemesine katılıyorsa analyzer bunu derleme zamanında `VM001` hatasıyla bildirir. Kurucu seçim kuralları için [Constructor & Record](../constructors.md).

VeloxMapper'a özgü.

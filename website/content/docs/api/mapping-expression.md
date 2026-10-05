---
title: "IMappingExpression"
description: "CreateMap çağrısından dönen fluent IMappingExpression arayüzünün tüm yöntemleri: üye kuralları, dönüştürücüler, kalıtım, ReverseMap ve doğrulama."
section: api
order: 30
---

# IMappingExpression

`CreateMap<TSource, TDestination>()` bir `IMappingExpression<TSource, TDestination>` döndürür. Bir eşlemenin tüm kuralları bu arayüzdeki zincirlenebilir yöntemlerle tanımlanır. Yöntem adları ve overload'lar AutoMapper ile aynıdır.

```csharp
namespace VeloxMapper;

public interface IMappingExpression<TSource, TDestination>
```

Her yöntem aynı ifadeyi döndürür (`ReverseMap` hariç; o ters ifadeyi döndürür), böylece kurallar zincirlenebilir:

```csharp
CreateMap<Order, OrderDto>()
    .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.Customer.Name))
    .ForMember(d => d.InternalNote, o => o.Ignore())
    .AfterMap((s, d) => d.LoadedAt = DateTime.UtcNow)
    .ReverseMap();
```

## Üye yapılandırması

### ForMember

```csharp
IMappingExpression<TSource, TDestination> ForMember<TMember>(
    Expression<Func<TDestination, TMember>> destinationMember,
    Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions);

IMappingExpression<TSource, TDestination> ForMember(
    string name,
    Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions);
```

Bir hedef property veya field'ı yapılandırır. Seçici yalnızca doğrudan üyeyi gösterebilir (`d => d.Name`); iç içe yol (`d => d.Customer.Name`) `ArgumentException` fırlatır. Kural seçenekleri için [IMemberConfigurationExpression](./member-configuration-expression.md).

AutoMapper ile aynı.

### ForPath

```csharp
IMappingExpression<TSource, TDestination> ForPath<TMember>(
    Expression<Func<TDestination, TMember>> destinationMember,
    Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions);
```

İç içe bir hedef yolunu yapılandırır; yoldaki `null` ara nesneler oluşturulur. `ProjectTo`'da uygulanmaz. Bkz. [ForPath & IncludeMembers](../nested-mapping.md).

AutoMapper ile aynı.

### ForSourceMember

```csharp
IMappingExpression<TSource, TDestination> ForSourceMember(
    Expression<Func<TSource, object?>> sourceMember,
    Action<ISourceMemberConfigurationExpression> memberOptions);

IMappingExpression<TSource, TDestination> ForSourceMember(
    string sourceMemberName,
    Action<ISourceMemberConfigurationExpression> memberOptions);
```

Bir kaynak üyeyi `MemberList.Source` doğrulamasından muaf tutar. `ISourceMemberConfigurationExpression` iki yöntem sunar: `DoNotValidate()` ve aynı işi yapan `Ignore()`.

```csharp
CreateMap<RegisterRequest, User>(MemberList.Source)
    .ForSourceMember(s => s.PasswordConfirmation, o => o.DoNotValidate());
```

AutoMapper ile aynı.

### ForCtorParam

```csharp
IMappingExpression<TSource, TDestination> ForCtorParam(
    string ctorParamName,
    Action<ICtorParamConfigurationExpression<TSource>> paramOptions);
```

Bir kurucu parametresinin değer kaynağını belirler. `ICtorParamConfigurationExpression<TSource>` üç `MapFrom` overload'u sunar:

```csharp
void MapFrom<TMember>(Expression<Func<TSource, TMember>> sourceMember);
void MapFrom<TMember>(Func<TSource, ResolutionContext, TMember> resolver);
void MapFrom(string sourceMembersPath);
```

```csharp
CreateMap<PersonEntity, PersonRecord>()
    .ForCtorParam("fullName", o => o.MapFrom(s => s.FirstName + " " + s.LastName));
```

Parametre adı büyük/küçük harf duyarsız eşleşir. Bkz. [Constructor & Record](../constructors.md).

AutoMapper ile aynı.

### ForAllMembers ve ForAllOtherMembers

```csharp
IMappingExpression<TSource, TDestination> ForAllMembers(
    Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions);

IMappingExpression<TSource, TDestination> ForAllOtherMembers(
    Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions);
```

`ForAllMembers` tüm hedef üyelere, `ForAllOtherMembers` yalnızca `ForMember`/`ForPath` ile yapılandırılmamış üyelere aynı kuralı uygular.

```csharp
CreateMap<PatchCustomerRequest, Customer>()
    .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));
```

AutoMapper ile aynı.

### Ignore (kısayol)

```csharp
IMappingExpression<TSource, TDestination> Ignore(Expression<Func<TDestination, object?>> destinationMember);
```

`ForMember(d => d.X, o => o.Ignore())` kısayolu.

VeloxMapper'a özgü.

### AddTransform

```csharp
IMappingExpression<TSource, TDestination> AddTransform<TValue>(Expression<Func<TValue, TValue>> transformer);
```

Bu eşlemedeki `TValue` türündeki tüm hedef değerlere uygulanan bir value transformer ekler. Bkz. [Value Transformer](../value-transformers.md).

AutoMapper ile aynı.

## Tür dönüştürme

### ConvertUsing

```csharp
IMappingExpression<TSource, TDestination> ConvertUsing(Expression<Func<TSource, TDestination>> mappingExpression);
IMappingExpression<TSource, TDestination> ConvertUsing(Func<TSource, TDestination, TDestination> mappingFunction);
IMappingExpression<TSource, TDestination> ConvertUsing(Func<TSource, TDestination, ResolutionContext, TDestination> mappingFunction);
IMappingExpression<TSource, TDestination> ConvertUsing(ITypeConverter<TSource, TDestination> converter);
IMappingExpression<TSource, TDestination> ConvertUsing<TTypeConverter>();
IMappingExpression<TSource, TDestination> ConvertUsing(Type typeConverterType);
IMappingExpression<TSource, TDestination> ConvertUsing(IVeloxTypeConverter<TSource, TDestination> converter);
```

Eşlemeyi tamamen bir ifadeye, fonksiyona veya dönüştürücüye devreder; üye kuralları uygulanmaz. Yalnızca ifade overload'u `ProjectTo`'da çalışır. Bkz. [Type Converter](../type-converters.md).

AutoMapper ile aynı. Tek parametreli overload `Expression<Func<,>>` aldığı için ifade gövdeli lambda gerekir (AutoMapper'da da aynıdır). `IVeloxTypeConverter` overload'u 5.x uyumluluğu içindir.

### ConvertUsingEnumMapping

```csharp
IMappingExpression<TSource, TDestination> ConvertUsingEnumMapping(Action<EnumMappingExpression<TSource, TDestination>> configure);
```

İki enum (veya `Nullable` enum) arasında eşleme kurallarını belirler. `EnumMappingExpression` yöntemleri:

| Yöntem | Açıklama |
| --- | --- |
| `MapByName(bool ignoreCase = false)` | Üyeleri isme göre eşler; eşleşmeyen değerler sayısal değerle eşlenir. |
| `MapByValue()` | Sayısal değere göre eşler (`ConvertUsingEnumMapping` içindeki varsayılan). |
| `MapValue(TSource source, TDestination destination)` | Belirli bir değer için özel eşleme. `ReverseMap` ile ters çevrilir. |

Türlerden biri enum değilse `ArgumentException` fırlatılır. Bkz. [Enum Eşleme](../enums.md).

AutoMapper.Extensions.EnumMapping paketindeki API ile aynı; ayrı paket gerekmez.

## Nesne oluşturma

```csharp
IMappingExpression<TSource, TDestination> ConstructUsing(Func<TSource, TDestination> ctor);
IMappingExpression<TSource, TDestination> ConstructUsing(Func<TSource, ResolutionContext, TDestination> ctor);
IMappingExpression<TSource, TDestination> ConstructUsingServiceLocator();
```

| Yöntem | Açıklama |
| --- | --- |
| `ConstructUsing(src => new T(...))` | Hedef nesneyi verilen fabrikayla oluşturur; üye atamaları ardından uygulanır. |
| `ConstructUsing((src, ctx) => ...)` | Fabrika çağrı bağlamına erişir. |
| `ConstructUsingServiceLocator()` | Hedef nesneyi resolver'larla aynı mekanizmayla (DI) çözer. |

```csharp
CreateMap<OrderEntity, Order>()
    .ConstructUsing(src => Order.Rehydrate(src.Id, src.CreatedAt));
```

AutoMapper ile aynı. `ConstructUsing` fonksiyonları `ProjectTo`'da kullanılmaz.

## BeforeMap ve AfterMap

```csharp
IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> beforeFunction);
IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination, ResolutionContext> beforeFunction);
IMappingExpression<TSource, TDestination> BeforeMap<TMappingAction>();

IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> afterFunction);
IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination, ResolutionContext> afterFunction);
IMappingExpression<TSource, TDestination> AfterMap<TMappingAction>();
```

Üye atamalarından önce veya sonra çalışan eylemler. Tür parametreli overload'lar `IMappingAction<TSource, TDestination>` uygulayan ve DI ile çözülen bir sınıf alır. Bkz. [Before/After Map](../before-after-map.md).

AutoMapper ile aynı.

## Kalıtım

```csharp
IMappingExpression<TSource, TDestination> Include<TOtherSource, TOtherDestination>()
    where TOtherSource : TSource where TOtherDestination : TDestination;
IMappingExpression<TSource, TDestination> Include(Type derivedSourceType, Type derivedDestinationType);
IMappingExpression<TSource, TDestination> IncludeBase<TSourceBase, TDestinationBase>();
IMappingExpression<TSource, TDestination> IncludeBase(Type sourceBase, Type destinationBase);
IMappingExpression<TSource, TDestination> IncludeAllDerived();
IMappingExpression<TSource, TDestination> As<TOtherDestination>() where TOtherDestination : TDestination;
```

| Yöntem | Açıklama |
| --- | --- |
| `Include` | Türetilmiş tür çiftini polimorfik eşlemeye ekler. |
| `IncludeBase` | Taban eşlemenin kurallarını devralır. |
| `IncludeAllDerived` | Tabandan türeyen tüm eşlemeleri polimorfik eşlemeye ekler. |
| `As<T>` | Sonucu her zaman türetilmiş `T` türünde üretir. |

Bkz. [Kalıtım & Polimorfizm](../inheritance.md).

AutoMapper ile aynı.

### IncludeMembers

```csharp
IMappingExpression<TSource, TDestination> IncludeMembers(params Expression<Func<TSource, object?>>[] memberExpressions);
```

Kaynağın alt nesnelerindeki üyeleri hedef için kaynak üye havuzuna ekler. Her alt nesne türü için hedefe ayrı bir `CreateMap` tanımlanmalıdır. Bkz. [ForPath & IncludeMembers](../nested-mapping.md#includemembers-alt-nesneleri-düzleştirmek).

AutoMapper ile aynı.

## ReverseMap

```csharp
IMappingExpression<TDestination, TSource> ReverseMap();
```

Ters yönde bir eşleme oluşturur ve **ters ifadeyi** döndürür. Zincirde sonra gelen kurallar yalnızca ters yöne uygulanır. Ters eşleme `MemberList.None` ile oluşturulur. Bkz. [ReverseMap](../reverse-mapping.md).

AutoMapper ile aynı.

## Derinlik ve referanslar

```csharp
IMappingExpression<TSource, TDestination> MaxDepth(int depth);
IMappingExpression<TSource, TDestination> PreserveReferences();
```

| Yöntem | Açıklama |
| --- | --- |
| `MaxDepth(n)` | Rekürsif eşlemeyi `n` seviyede keser; `ProjectTo`'da rekürsif modeller için zorunludur. |
| `PreserveReferences()` | Aynı `Map` çağrısında aynı kaynak nesne için aynı hedef örneğini kullanır. Döngüsel tür grafiklerinde otomatik açılır. |

Bkz. [Döngüsel Referanslar](../circular-references.md).

AutoMapper ile aynı.

## Doğrulama

```csharp
IMappingExpression<TSource, TDestination> ValidateMemberList(MemberList memberList);
IMappingExpression<TSource, TDestination> DisableCtorValidation();
IMappingExpression<TSource, TDestination> IgnoreAllPropertiesWithAnInaccessibleSetter();
IMappingExpression<TSource, TDestination> IgnoreAllSourcePropertiesWithAnInaccessibleSetter();
```

| Yöntem | Açıklama |
| --- | --- |
| `ValidateMemberList` | Doğrulanacak tarafı değiştirir (`Destination`, `Source`, `None`). |
| `DisableCtorValidation` | Yalnızca kurucu doğrulamasını kapatır. |
| `IgnoreAllPropertiesWithAnInaccessibleSetter` | Setter'ı private/protected olan hedef property'leri yok sayar. |
| `IgnoreAllSourcePropertiesWithAnInaccessibleSetter` | Getter'ı public olmayan kaynak property'leri kaynak doğrulamasından muaf tutar. |

Bkz. [Yapılandırma Doğrulama](../configuration-validation.md).

AutoMapper ile aynı.

## Non-generic IMappingExpression

`CreateMap(Type, Type)` (open generic dahil) ve `ForAllMaps` çağrıları non-generic `IMappingExpression` döndürür. Üye adları string olarak verilir.

```csharp
public interface IMappingExpression
{
    IMappingExpression ReverseMap();
    IMappingExpression ForMember(string name, Action<IMemberConfigurationExpression> memberOptions);
    IMappingExpression ForAllMembers(Action<IMemberConfigurationExpression> memberOptions);
    IMappingExpression ForAllOtherMembers(Action<IMemberConfigurationExpression> memberOptions);
    IMappingExpression ForSourceMember(string sourceMemberName, Action<ISourceMemberConfigurationExpression> memberOptions);
    IMappingExpression ConvertUsing(Type typeConverterType);
    IMappingExpression Include(Type derivedSourceType, Type derivedDestinationType);
    IMappingExpression IncludeBase(Type sourceBase, Type destinationBase);
    IMappingExpression IncludeAllDerived();
    IMappingExpression As(Type typeOverride);
    IMappingExpression MaxDepth(int depth);
    IMappingExpression PreserveReferences();
    IMappingExpression ValidateMemberList(MemberList memberList);
    IMappingExpression DisableCtorValidation();
    IMappingExpression IgnoreAllPropertiesWithAnInaccessibleSetter();
    IMappingExpression IgnoreAllSourcePropertiesWithAnInaccessibleSetter();
}
```

`IMemberConfigurationExpression`, `IMemberConfigurationExpression<object, object, object>`'ten türer; tür parametresiz kurallar (`MapFrom(string)`, `Ignore`, `Condition`...) aynı şekilde kullanılır.

```csharp
cfg.CreateMap(typeof(Page<>), typeof(PageDto<>))
    .ForMember("Total", o => o.MapFrom("TotalCount"))
    .ReverseMap();
```

Bkz. [Open Generic](../open-generics.md).

AutoMapper ile aynı.

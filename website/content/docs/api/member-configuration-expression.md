---
title: "IMemberConfigurationExpression"
description: "ForMember, ForPath ve ForAllMembers içinde tek bir hedef üyeyi yapılandıran IMemberConfigurationExpression arayüzünün tüm yöntemleri."
section: api
order: 40
---

# IMemberConfigurationExpression

`ForMember`, `ForPath`, `ForAllMembers` ve `ForAllOtherMembers` çağrılarındaki `o` parametresi bu arayüzdür. Bir hedef üyenin değerinin nereden geleceğini, hangi koşulda atanacağını ve `null` durumunda ne olacağını belirler.

```csharp
namespace VeloxMapper;

public interface IMemberConfigurationExpression<TSource, TDestination, TMember>
public interface IMemberConfigurationExpression : IMemberConfigurationExpression<object, object, object>
```

`TMember`, hedef üyenin türüdür. `ForAllMembers`, `ForAllOtherMembers` ve string tabanlı `ForMember` içinde `TMember` `object`'tir.

## DestinationMember

```csharp
MemberInfo DestinationMember { get; }
```

Yapılandırılan hedef üye. `ForAllMembers` içinde üyeye göre karar vermek için kullanılır:

```csharp
.ForAllMembers(o =>
{
    if (o.DestinationMember.Name.EndsWith("Id")) o.Ignore();
});
```

AutoMapper ile aynı.

## Değer kaynağı

### MapFrom (ifade)

```csharp
void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> mapExpression);
```

Değeri bir kaynak ifadesinden alır. İfadenin türü hedef üyeden farklı olabilir; değer normal eşleme kurallarıyla (iç içe nesne, koleksiyon, enum, sayısal/string dönüşüm) çevrilir. Ara üyeler null-güvenlidir. `ProjectTo`'da SQL'e çevrilir.

```csharp
o.MapFrom(s => s.Customer.FirstName + " " + s.Customer.LastName)
o.MapFrom(s => s.Lines)          // List<OrderLine> → List<OrderLineDto>
```

AutoMapper ile aynı.

### MapFrom (yol)

```csharp
void MapFrom(string sourceMembersPath);
```

Değeri bir üye adından veya noktalı yoldan (`"Customer.Address.City"`) alır. `ProjectTo`'da çalışır.

AutoMapper ile aynı.

### MapFrom (fonksiyon)

```csharp
void MapFrom<TResult>(Func<TSource, TDestination, TResult> mappingFunction);
void MapFrom<TResult>(Func<TSource, TDestination, TMember, TResult> mappingFunction);
void MapFrom<TResult>(Func<TSource, TDestination, TMember, ResolutionContext, TResult> mappingFunction);
```

Değeri bir fonksiyonla hesaplar. Parametreler sırasıyla kaynak, hedef, hedef üyenin mevcut değeri ve çağrı bağlamıdır. `ProjectTo`'da atlanır.

```csharp
o.MapFrom((src, dest, current, ctx) => ctx.Items.TryGetValue("Lang", out var lang) ? Localize(src.Title, (string)lang) : src.Title)
```

AutoMapper ile aynı.

### MapFrom (resolver)

```csharp
void MapFrom<TValueResolver>();
void MapFrom(Type valueResolverType);
void MapFrom(IValueResolver<TSource, TDestination, TMember> valueResolver);

void MapFrom<TValueResolver, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember);
void MapFrom<TValueResolver, TSourceMember>(string sourceMemberName);
void MapFrom<TSourceMember>(IMemberValueResolver<TSource, TDestination, TSourceMember, TMember> valueResolver,
    Expression<Func<TSource, TSourceMember>> sourceMember);
```

Değeri bir value resolver ile üretir. Tür olarak verilen resolver'lar DI'dan çözülür. `TValueResolver`'ın `IValueResolver<,,>` (veya `IMemberValueResolver<,,,>`) uygulamadığı yapılandırma sırasında `ArgumentException` ile bildirilir. `ProjectTo`'da atlanır. Bkz. [Value Resolver](../value-resolvers.md).

AutoMapper ile aynı. 5.x `IVeloxValueResolver` türleri de kabul edilir.

### ConvertUsing (value converter)

```csharp
void ConvertUsing<TValueConverter, TSourceMember>();
void ConvertUsing<TValueConverter, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember);
void ConvertUsing<TValueConverter, TSourceMember>(string sourceMemberName);
void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter);
void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter, Expression<Func<TSource, TSourceMember>> sourceMember);
void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter, string sourceMemberName);
void ConvertUsing<TSourceMember>(IVeloxValueConverter<TSourceMember, TMember> converter, Expression<Func<TSource, TSourceMember>> sourceMember);
```

Bir kaynak üyeyi value converter ile dönüştürür. Kaynak üye verilmezse hedefle aynı adlı kaynak üye kullanılır. `ProjectTo`'da atlanır. Bkz. [Value Converter](../value-converters.md).

AutoMapper ile aynı.

## Yok sayma ve doğrulama

```csharp
void Ignore();
void DoNotValidate();
```

| Yöntem | Üye eşlenir mi? | Doğrulamada hata verir mi? |
| --- | --- | --- |
| `Ignore()` | Hayır | Hayır |
| `DoNotValidate()` | Evet (konvansiyonla eşlenebiliyorsa) | Hayır |

`Ignore()` kuralları `ReverseMap` ile ters çevrilmez.

AutoMapper ile aynı.

## Koşullar

```csharp
void Condition(Func<TSource, bool> condition);
void Condition(Func<TSource, TDestination, bool> condition);
void Condition(Func<TSource, TDestination, TMember, bool> condition);
void Condition(Func<TSource, TDestination, TMember, TMember, bool> condition);
void Condition(Func<TSource, TDestination, TMember, TMember, ResolutionContext, bool> condition);

void PreCondition(Func<TSource, bool> condition);
void PreCondition(Func<ResolutionContext, bool> condition);
void PreCondition(Func<TSource, ResolutionContext, bool> condition);
void PreCondition(Func<TSource, TDestination, ResolutionContext, bool> condition);
```

`Condition` değer hesaplandıktan sonra, `PreCondition` hesaplanmadan önce değerlendirilir. `Condition`'ın üçüncü parametresi **çözülen kaynak değerdir**, dördüncüsü hedef üyenin mevcut değeridir. İkisi de `ProjectTo`'da uygulanmaz. Bkz. [Koşullu Eşleme](../conditional-mapping.md).

AutoMapper ile aynı.

## Null davranışı

```csharp
void NullSubstitute(object? nullSubstitute);
void AllowNull();
void DoNotAllowNull();
```

| Yöntem | Açıklama |
| --- | --- |
| `NullSubstitute(value)` | Çözülen değer `null` ise `value` yazılır (hedef türe dönüştürülür). `ProjectTo`'da çalışır. |
| `AllowNull()` | `null` kaynak hedefe `null` olarak yazılır (koleksiyonlarda boş koleksiyon oluşturulmaz). |
| `DoNotAllowNull()` | `null` kaynak için boş koleksiyon veya yeni nesne oluşturulur. |

`AllowNull`/`DoNotAllowNull`, `AllowNullCollections` ve `AllowNullDestinationValues` global/profil ayarlarını bu üye için ezer. Bkz. [Null Yönetimi](../null-handling.md).

AutoMapper ile aynı.

## Hedef değeri ve sıra

```csharp
void UseDestinationValue();
void DoNotUseDestinationValue();
void SetMappingOrder(int mappingOrder);
```

| Yöntem | Açıklama |
| --- | --- |
| `UseDestinationValue()` | Hedef üyedeki mevcut nesneyi/koleksiyonu korur ve kaynağı bu örneğe eşler. |
| `DoNotUseDestinationValue()` | Mevcut değeri kullanmaz; her eşlemede yeni değer atanır. |
| `SetMappingOrder(n)` | Atama sırası; küçük değerler önce atanır. |

AutoMapper ile aynı.

## Dönüşüm

```csharp
void AddTransform(Expression<Func<TMember, TMember>> transformer);
```

Yalnızca bu üyeye uygulanan bir value transformer ekler. Eşleme, profil ve global transformer'lardan önce uygulanır; `null` değerlere uygulanmaz. Bkz. [Value Transformer](../value-transformers.md).

AutoMapper ile aynı (AutoMapper `null` değerlere de uygular).

## ProjectTo

```csharp
void ExplicitExpansion();
```

Üyeyi `ProjectTo` sorgularına yalnızca `membersToExpand` ile açıkça istendiğinde ekler. `Map` çağrılarını etkilemez. Bkz. [ExplicitExpansion](../projection.md#explicitexpansion).

AutoMapper ile aynı.

## Uyumluluk için kabul edilen

```csharp
void MapAtRuntime();
```

AutoMapper koduyla derleme uyumluluğu için kabul edilir; davranışı değiştirmez.

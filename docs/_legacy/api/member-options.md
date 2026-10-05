---
id: member-options
title: Üye Seçenekleri (IMemberConfigurationExpression)
sidebar_label: Member Options
description: ForMember içindeki opt seçeneklerinin API referansı.
---

# `IMemberConfigurationExpression<TSource, TDestination, TMember>`

Namespace: `VeloxMapper`. `ForMember(dest => ..., opt => ...)` içindeki `opt`.

## MapFrom (8 biçim)

| İmza | Açıklama |
| :--- | :--- |
| `MapFrom(Expression<Func<TSource,TMember>> sourceMember)` | Kaynak ifadesi (lambda). |
| `MapFrom(string sourceMember)` | Kaynak üye adı; noktalı nested yol (`"Address.City"`). |
| `MapFrom(Func<TSource,TDestination,TMember>)` | `(src, dest)`. |
| `MapFrom(Func<TSource,TDestination,TMember,TMember>)` | `(src, dest, destMember)`. |
| `MapFrom(Func<TSource,TDestination,TMember,VeloxResolutionContext,TMember>)` | `(src, dest, destMember, ctx)`. |
| `MapFrom<TResolver>() where TResolver : class, IVeloxValueResolver<TSource,TDestination,TMember>` | DI-destekli resolver. |
| `MapFrom<TResolver,TSourceMember>(Expression<Func<TSource,TSourceMember>> src) where TResolver : class, IVeloxMemberValueResolver<...>` | Kaynak-üye alan resolver. |
| `MapFrom(Type valueResolverType)` | Resolver tipini DI'dan çöz. |

```csharp
.ForMember(d => d.City, o => o.MapFrom("Address.City"))
.ForMember(d => d.Total, o => o.MapFrom((s, d) => s.Amount + d.Fee))
.ForMember(d => d.FullName, o => o.MapFrom<FullNameResolver>());
```

## ConvertUsing (value converter)

| İmza |
| :--- |
| `ConvertUsing<TConverter,TSourceMember>(Expression<Func<TSource,TSourceMember>> src) where TConverter : class, IVeloxValueConverter<TSourceMember,TMember>` |
| `ConvertUsing<TSourceMember>(IVeloxValueConverter<TSourceMember,TMember> converter, Expression<Func<TSource,TSourceMember>> src)` |

## Condition (5 biçim)

| İmza |
| :--- |
| `Condition(Func<TSource,bool>)` |
| `Condition(Func<TSource,TDestination,bool>)` |
| `Condition(Func<TSource,TDestination,TMember,bool>)` |
| `Condition(Func<TSource,TDestination,TMember,TMember,bool>)` — `(src, dest, srcMember, destMember)` |
| `Condition(Func<TSource,TDestination,TMember,TMember,VeloxResolutionContext,bool>)` |

## PreCondition (3 biçim)

`PreCondition(Func<TSource,bool>)` · `PreCondition(Func<TSource,VeloxResolutionContext,bool>)` · `PreCondition(Func<TSource,TDestination,VeloxResolutionContext,bool>)`

> Yürütme sırası: **PreCondition → değer çözümleme → Condition → atama**.

## Diğer seçenekler

| İmza | Açıklama |
| :--- | :--- |
| `Ignore()` | Üyeyi eşleme dışı bırak. |
| `DoNotValidate()` | Eşleme + doğrulama dışı bırak. |
| `NullSubstitute(TMember substituteValue)` | Çözülen değer null ise varsayılan. |
| `UseDestinationValue()` | Hedefin mevcut değer/referansını koru. |
| `DoNotUseDestinationValue()` | Korumayı kapat (varsayılan). |
| `SetMappingOrder(int mappingOrder)` | Üye atama sırası. |
| `MapAtRuntime()` | Çalışma zamanı eşlemesi (API uyumluluğu). |
| `ExplicitExpansion()` | ProjectTo'da yalnızca açıkça istenince genişlet. |
| `AllowNull()` / `DoNotAllowNull()` | Üye bazında null davranışı (API uyumluluğu). |

## `IForAllMembersExpression<TSource,TDestination>`
`Condition(Func<TSource,TDestination,object?,bool>)` · `Ignore()`

## `ISourceMemberConfigurationExpression`
`DoNotValidate()` · `Ignore()`

## `ICtorParamConfigurationExpression<TSource>`
`MapFrom<TMember>(Expression<Func<TSource,TMember>> sourceMember)`

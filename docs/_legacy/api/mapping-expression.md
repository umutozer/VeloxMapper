---
id: mapping-expression
title: CreateMap Zinciri (IMappingExpression)
sidebar_label: Mapping Expression
description: CreateMap sonrası fluent zincir metotlarının API referansı.
---

# `IMappingExpression<TSource, TDestination>`

Namespace: `VeloxMapper`. `CreateMap<TSource,TDestination>()` çağrısından döner. Tüm metotlar zincirleme için aynı ifadeyi döndürür.

## Üye yapılandırma

| İmza | Açıklama |
| :--- | :--- |
| `ForMember<TMember>(Expression<Func<TDestination,TMember>> destMember, Action<IMemberConfigurationExpression<...>> opt)` | Hedef üyeyi lambda ile yapılandırır. |
| `ForMember(string destMember, Action<IMemberConfigurationExpression<TSource,TDestination,object>> opt)` | Hedef üyeyi **adıyla** yapılandırır. |
| `ForPath<TMember>(Expression<Func<TDestination,TMember>> path, Action<...> opt)` | Nested yol (`d.Customer.Name`) yapılandırır. |
| `ForCtorParam(string ctorParamName, Action<ICtorParamConfigurationExpression<TSource>> opt)` | Constructor parametresi kaynağı (record/immutable). |
| `ForSourceMember(Expression<Func<TSource,object?>> srcMember, Action<ISourceMemberConfigurationExpression> opt)` | Kaynak üye (doğrulama davranışı). |
| `ForAllMembers(Action<IForAllMembersExpression<...>> opt)` | Tüm üyelere toplu kural. |
| `ForAllOtherMembers(Action<IMemberConfigurationExpression<TSource,TDestination,object>> opt)` | Açıkça yapılandırılmamış üyelere toplu kural. |
| `Ignore(Expression<Func<TDestination,object?>> destMember)` | Hedef üyeyi yok say (kısayol). |

```csharp
CreateMap<Order, OrderDto>()
    .ForMember(d => d.Total, o => o.MapFrom(s => s.Amount + s.Tax))
    .ForMember("Code", o => o.MapFrom(s => s.Reference))
    .ForPath(d => d.Customer.Name, o => o.MapFrom(s => s.CustomerName))
    .ForCtorParam("id", o => o.MapFrom(s => s.ExternalId));
```

## Polimorfizm & kalıtım

| İmza | Açıklama |
| :--- | :--- |
| `Include<TDerivedSource,TDerivedDestination>()` | Türetilmiş çift için polimorfik eşleme. |
| `Include(Type derivedSource, Type derivedDestination)` | Tip nesneleriyle. |
| `IncludeBase<TBaseSource,TBaseDestination>()` | Base çiftin kurallarını miras al. |
| `IncludeAllDerived()` | Tüm derived eşleşmeleri otomatik dahil et. |
| `As<TDestinationRedirect>()` | Sonucu türetilmiş bir hedef tipe yönlendir. |
| `IncludeMembers(params Expression<Func<TSource,object?>>[] members)` | Alt nesnelerin üyelerini düzleştir (ilk eşleşen kazanır). |

## Ters yön & inşa

| İmza | Açıklama |
| :--- | :--- |
| `ReverseMap()` | Ters (Dest→Source) kayıt (unflattening dahil). |
| `ConstructUsing(Func<TSource,TDestination> factory)` | Hedefi factory ile oluştur. |
| `ConstructUsing(Func<TSource,VeloxResolutionContext,TDestination> factory)` | Bağlamlı factory. |

## Tüm-tip dönüştürücü

| İmza | Açıklama |
| :--- | :--- |
| `ConvertUsing(IVeloxTypeConverter<TSource,TDestination> converter)` | Örnek converter. |
| `ConvertUsing(Func<TSource?,TDestination> mappingFunction)` | Lambda converter. |
| `ConvertUsing<TTypeConverter>() where TTypeConverter : IVeloxTypeConverter<TSource,TDestination>` | Tipi DI'dan (veya parametresiz kurucuyla) çöz. |

## Hook'lar (BeforeMap / AfterMap)

| İmza | Açıklama |
| :--- | :--- |
| `BeforeMap(Action<TSource,TDestination>)` / `AfterMap(...)` | Satır içi (2-arg). |
| `BeforeMap(Action<TSource,TDestination,VeloxResolutionContext>)` / `AfterMap(...)` | Bağlamlı (3-arg). |
| `BeforeMap<TAction>()` / `AfterMap<TAction>() where TAction : IVeloxMappingAction<TSource,TDestination>` | DI-destekli. |

## Doğrulama & döngü kontrolü

| İmza | Açıklama |
| :--- | :--- |
| `MaxDepth(int depth)` | Rekürsif derinlik sınırı. |
| `PreserveReferences()` | Döngüsel referanslarda önceki sonucu döndür. |
| `ValidateMemberList(MemberList)` | Doğrulanacak üye listesi. |
| `DisableCtorValidation()` | Constructor doğrulamasını kapat. |
| `IgnoreAllPropertiesWithAnInaccessibleSetter()` | Erişilemez setter'lı hedef üyeleri yok say. |
| `IgnoreAllSourcePropertiesWithAnInaccessibleSetter()` | Erişilemez getter'lı kaynak üyeleri doğrulama dışı bırak. |

## Enum eşleme

`ConvertUsingEnumMapping(Action<EnumMappingExpression<TSource,TDestination>> configure)` — bkz. aşağıdaki `EnumMappingExpression`.

### `EnumMappingExpression<TSource,TDestination>`
`EnumMappingExpression<TSource,TDestination> MapByName()` · `MapValue(TSource sourceValue, TDestination destinationValue)`

```csharp
CreateMap<SourceStatus, DestStatus>()
    .ConvertUsingEnumMapping(opt => opt.MapByName());
```

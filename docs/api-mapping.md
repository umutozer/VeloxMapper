---
title: "API Eşleme Tablosu"
description: "AutoMapper'daki her paket, namespace, tür, metot ve istisnanın VeloxMapper karşılığını; neyin aynı kaldığını ve neyin değiştiğini tek sayfada görün."
section: migration
order: 30
---

# API Eşleme Tablosu

Bu sayfa AutoMapper adlarının VeloxMapper karşılıklarını listeler. **Aynı** olarak işaretli satırlarda kodunuzda değişiklik gerekmez; **Değişir** olarak işaretli satırları [geçiş betiği](./migration-script.md) otomatik olarak günceller.

## Paketler

| AutoMapper | VeloxMapper | Durum |
| --- | --- | --- |
| `AutoMapper` | `VeloxMapper` | Değişir |
| `AutoMapper.Extensions.Microsoft.DependencyInjection` | `VeloxMapper` paketine dahil | Değişir (paketi kaldırın) |
| `AutoMapper.Extensions.EnumMapping` | `VeloxMapper` paketine dahil | Değişir (paketi kaldırın) |
| `AutoMapper.Collection` | Karşılığı yok | Desteklenmez |
| `AutoMapper.Extensions.ExpressionMapping` | Karşılığı yok | Desteklenmez |
| `AutoMapper.EF6`, `AutoMapper.Data`, `AutoMapper.AspNetCore.OData` | Karşılığı yok | Desteklenmez |

## Namespace'ler

| AutoMapper | VeloxMapper | Durum |
| --- | --- | --- |
| `AutoMapper` | `VeloxMapper` | Değişir |
| `AutoMapper.QueryableExtensions` | `VeloxMapper.QueryableExtensions` | Değişir |
| `AutoMapper.Configuration.Annotations` | `VeloxMapper.Configuration.Annotations` | Değişir |
| `AutoMapper.Extensions.EnumMapping` | `VeloxMapper` (ayrı `using` gerekmez) | Değişir (satırı silin) |
| `Microsoft.Extensions.DependencyInjection` (`AddAutoMapper`) | `Microsoft.Extensions.DependencyInjection` (`AddVeloxMapper`) | Aynı |

## Yapılandırma türleri

| AutoMapper | VeloxMapper | Durum |
| --- | --- | --- |
| `MapperConfiguration` | `MapperConfiguration` | Aynı |
| `IConfigurationProvider` | `IConfigurationProvider` | Aynı |
| `MapperConfigurationExpression` | `MapperConfigurationExpression` | Aynı |
| `IMapperConfigurationExpression` | `IMapperConfigurationExpression` | Aynı |
| `Profile` | `Profile` | Aynı |
| `IProfileExpression` | `IProfileExpression` | Aynı |
| `IMappingExpression<TSource, TDestination>`, `IMappingExpression` | Aynı adlar | Aynı |
| `IMemberConfigurationExpression<TSource, TDestination, TMember>`, `IMemberConfigurationExpression` | Aynı adlar | Aynı |
| `ISourceMemberConfigurationExpression` | `ISourceMemberConfigurationExpression` | Aynı |
| `ICtorParamConfigurationExpression<TSource>` | `ICtorParamConfigurationExpression<TSource>` | Aynı |
| `MemberList` | `MemberList` | Aynı |
| `TypeMap`, `PropertyMap` | `TypeMap`, `PropertyMap` | Aynı (bkz. [not](#bilinen-api-farkları)) |
| `PascalCaseNamingConvention`, `LowerUnderscoreNamingConvention`, `ExactMatchNamingConvention` | Aynı adlar | Aynı |
| `cfg.LicenseKey` | `cfg.LicenseKey` | Aynı (kabul edilir, yok sayılır) |

`new MapperConfiguration(cfg => ...)` ve AutoMapper 15'teki `new MapperConfiguration(cfg => ..., loggerFactory)` imzalarının ikisi de vardır.

## Çalışma zamanı türleri

| AutoMapper | VeloxMapper | Durum |
| --- | --- | --- |
| `IMapper` | `IMapper` | Aynı |
| `Mapper` | `Mapper` | Aynı |
| `ResolutionContext` | `ResolutionContext` | Aynı |
| `IMappingOperationOptions`, `IMappingOperationOptions<TSource, TDestination>` | Aynı adlar | Aynı |
| `IValueResolver<TSource, TDestination, TDestMember>` | Aynı ad ve imza | Aynı |
| `IMemberValueResolver<TSource, TDestination, TSourceMember, TDestMember>` | Aynı ad ve imza | Aynı |
| `IValueConverter<TSourceMember, TDestinationMember>` | Aynı ad ve imza | Aynı |
| `ITypeConverter<TSource, TDestination>` | Aynı ad ve imza | Aynı |
| `IMappingAction<TSource, TDestination>` | Aynı ad ve imza | Aynı |

## IMapper metotları

| AutoMapper | VeloxMapper | Durum |
| --- | --- | --- |
| `Map<TDestination>(object source)` | Aynı | Aynı |
| `Map<TDestination>(object source, Action<IMappingOperationOptions<object, TDestination>> opts)` | Aynı | Aynı |
| `Map<TSource, TDestination>(TSource source)` | Aynı | Aynı |
| `Map<TSource, TDestination>(TSource source, Action<...> opts)` | Aynı | Aynı |
| `Map<TSource, TDestination>(TSource source, TDestination destination)` | Aynı; hedefi döndürür | Aynı |
| `Map<TSource, TDestination>(TSource source, TDestination destination, Action<...> opts)` | Aynı | Aynı |
| `Map(object source, Type sourceType, Type destinationType)` | Aynı | Aynı |
| `Map(object source, object destination, Type sourceType, Type destinationType)` | Aynı | Aynı |
| `ProjectTo<TDestination>(IQueryable source, object? parameters, params Expression<...>[] membersToExpand)` | Aynı | Aynı |
| `ConfigurationProvider` | Aynı | Aynı |

## Yapılandırma metotları

Aşağıdaki metotlar adları ve imzalarıyla aynıdır; değişiklik gerekmez.

| Alan | Metotlar |
| --- | --- |
| Profil ve tarama | `CreateMap`, `CreateProjection`, `AddProfile`, `AddProfiles`, `AddMaps`, `CreateProfile` |
| Konvansiyonlar | `RecognizePrefixes`, `RecognizePostfixes`, `RecognizeDestinationPrefixes`, `RecognizeDestinationPostfixes`, `ClearPrefixes`, `ReplaceMemberName`, `AddGlobalIgnore`, `SourceMemberNamingConvention`, `DestinationMemberNamingConvention`, `IncludeSourceExtensionMethods` |
| Global ayarlar | `AllowNullDestinationValues`, `AllowNullCollections`, `EnableNullPropagationForQueryMapping`, `ShouldMapProperty`, `ShouldMapField`, `ShouldMapMethod`, `ShouldUseConstructor`, `DisableConstructorMapping`, `ConstructServicesUsing`, `ValueTransformers` |
| Toplu kurallar | `ForAllMaps`, `ForAllPropertyMaps` |
| Tür eşlemesi | `ForMember`, `ForPath`, `ForSourceMember`, `ForCtorParam`, `ForAllMembers`, `ForAllOtherMembers`, `ReverseMap`, `ConvertUsing`, `ConstructUsing`, `ConstructUsingServiceLocator`, `BeforeMap`, `AfterMap`, `Include`, `IncludeBase`, `IncludeAllDerived`, `IncludeMembers`, `As`, `MaxDepth`, `PreserveReferences`, `ValidateMemberList`, `DisableCtorValidation`, `IgnoreAllPropertiesWithAnInaccessibleSetter`, `IgnoreAllSourcePropertiesWithAnInaccessibleSetter`, `AddTransform`, `ConvertUsingEnumMapping` |
| Üye seçenekleri | `MapFrom` (ifade, string yol, fonksiyon, resolver), `ConvertUsing` (value converter), `Ignore`, `DoNotValidate`, `Condition`, `PreCondition`, `NullSubstitute`, `UseDestinationValue`, `DoNotUseDestinationValue`, `SetMappingOrder`, `AddTransform`, `ExplicitExpansion`, `AllowNull`, `DoNotAllowNull`, `MapAtRuntime` |
| Doğrulama ve derleme | `AssertConfigurationIsValid()`, `AssertConfigurationIsValid<TProfile>()`, `AssertConfigurationIsValid(string profileName)`, `CompileMappings()`, `BuildExecutionPlan(Type, Type)`, `CreateMapper()`, `CreateMapper(Func<Type, object>)` |

## ProjectTo

| AutoMapper | VeloxMapper | Durum |
| --- | --- | --- |
| `using AutoMapper.QueryableExtensions;` | `using VeloxMapper.QueryableExtensions;` | Değişir |
| `query.ProjectTo<TDest>(IConfigurationProvider, params Expression<...>[])` | Aynı | Aynı |
| `query.ProjectTo<TDest>(IConfigurationProvider, object parameters, params Expression<...>[])` | Aynı | Aynı |
| `query.ProjectTo<TDest>(IConfigurationProvider, IDictionary<string, object>, params string[])` | Aynı | Aynı |
| `query.ProjectTo(Type, IConfigurationProvider)` | Aynı | Aynı |
| `query.ProjectTo(Type, IConfigurationProvider, IDictionary<string, object>, params string[])` | Aynı | Aynı |

## Dependency injection

| AutoMapper | VeloxMapper | Durum |
| --- | --- | --- |
| `services.AddAutoMapper(typeof(Program))` | `services.AddVeloxMapper(typeof(Program))` | Değişir (eski ad da derlenir) |
| `services.AddAutoMapper(cfg => ..., typeof(Program))` | `services.AddVeloxMapper(cfg => ..., typeof(Program))` | Değişir (eski ad da derlenir) |
| `services.AddAutoMapper((sp, cfg) => ..., assemblies)` | `services.AddVeloxMapper((sp, cfg) => ..., assemblies)` | Değişir (eski ad da derlenir) |
| `IMapper` transient, `IConfigurationProvider` singleton | Aynı ömürler | Aynı |

## İstisnalar

| AutoMapper | VeloxMapper | Durum |
| --- | --- | --- |
| `AutoMapperMappingException` | `VeloxMapper.Exceptions.VeloxMappingException` | Değişir |
| `AutoMapperConfigurationException` | `VeloxMapper.Exceptions.VeloxValidationException` | Değişir |
| `DuplicateTypeMapConfigurationException` | `VeloxMapper.Exceptions.VeloxConfigurationException` | Değişir |
| — | `VeloxMapper.Exceptions.VeloxAmbiguousConstructorException` | Yeni (hedef tür için kurucu seçilemedi) |
| — | `VeloxMapper.Exceptions.VeloxProjectionException` | Yeni (`ProjectTo` ifadesi üretilemedi) |
| — | `VeloxMapper.Exceptions.VeloxException` | Yeni (tüm VeloxMapper istisnalarının tabanı) |

İstisna mesajları Türkçedir ve tür/üye bilgisi içerir. Ayrıntılar için [Davranış Farkları](./behavior-differences.md#değişen-istisna-türleri-ve-mesajlar) sayfasına bakın.

## Öznitelikler

| AutoMapper | VeloxMapper | Durum |
| --- | --- | --- |
| `[AutoMap(typeof(TSource))]` | `[AutoMap(typeof(TSource))]` (`VeloxMapper` namespace) | Aynı |
| `[Ignore]` | `[Ignore]` | Aynı (namespace değişir) |
| `[SourceMember]` | `[SourceMember]` | Aynı (namespace değişir) |
| `[NullSubstitute]` | `[NullSubstitute]` | Aynı (namespace değişir) |
| `[ValueResolver]` | `[ValueResolver]` | Aynı (namespace değişir) |
| `[ValueConverter]` | `[ValueConverter]` | Aynı (namespace değişir) |
| `[UseExistingValue]` | `[UseExistingValue]` | Aynı (namespace değişir) |
| `[MappingOrder]` | `[MappingOrder]` | Aynı (namespace değişir) |
| `[MapAtRuntime]` | `[MapAtRuntime]` | Aynı (namespace değişir) |

Üye öznitelikleri `VeloxMapper.Configuration.Annotations` namespace'indedir.

## Enum eşleme eklentisi

| AutoMapper.Extensions.EnumMapping | VeloxMapper | Durum |
| --- | --- | --- |
| `ConvertUsingEnumMapping(opt => opt.MapByName())` | Aynı | Aynı |
| `ConvertUsingEnumMapping(opt => opt.MapByValue())` | Aynı | Aynı |
| `.MapValue(Source.A, Destination.B)` | Aynı | Aynı |

## VeloxMapper'a özgü API'ler

Bu API'lerin AutoMapper karşılığı yoktur. Geçiş için gerekli değildir; isteğe bağlı olarak kullanabilirsiniz.

| API | Namespace | Açıklama |
| --- | --- | --- |
| `cfg.PatchMapping.IgnoreNullValues` | `VeloxMapper` | `Map(source, destination)` sırasında `null` kaynak değerlerini atlar. Bkz. [Mevcut Nesneye Eşleme](./map-to-existing.md). |
| `[VeloxMap(typeof(TSource), typeof(TDestination))]` | `VeloxMapper.Attributes` | Derleme zamanı Source Generator ile eşleme kodu üretir. |
| `configuration.RegisterPrecompiledMapper<TSource, TDestination>(...)` | `VeloxMapper` | Üretilen kodu `IMapper.Map` çağrılarına bağlar. |
| `[VeloxConstructor]` | `VeloxMapper.Attributes` | Hedef türde kullanılacak kurucuyu açıkça seçer. Bkz. [Constructor ve Record](./constructors.md). |
| `configuration.GetMappingPlan(typeof(A), typeof(B))` | `VeloxMapper` | Üye bazında eşleme planı (teşhis). |
| `MappingPlanReport.GenerateTextReport / GenerateJsonReport / GenerateHash` | `VeloxMapper.Diagnostics` | Eşleme planını CI snapshot testleri için metne veya JSON'a çevirir. |
| `cfg.DiagnosticsSink` (`IVeloxDiagnosticsSink`) | `VeloxMapper.Abstractions` | Teşhis olaylarını kendi hedefinize yönlendirir. |
| `cfg.NamingConvention` | `VeloxMapper` | Kaynak ve hedef için tek isimlendirme kuralı. |
| `cfg.AddCustomConverter<TSource, TDestination>(converter)` | `VeloxMapper` | Bir tür dönüştürücüyü örnek olarak kaydeder. |
| `cfg.AddProfilesFromAssembly`, `AddProfilesFromAssemblyOf<T>`, `AddProfilesFromAssemblies` | `VeloxMapper` | `AddMaps` ile eşdeğer tarama yardımcıları. |
| `.Ignore(d => d.Member)` | `VeloxMapper` | `ForMember(d => d.Member, o => o.Ignore())` kısaltması. |
| `query.ProjectTo<TDest>(IMapper mapper, ...)` | `VeloxMapper.QueryableExtensions` | `mapper.ConfigurationProvider` yazmadan projeksiyon. |
| `VeloxVersion.Current` | `VeloxMapper.Diagnostics` | Yüklü kütüphane sürümü. |

## VeloxMapper 5.x takma adları

VeloxMapper 5.x ile yazılmış kod için şu adlar korunur: `IVeloxMapper` (`IMapper` ile eşdeğer), `VeloxProfile` (`Profile` sınıfının tabanı), `VeloxMapperOptions` (yapılandırma delegesinin somut türü), `VeloxResolutionContext`, `IVeloxValueResolver`, `IVeloxMemberValueResolver`, `IVeloxValueConverter`, `IVeloxTypeConverter`, `IVeloxMappingAction` (son altısı `VeloxMapper.Abstractions` namespace'inde). Yeni kodda AutoMapper adlarını kullanın.

## Bilinen API farkları

- `TypeMap` ve `PropertyMap` yalnızca temel bilgileri sunar (`TypeMap.SourceType`, `DestinationType`, `ProfileName`; `PropertyMap.DestinationName`, `DestinationType`, `SourceMember`, `SourceType`, `TypeMap`). `ForAllMaps` / `ForAllPropertyMaps` içinde AutoMapper'ın iç `TypeMap` özelliklerini (ör. `PropertyMaps`) kullanan kod derlenmez.
- Özel isimlendirme kuralları `VeloxMapper.Abstractions.ICustomNamingConvention` arayüzünü uygular; AutoMapper'ın `INamingConvention` arayüzü yoktur.
- `ResolutionContext.Mapper` `IMapper` türündedir; `IRuntimeMapper` türü yoktur.
- `cfg.Internal()`, `UseAsDataSource`, `EqualityComparison` ve `IMappingExpression.AsProxy()` yoktur. `[AutoMap(..., AsProxy = true)]` derlenir ancak proxy üretmez.

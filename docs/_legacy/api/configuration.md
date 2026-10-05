---
id: configuration
title: Yapılandırma API'si
sidebar_label: Configuration & Profile
description: MapperConfiguration, VeloxMapperOptions ve VeloxProfile API referansı.
---

# Yapılandırma

## `MapperConfiguration` (sealed)
Namespace: `VeloxMapper.Configuration` · `IConfigurationProvider` uygular.

**Kurucular**
```csharp
MapperConfiguration(Action<VeloxMapperOptions> configure)
MapperConfiguration(Assembly[] assemblies)   // assembly tarama kısayolu
```

**Metotlar** (`IConfigurationProvider` üyelerine ek olarak)
| İmza | Açıklama |
| :--- | :--- |
| `MappingPlanDef GetMappingPlan(Type source, Type destination, MappingMode mode = MappingMode.Map)` | Eşleme planını çıkarır (gözlemlenebilirlik). |
| `void RegisterPrecompiledMapper<TSource,TDestination>(Func<TSource,TDestination> mapFunc)` | Source-generated (Layer 1) delege kaydeder. |

**Salt-okunur özellikler:** `Prefixes`, `DestinationPrefixes`, `Postfixes`, `DestinationPostfixes`, `GlobalIgnores`, `AllowNullCollections`, `NamingConvention`, `Source/DestinationMemberNamingConvention`, `ShouldMapProperty`, `ShouldMapField`, `RegistrationCount`.

## `VeloxMapperOptions` (config builder — `cfg`)
Namespace: `VeloxMapper.Configuration`. `new MapperConfiguration(cfg => { ... })` içindeki `cfg`.

### Eşleme tanımı
```csharp
IMappingExpression<TSource,TDestination> CreateMap<TSource,TDestination>()
IMappingExpression<TSource,TDestination> CreateMap<TSource,TDestination>(MemberList memberList)
IMappingExpression CreateMap(Type sourceType, Type destinationType)     // open generics
IMappingExpression<TSource,TDestination> CreateProjection<TSource,TDestination>()
IMappingExpression<TSource,TDestination> CreateProjection<TSource,TDestination>(MemberList memberList)
```

### Profil / assembly ekleme
```csharp
void AddProfile<TProfile>() where TProfile : VeloxProfile, new()
void AddProfile(VeloxProfile profile)
void AddProfile(Type profileType)
void AddProfiles(IEnumerable<VeloxProfile> profiles)
void AddProfilesFromAssembly(Assembly assembly)
void AddProfilesFromAssemblyOf<T>()
void AddProfilesFromAssemblies(params Assembly[] assemblies)
void AddMaps(params Assembly[] assembliesToScan)
void AddMaps(params Type[] typesFromAssembliesContainingMaps)
void AddMaps(IEnumerable<string> assemblyNamesToScan)
void AddMaps(string assemblyNameToScan)
```

### İsimlendirme & filtreleme
```csharp
void RecognizePrefixes(params string[] prefixes)
void RecognizeDestinationPrefixes(params string[] prefixes)
void RecognizePostfixes(params string[] postfixes)
void RecognizeDestinationPostfixes(params string[] postfixes)
void ClearPrefixes()
void AddGlobalIgnore(string propertyName)          // + params string[] overload
void ForAllMaps(Action<MappingRegistration> configure)
void AddCustomConverter<TSource,TDestination>(IVeloxTypeConverter<TSource,TDestination> converter)
```

### Özellikler
| Özellik | Tip | Açıklama |
| :--- | :--- | :--- |
| `NamingConvention` | `ICustomNamingConvention?` | Genel isimlendirme kuralı |
| `SourceMemberNamingConvention` | `ICustomNamingConvention?` | Kaynak tarafı |
| `DestinationMemberNamingConvention` | `ICustomNamingConvention?` | Hedef tarafı |
| `AllowNullCollections` | `bool` | Null koleksiyonları boş yerine null map et |
| `AllowNullDestinationValues` | `bool` (varsayılan `true`) | AutoMapper uyumluluğu |
| `ShouldMapProperty` | `Func<PropertyInfo,bool>` | Global property filtresi |
| `ShouldMapField` | `Func<FieldInfo,bool>` | Global field filtresi (varsayılan: false) |
| `PatchMapping` | `PatchMappingOptions` | `IgnoreNullValues` içerir |
| `ValueTransformers` | `ValueTransformerCollection` | Global değer transformerları |
| `DiagnosticsSink` | `IVeloxDiagnosticsSink?` | Loglama havuzu |

## `VeloxProfile` (abstract)
Namespace: `VeloxMapper`. `AutoMapper.Profile` karşılığı. Protected metotlar:
```csharp
protected IMappingExpression<TSource,TDestination> CreateMap<TSource,TDestination>()
protected IMappingExpression<TSource,TDestination> CreateMap<TSource,TDestination>(MemberList memberList)
protected IMappingExpression CreateMap(Type sourceType, Type destinationType)
protected IMappingExpression<TSource,TDestination> CreateProjection<TSource,TDestination>()
protected IMappingExpression<TSource,TDestination> CreateProjection<TSource,TDestination>(MemberList memberList)
protected ValueTransformerCollection ValueTransformers { get; }
```

## `MemberList` (enum)
`Destination = 0`, `Source = 1`, `None = 2`. AutoMapper ile aynı sayısal değerler. `MemberList.None` bu eşleme için doğrulamayı atlar.

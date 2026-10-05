---
id: analysis-gaps
title: Dokümantasyon Analizi & Eksikler Raporu
sidebar_label: Analiz & Eksikler
description: VeloxMapper kod tabanı ve mevcut dokümantasyonun analizi, tespit edilen eksikler.
---

# Dokümantasyon Analizi & Eksikler Raporu

Bu rapor, `src/` altındaki gerçek public API ile mevcut website dokümanları (`website/src/app/docs/docsData.ts`) karşılaştırılarak hazırlanmıştır.

## 1. Public API Envanteri (özet)

| Alan | Tür / Arayüz | Durum |
| :--- | :--- | :--- |
| Eşleme | `IVeloxMapper` (7 Map overload + `ProjectTo` + `ConfigurationProvider`) | Kod mevcut |
| Yapılandırma | `IConfigurationProvider` (`AssertConfigurationIsValid`, `CompileMappings`, `CreateMapper`) | Kod mevcut |
| Config builder | `VeloxMapperOptions` (CreateMap, CreateProjection, AddProfile*, AddMaps, Recognize*, naming, AllowNull*, ShouldMap*) | Kod mevcut |
| Frozen config | `MapperConfiguration` (Assert, Compile, CreateMapper, GetMappingPlan, RegisterPrecompiledMapper) | Kod mevcut |
| Profil | `VeloxProfile` (protected CreateMap / CreateProjection) | Kod mevcut |
| Zincir | `IMappingExpression<TSource,TDestination>` (ForMember, ForPath, ForCtorParam, ForSourceMember, Include(*), IncludeBase, IncludeAllDerived, IncludeMembers, As, ReverseMap, ConstructUsing×2, ConvertUsing×3, MaxDepth, PreserveReferences, DisableCtorValidation, ValidateMemberList, IgnoreAll*, BeforeMap×3, AfterMap×3, ConvertUsingEnumMapping) | Kod mevcut |
| Üye | `IMemberConfigurationExpression` (MapFrom×8, ConvertUsing×2, Condition×5, PreCondition×3, NullSubstitute, Ignore, DoNotValidate, Use/DoNotUseDestinationValue, SetMappingOrder, MapAtRuntime, ExplicitExpansion, AllowNull/DoNotAllowNull) | Kod mevcut |
| Arayüzler | `IVeloxValueResolver`, `IVeloxMemberValueResolver`, `IVeloxValueConverter`, `IVeloxTypeConverter`, `IVeloxMappingAction`, `ICustomNamingConvention` | Kod mevcut |
| Öznitelikler | `[VeloxMap]` (source-gen), `[VeloxConstructor]`, `[AutoMap]` (runtime) | Kod mevcut |
| Naming | `PascalCaseNamingConvention`, `LowerUnderscoreNamingConvention`, `ExactMatchNamingConvention` | Kod mevcut |
| Extension | `AddVeloxMapper` (×2), `IQueryable.ProjectTo` | Kod mevcut |
| Diagnostics | `MappingPlanReport` (Json/Text/Hash), `MappingPlanDef`, `VeloxVersion` | Kod mevcut |
| Enum | `EnumMappingExpression` (MapByName, MapValue) | Kod mevcut |
| Context | `VeloxResolutionContext` (Items, Mapper, ServiceProvider, CurrentMember) | Kod mevcut |
| İstisnalar | `VeloxException`, `VeloxMappingException`, `VeloxConfigurationException`, `VeloxValidationException`, `VeloxProjectionException`, `VeloxAmbiguousConstructorException` | Kod mevcut |

## 2. Tespit Edilen Eksikler (mevcut website docs'a göre)

### Eksik veya zayıf API açıklamaları
- **Formal API referansı yoktu.** Mevcut docs tutorial tarzıydı; her public metot için imza + parametre + dönüş tipi + örnek içeren bir referans bölümü **eksikti** → bu docs setinde `api/` altında eklendi.
- `RegisterPrecompiledMapper`, `GetMappingPlan`, `VeloxResolutionContext` üyeleri (Items/Mapper/ServiceProvider) formal olarak belgelenmemişti.
- `ShouldMapProperty` / `ShouldMapField` global filtreleri, `AddGlobalIgnore`, `ForAllMaps` yüzeysel geçiyordu.

### Eksik kullanım senaryoları / örnekler
- **Uçtan uca senaryolar** (DTO↔Entity, API response shaping, complex/nested graph) tek bir yerde toplu değildi → `scenarios.md` eklendi.
- **Troubleshooting** (gerçek istisna tipleri + çözümleri) yoktu → `troubleshooting.md` eklendi.
- **Best Practices** (Clean Architecture entegrasyonu, profil organizasyonu) yoktu → `best-practices.md` eklendi.

### Developer Experience (DX) problemleri
- Beginner → Advanced öğrenme akışı website'ta kategori bazlıydı ama **doğrusal bir "öğren" yolu** (Landing → Getting Started → Core → Advanced) net değildi → bu docs seti bu akışı sunar.
- Migration rehberi vardı ancak formal **API referansı ile çapraz bağlantı** eksikti.

## 3. Sonuç

Kod tarafı **eksiksiz** (100% AutoMapper dokümante-metot paritesi). Dokümantasyon tarafındaki asıl eksik **formal API referansı** ve **doğrusal öğrenme akışı**ydı; bu docs seti ikisini de kapatır. Kodda olmayan hiçbir özellik uydurulmamıştır.

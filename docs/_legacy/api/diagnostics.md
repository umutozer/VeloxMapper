---
id: diagnostics
title: Tanılama & Gözlemlenebilirlik
sidebar_label: Diagnostics
description: MappingPlanReport, MappingPlanDef ve VeloxVersion API referansı.
---

# Tanılama (Diagnostics)

Namespace: `VeloxMapper.Diagnostics`. Eşleme planlarını gözlemlenebilir kılar ve CI/CD snapshot doğrulaması için deterministik hash üretir.

## `VeloxVersion` (static)
| Üye | Değer | Açıklama |
| :--- | :--- | :--- |
| `const string Current` | `"5.4.0"` | Kütüphane sürümü (SemVer 2.0), NuGet ile senkron. |
| `const string PlanSchemaVersion` | `"1.0"` | Plan şeması sürümü; şema değişince artırılır. |

## `MappingPlanDef` (sealed)
Bir kaynak → hedef eşleme planı.

| Özellik | Tip | Açıklama |
| :--- | :--- | :--- |
| `VeloxMapperVersion` | `string` | Varsayılan `VeloxVersion.Current`. |
| `PlanSchemaVersion` | `string` | Varsayılan `VeloxVersion.PlanSchemaVersion`. |
| `ProfileName` | `string` | Profil adı (opsiyonel). |
| `SourceType` | `string` | Kaynak türün tam adı. |
| `DestinationType` | `string` | Hedef türün tam adı. |
| `Mode` | `MappingMode` | `Map`, `Patch` veya `ProjectTo`. |
| `Properties` | `MappedPropertyDesc[]` | Eşleştirilen özellikler. |

### `MappedPropertyDesc` (sealed)
`SourceProperty` · `TargetProperty` · `ExecutionType` (örn. `"Assigned"`, `"Complex"`, `"Ignored"`) — hepsi `string`.

Plan, `MapperConfiguration.GetMappingPlan(source, destination, mode)` ile elde edilir.

## `MappingPlanReport` (static)

| İmza | Açıklama |
| :--- | :--- |
| `string GenerateJsonReport(MappingPlanDef plan)` | camelCase, girintili JSON (makine tüketimi). |
| `string GenerateTextReport(MappingPlanDef plan)` | Okunabilir metin; `[META]`/`[INFO]`/`[DETAIL]` satırları. |
| `string GenerateHash(MappingPlanDef plan)` | Deterministik SHA-256 (küçük harf hex). |

> **Hash davranışı:** hash bileşenleri `PlanSchemaVersion`, `SourceType`, `DestinationType`, `Mode` ve **sıralı** property eşleştirmeleridir. `VeloxMapperVersion` hash'e **dahil değildir** — minor/patch güncellemeleri snapshot'ı kırmaz; yalnızca eşleme mantığı değişince kırılır.

```csharp
var config = (MapperConfiguration)mapper.ConfigurationProvider;
var plan = config.GetMappingPlan(typeof(User), typeof(UserDto));

string text = MappingPlanReport.GenerateTextReport(plan);
string json = MappingPlanReport.GenerateJsonReport(plan);
string hash = MappingPlanReport.GenerateHash(plan); // CI snapshot testi
```

## `IVeloxDiagnosticsSink`
Namespace: `VeloxMapper.Abstractions`. `VeloxMapperOptions.DiagnosticsSink` ile bağlanır. Bkz. [Interfaces](interfaces.md).

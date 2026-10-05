---
id: attributes
title: Öznitelikler (Attributes)
sidebar_label: Attributes
description: VeloxMap, VeloxConstructor ve AutoMap özniteliklerinin API referansı.
---

# Öznitelikler

Namespace: `VeloxMapper.Attributes`.

## `[VeloxMap(Type sourceType, Type destinationType)]`
Source Generator (Layer 1) tarafından analiz edilerek **derleme zamanında** NativeAOT uyumlu eşleme kodu üretir.

- `[AttributeUsage(Assembly | Class, AllowMultiple = true, Inherited = false)]`
- `Type SourceType { get; }` · `Type DestinationType { get; }`

```csharp
[assembly: VeloxMap(typeof(User), typeof(UserDto))]

[VeloxMap(typeof(Order), typeof(OrderDto))]
public partial class MappingRegistry { }
```

## `[VeloxConstructor]`
Hedef tür oluşturulurken kullanılacak kurucuyu belirler. Birden fazla kurucu varsa işaretli olana öncelik verilir.

- `[AttributeUsage(Constructor, AllowMultiple = false, Inherited = false)]`
- Parametresizdir.

```csharp
public class Person
{
    [VeloxConstructor]
    public Person(string name) { /* ... */ }
    public Person() { }
}
```

## `[AutoMap(Type sourceType)]`
Bir **hedef tür** üzerine konur; assembly taraması (`AddVeloxMapper` / `AddProfilesFromAssembly` / `AddMaps`) sırasında **çalışma zamanı** `CreateMap(sourceType, destType)` kaydı üretir. AutoMapper `[AutoMap(typeof(Source))]` ile uyumludur.

- `[AttributeUsage(Class | Struct | Interface, AllowMultiple = true, Inherited = false)]`

| Üye | Tip | Açıklama |
| :--- | :--- | :--- |
| `SourceType` | `Type` (get) | Kaynak tür (kurucudan). |
| `ReverseMap` | `bool` | Ters eşleme de oluştur. |
| `MaxDepth` | `int` | Rekürsif derinlik sınırı (0 = sınırsız). |
| `PreserveReferences` | `bool` | Döngüsel referans koruması. |
| `DisableCtorValidation` | `bool` | Constructor doğrulamasını kapat. |
| `IncludeAllDerived` | `bool` | Türetilmiş tip eşleşmelerini dahil et. |

```csharp
[AutoMap(typeof(User), ReverseMap = true)]
public class UserDto
{
    public string Name { get; set; } = "";
}
```

> **Not:** Derleme zamanı (NativeAOT, Layer 1) için `[VeloxMap]`; çalışma zamanı için `[AutoMap]` kullanılır.

---
id: interfaces
title: Genişletilebilirlik Arayüzleri
sidebar_label: Interfaces
description: Resolver, converter, action ve naming convention arayüzlerinin API referansı.
---

# Genişletilebilirlik Arayüzleri

Namespace: `VeloxMapper.Abstractions` (naming: `VeloxMapper`). Tümü DI-destekli olarak `AddVeloxMapper` ile otomatik Transient kaydedilir.

## `IVeloxValueResolver<in TSource, in TDestination, TDestMember>`
Karmaşık hedef değer çözümü.
```csharp
TDestMember Resolve(TSource source, TDestination destination, TDestMember destMember, VeloxResolutionContext context);
```
```csharp
public class FullNameResolver : IVeloxValueResolver<User, UserDto, string>
{
    public string Resolve(User s, UserDto d, string dest, VeloxResolutionContext ctx)
        => $"{s.FirstName} {s.LastName}";
}
```

## `IVeloxMemberValueResolver<in TSource, in TDestination, in TSourceMember, TDestMember>`
Önceden seçilmiş bir kaynak üye değerini alan resolver.
```csharp
TDestMember Resolve(TSource source, TDestination destination, TSourceMember sourceMember, TDestMember destMember, VeloxResolutionContext context);
```

## `IVeloxValueConverter<in TSourceMember, out TDestMember>`
Hafif, saf üye-üye dönüştürücü (DI gerektirmez).
```csharp
TDestMember Convert(TSourceMember sourceMember, VeloxResolutionContext context);
```

## `IVeloxTypeConverter<in TSource, out TDestination>`
Tüm tip için özel dönüşüm (auto-mapping yerine geçer).
```csharp
TDestination Convert(TSource? source);
```

## `IVeloxMappingAction<in TSource, in TDestination>`
BeforeMap/AfterMap için DI-destekli hook.
```csharp
void Process(TSource source, TDestination destination, VeloxResolutionContext context);
```

## `ICustomNamingConvention`
```csharp
string Normalize(string propertyName);
```
Hazır uygulamalar (namespace `VeloxMapper`, `.Instance` singleton'lı):
- `PascalCaseNamingConvention`
- `LowerUnderscoreNamingConvention`
- `ExactMatchNamingConvention`

## `IVeloxDiagnosticsSink`
```csharp
void Log(string message, string severity = "Information", string? sourceFile = null, int? lineNumber = null);
```

## `VeloxResolutionContext` (sealed, IDisposable)
Namespace: `VeloxMapper.Abstractions`.

| Üye | Tip |
| :--- | :--- |
| `Mapper` | `IVeloxMapper` |
| `ServiceProvider` | `IServiceProvider?` |
| `Items` | `IDictionary<string, object>` |
| `CurrentMember` | `string?` |

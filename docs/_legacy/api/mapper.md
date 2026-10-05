---
id: mapper
title: IVeloxMapper & IConfigurationProvider
sidebar_label: Mapper & Config Provider
description: Eşleme yürütme ve yapılandırma sağlayıcı API referansı.
---

# `IVeloxMapper`

Namespace: `VeloxMapper` · Uygulama: `Mapper` (sealed) · Thread-safe, Singleton.

## Metotlar

### `TDestination Map<TDestination>(object source)`
Kaynak nesneyi `TDestination`'a dönüştürür; **kaynak tür çalışma zamanında** çözülür (proxy unwrapping dahil).
```csharp
UserDto dto = mapper.Map<UserDto>(user);
```

### `TDestination Map<TSource, TDestination>(TSource source)`
Strongly-typed eşleme; en hızlı yol. Yeni hedef döner.
```csharp
UserDto dto = mapper.Map<User, UserDto>(user);
```

### `void Map<TSource, TDestination>(TSource source, TDestination destination)`
**Patch** — kaynak değerlerini var olan hedef örneği üzerine yazar.
```csharp
mapper.Map(updateDto, existingUser);
```

### `TDestination Map<TSource, TDestination>(TSource source, Action<VeloxResolutionContext> contextConfig)`
Bağlam (Items vb.) yapılandırarak strongly-typed eşleme.
```csharp
var dto = mapper.Map<User, UserDto>(user, ctx => ctx.Items["culture"] = "tr");
```

### `void Map<TSource, TDestination>(TSource source, TDestination destination, Action<VeloxResolutionContext> contextConfig)`
Bağlam yapılandırarak **patch**.

### `TDestination Map<TDestination>(object source, Action<VeloxResolutionContext> contextConfig)`
Kaynak türü runtime'da çözerek, bağlamlı eşleme. AutoMapper `mapper.Map<TDest>(src, opt => opt.Items[...])` karşılığı.
```csharp
var dto = mapper.Map<UserDto>(user, ctx => ctx.Items["user"] = current);
```

### `TDestination Map<TSource, TDestination>(TSource source, VeloxResolutionContext context)`
Verilen bağlam nesnesiyle eşleme (iç içe/patch senaryoları — genellikle dahili kullanım).

### `IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source)`
`IQueryable` kaynağını EF Core uyumlu ifade ağacına projekte eder. Bkz. [Extension Metotları](../extension-methods.md).

### `IConfigurationProvider ConfigurationProvider { get; }`
Bu mapper'ın yapılandırma sağlayıcısı (aşağıya bakınız).

## `Mapper` sınıfı — ek üye

### `static Type GetUnproxiedType(Type type)`
Castle / EF Core Lazy-Loading proxy tiplerinin ardındaki gerçek POCO türünü döndürür.

### `Mapper(MapperConfiguration configuration, IServiceProvider? serviceProvider = null)`
Yeni mapper örneği oluşturur.

---

# `IConfigurationProvider`

Namespace: `VeloxMapper` · Uygulama: `MapperConfiguration`. AutoMapper'ın `IConfigurationProvider` karşılığı.

### `void AssertConfigurationIsValid()`
Tüm eşleştirme kurallarını doğrular; eksik/uyumsuz üye varsa **`VeloxValidationException`** fırlatır (fail-fast).

### `void CompileMappings()`
Tüm eşlemeleri başlangıçta önceden derler (warm-up); ilk istekteki gecikmeyi kaldırır.

### `IVeloxMapper CreateMapper()`
Bu yapılandırmadan yeni bir mapper örneği oluşturur (`config.CreateMapper()`).

```csharp
mapper.ConfigurationProvider.AssertConfigurationIsValid();
mapper.ConfigurationProvider.CompileMappings();
```

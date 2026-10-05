---
id: extension-methods
title: Genişletme Metotları
sidebar_label: Extension Methods
description: AddVeloxMapper (DI) ve ProjectTo (IQueryable) genişletme metotları.
---

# Genişletme Metotları

## `AddVeloxMapper` (IServiceCollection)
Namespace: `VeloxMapper.DependencyInjection`.

### `IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<VeloxMapperOptions> configure)`
VeloxMapper'ı yapılandırma delegesi ile **Singleton** kaydeder. Ayrıca profillerdeki tüm resolver, member-value resolver, value converter ve `IVeloxMappingAction` tiplerini **Transient** olarak DI'a otomatik kaydeder.

```csharp
builder.Services.AddVeloxMapper(cfg =>
{
    cfg.AddProfilesFromAssembly(typeof(Program).Assembly);
    cfg.PatchMapping.IgnoreNullValues = true;
});
```

### `IServiceCollection AddVeloxMapper(this IServiceCollection services, params Assembly[] assemblies)`
Assembly tarama kısayolu — verilen assembly'lerdeki tüm `VeloxProfile` alt sınıflarını (ve `[AutoMap]` tiplerini) otomatik bulur.

```csharp
builder.Services.AddVeloxMapper(typeof(Program).Assembly);

builder.Services.AddVeloxMapper(
    typeof(Program).Assembly,
    typeof(SomeProfile).Assembly);
```

Her iki aşırı yükleme de zincirleme için `IServiceCollection` döndürür. Kayıt sonrası `IVeloxMapper` constructor injection ile alınır:

```csharp
public class UserService(IVeloxMapper mapper) { /* ... */ }
```

## `ProjectTo<TDestination>` (IQueryable)
Namespace: `VeloxMapper.Extensions`.

### `IQueryable<TDestination> ProjectTo<TDestination>(this IQueryable source, IVeloxMapper mapper)`
`IQueryable` kaynağını hedef türe projekte eder ve ifade ağacını **EF Core uyumluluğu** için doğrular: `System.Linq.Queryable` / `Enumerable` dışındaki kullanıcı metot çağrıları `VeloxProjectionException` fırlatır (SQL'e çevrilemeyeceği için).

```csharp
using VeloxMapper.Extensions;

var dtos = dbContext.Users
    .Where(u => u.IsActive)
    .ProjectTo<UserDto>(mapper)
    .ToList();
```

> **İpucu:** Metot çağrısı gerektiren dönüşümler için önce `.ToList()` ile belleğe alıp sonra `mapper.Map<...>()` kullanın. `IVeloxMapper.ProjectTo` doğrudan da çağrılabilir; genişletme metodu ek doğrulama katmanı ekler.

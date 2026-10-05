---
id: intro
title: VeloxMapper
sidebar_label: Giriş
slug: /
description: Modern .NET için deterministik, fail-fast, NativeAOT uyumlu hibrit nesne eşleme kütüphanesi.
---

# VeloxMapper

VeloxMapper; modern .NET platformları (**.NET 8.0, .NET 9.0, .NET 10.0**) için tasarlanmış, **deterministik** ve **fail-fast** tabanlı, **NativeAOT** uyumlu hibrit bir nesne eşleme (object mapping) kütüphanesidir.

## Hangi Problemi Çözer?

Geleneksel eşleştiriciler, yapılandırma hatalarını ancak ilgili kod satırı çalıştığında — genellikle **production'da** — fırlatır. Ayrıca yoğun reflection, yüksek bellek tüketimi ve JIT yükü oluşturur.

VeloxMapper üç sütunla çözer:

1. **Fail-Fast doğrulama** — Tüm eşleştirme kuralları uygulama başlangıcında `AssertConfigurationIsValid()` ile doğrulanır. Eşleşmeyen tek bir alan bile varsa startup'ta hata alırsınız.
2. **Hibrit çift katmanlı motor** — Derleme zamanı Source Generator (Layer 1, `[VeloxMap]`) ile çalışma zamanı Expression Tree (Layer 2) birleşir.
3. **Gözlemlenebilirlik** — Eşleştirme planları JSON/metin rapora ve deterministik **SHA-256 hash**'e dönüştürülür; CI/CD'de şema kaymasını yakalar.

## Kısa Kullanım Örneği

```csharp
using VeloxMapper;
using VeloxMapper.Configuration;

var config = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<User, UserDto>()
       .ForMember(dest => dest.FullName,
                  opt => opt.MapFrom(src => src.FirstName + " " + src.LastName));
});

config.AssertConfigurationIsValid();        // fail-fast

IVeloxMapper mapper = config.CreateMapper();
UserDto dto = mapper.Map<User, UserDto>(user);
```

## AutoMapper'dan Geçiş

VeloxMapper'ın metot yüzeyi AutoMapper ile **birebir aynı isim ve imzada** tasarlanmıştır. Çoğu profil yalnızca iki değişiklikle taşınır:

```diff
- using AutoMapper;
+ using VeloxMapper;

- public class UserProfile : Profile
+ public class UserProfile : VeloxProfile
```

Detaylar için [Migration Rehberi](./migration.md).

## Sonraki Adım

➡️ [Getting Started](./getting-started.md) — kurulum ve ilk çalışan örnek.

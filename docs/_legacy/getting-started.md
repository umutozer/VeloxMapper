---
id: getting-started
title: Getting Started
sidebar_label: Getting Started
description: VeloxMapper kurulumu ve ilk çalışan eşleme örneği.
---

# Getting Started

## 1. Kurulum (NuGet)

`VeloxMapper.Core` ve Source Generator tek pakette birleştirilmiştir; yalnızca `VeloxMapper` paketini kurun.

```bash
dotnet add package VeloxMapper
```

```powershell
Install-Package VeloxMapper
```

## 2. Sınıfları Tanımlayın

```csharp
public class User
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int Age { get; set; }
}

public class UserDto
{
    public string FullName { get; set; } = "";
    public int Age { get; set; }
}
```

## 3. Minimum Çalışan Örnek

```csharp
using VeloxMapper;
using VeloxMapper.Configuration;

// 1) Yapılandırma
var config = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<User, UserDto>()
       .ForMember(dest => dest.FullName,
                  opt => opt.MapFrom(src => src.FirstName + " " + src.LastName));
});

// 2) Fail-fast doğrulama (önerilir)
config.AssertConfigurationIsValid();

// 3) Mapper örneği (uygulama boyunca Singleton)
IVeloxMapper mapper = config.CreateMapper();   // veya: new Mapper(config)

// 4) Eşleştir
var user = new User { FirstName = "Deniz", LastName = "Yılmaz", Age = 30 };
UserDto dto = mapper.Map<User, UserDto>(user);

Console.WriteLine($"{dto.FullName} ({dto.Age})"); // Deniz Yılmaz (30)
```

## 4. Dependency Injection ile

ASP.NET Core / Generic Host:

```csharp
using VeloxMapper.DependencyInjection;

// Assembly tarama: tüm VeloxProfile ve [AutoMap] tipleri bulunur
builder.Services.AddVeloxMapper(typeof(Program).Assembly);
```

Kullanım (constructor injection):

```csharp
public class UserService(IVeloxMapper mapper)
{
    public UserDto Get(User user) => mapper.Map<User, UserDto>(user);
}
```

Başlangıçta doğrulama için:

```csharp
app.Services.GetRequiredService<MapperConfiguration>().AssertConfigurationIsValid();
```

➡️ Sonraki: [Core Concepts](./core-concepts.md)

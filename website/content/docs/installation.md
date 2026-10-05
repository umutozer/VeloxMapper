---
title: "Kurulum"
description: "VeloxMapper NuGet paketini projenize ekleyin, desteklenen .NET sürümlerini ve paketin içeriğini öğrenin."
section: getting-started
order: 20
---

# Kurulum

VeloxMapper tek bir NuGet paketi olarak dağıtılır. Çalışma zamanı motoru, Source Generator ve dependency injection uzantıları bu paketin içindedir.

## Paketi ekleyin

.NET CLI ile:

```bash
dotnet add package VeloxMapper --version 6.0.0
```

Proje dosyasına doğrudan eklemek için:

```xml title="Shop.Api.csproj"
<ItemGroup>
  <PackageReference Include="VeloxMapper" Version="6.0.0" />
</ItemGroup>
```

Merkezi paket yönetimi (Central Package Management) kullanıyorsanız sürümü `Directory.Packages.props` dosyasına yazın ve projede sürümsüz referans verin:

```xml title="Directory.Packages.props"
<ItemGroup>
  <PackageVersion Include="VeloxMapper" Version="6.0.0" />
</ItemGroup>
```

```xml title="Shop.Api.csproj"
<ItemGroup>
  <PackageReference Include="VeloxMapper" />
</ItemGroup>
```

## Desteklenen platformlar

| Hedef çerçeve | Destek |
| --- | --- |
| .NET 10 (`net10.0`) | Evet |
| .NET 9 (`net9.0`) | Evet |
| .NET 8 (`net8.0`) | Evet |
| .NET Framework, .NET Standard, .NET 7 ve öncesi | Hayır |

Paketin bağımlılıkları yalnızca `Microsoft.Extensions.DependencyInjection.Abstractions` ve `Microsoft.Extensions.Logging.Abstractions` paketleridir. Bu bağımlılıkların sürümü hedef çerçevenizin ana sürümüyle eşleşir (ör. `net8.0` uygulaması 8.x sürümlerini kullanır); .NET 8 uygulamanız daha yeni bir sürüme zorlanmaz.

## Paketin içeriği

| Bileşen | Açıklama |
| --- | --- |
| Çalışma zamanı motoru | `IMapper`, `MapperConfiguration`, `Profile`, `ProjectTo` ve tüm yapılandırma API'si |
| Source Generator ve analyzer | `[VeloxMap]` için derleme zamanı kod üretimi ve `VM001`/`VM002` kurucu teşhisleri |
| DI uzantıları | `services.AddVeloxMapper(...)` |
| Enum eşleme | `ConvertUsingEnumMapping(...)` (AutoMapper'da ayrı bir eklenti paketiydi) |

AutoMapper'da ayrı paketler olan `AutoMapper.Extensions.Microsoft.DependencyInjection` ve `AutoMapper.Extensions.EnumMapping` karşılıkları VeloxMapper'da yerleşiktir. Bu paketleri kaldırın; VeloxMapper için ek bir paket kurmanız gerekmez.

## Namespace'ler

| Namespace | İçerik |
| --- | --- |
| `VeloxMapper` | `IMapper`, `Mapper`, `MapperConfiguration`, `Profile`, `ResolutionContext`, resolver/converter arayüzleri, `[AutoMap]` |
| `VeloxMapper.QueryableExtensions` | `IQueryable` için `ProjectTo` genişletme metotları |
| `VeloxMapper.Configuration.Annotations` | `[Ignore]`, `[SourceMember]`, `[NullSubstitute]`, `[ValueResolver]`, `[ValueConverter]` ve diğer üye öznitelikleri |
| `VeloxMapper.Exceptions` | `VeloxMappingException`, `VeloxValidationException`, `VeloxConfigurationException` ve diğer istisnalar |
| `VeloxMapper.Attributes` | VeloxMapper'a özgü `[VeloxMap]` ve `[VeloxConstructor]` öznitelikleri |
| `Microsoft.Extensions.DependencyInjection` | `AddVeloxMapper` (ek `using` gerekmez) |

Projede çok sayıda dosya `VeloxMapper` kullanıyorsa global using tanımlayabilirsiniz:

```csharp title="GlobalUsings.cs"
global using VeloxMapper;
```

## Kurulumu doğrulayın

Kurulan sürümü kodda okuyabilirsiniz:

```csharp
using VeloxMapper.Diagnostics;

Console.WriteLine(VeloxVersion.Current); // 6.0.0
```

## AutoMapper'dan geliyorsanız

AutoMapper paketlerini kaldırıp VeloxMapper'ı ekleyin:

```bash
dotnet remove package AutoMapper
dotnet remove package AutoMapper.Extensions.Microsoft.DependencyInjection
dotnet add package VeloxMapper --version 6.0.0
```

Bu adımı ve `using` değişikliklerini elle yapmak yerine [otomatik geçiş betiğini](./migration-script.md) kullanmanızı öneririz. Sürecin tamamı için [Geçiş Rehberi](./migration-guide.md)'ne bakın.

> [!WARNING]
> AutoMapper ve VeloxMapper paketlerini aynı projede birlikte tutmayın. İki paket de `IMapper`, `Profile` ve `AddAutoMapper` gibi aynı adları tanımladığı için derleyici belirsizlik hataları verir.

## Sonraki adım

[Hızlı Başlangıç](./quickstart.md) ile ilk profilinizi tanımlayın ve ilk eşlemenizi çalıştırın.

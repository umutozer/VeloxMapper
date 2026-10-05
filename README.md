# VeloxMapper

[![NuGet](https://img.shields.io/nuget/v/VeloxMapper.svg)](https://www.nuget.org/packages/VeloxMapper)
![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)

**AutoMapper ile aynı API'ye sahip, MIT lisanslı .NET nesne eşleme kütüphanesi.**
Mevcut AutoMapper kodunuz paket ve `using` satırları değiştirilerek derlenir; geçiş betiği bu değişikliği sizin yerinize yapar.

> **In English:** VeloxMapper is an MIT-licensed object mapper for .NET 8/9/10 that mirrors the AutoMapper API
> (`Profile`, `CreateMap`, `ForMember`, `MapFrom`, `IMapper`, `MapperConfiguration`, `ProjectTo`, `AssertConfigurationIsValid`,
> resolvers, converters, `AddAutoMapper` alias). Swap the package and the `using` directives — or run the bundled migration script —
> and your existing mapping code keeps compiling. Docs: https://veloxmapper-website.netlify.app

**Dokümantasyon:** https://veloxmapper-website.netlify.app/docs
**Geçiş rehberi:** https://veloxmapper-website.netlify.app/docs/migration-guide

---

## Neden VeloxMapper?

- **Aynı API, sıfır öğrenme maliyeti.** `Profile`, `CreateMap`, `ForMember`, `MapFrom`, `ReverseMap`, `ForPath`, `IncludeMembers`,
  `IValueResolver`, `ITypeConverter`, `IValueConverter`, `IMappingAction`, `ResolutionContext`, `IMapper`, `MapperConfiguration`,
  `ProjectTo`, `AssertConfigurationIsValid` — isimler ve imzalar AutoMapper ile aynıdır.
- **MIT lisansı.** Ticari kullanım ücretsizdir; lisans anahtarı gerekmez (`cfg.LicenseKey` satırı derlenir ve yok sayılır).
- **Tek paket.** DI uzantıları, enum eşleme, `ProjectTo` ve Source Generator aynı pakettedir.
- **Hızlı başlangıç, güvenli çalışma.** Eşleştirmeler ilk kullanımda Expression Tree olarak derlenir ve `MapperConfiguration` üzerinde
  önbelleklenir; `AssertConfigurationIsValid` tüm hataları tek raporda listeler.
- **EF Core dostu.** `ProjectTo` SQL'e çevrilebilir ifade üretir; `Map(source, destination)` iç nesneleri ve koleksiyonları
  mevcut örneklerine eşleyerek takip edilen varlıkları korur.
- **Gözlemlenebilir.** `GetMappingPlan` ve `MappingPlanReport` ile hangi üyenin nereden geldiğini görebilir, CI'da snapshot testi yazabilirsiniz.

## Kurulum

```bash
dotnet add package VeloxMapper
```

Hedef framework'ler: .NET 8, .NET 9, .NET 10.

## Hızlı başlangıç

```csharp
using VeloxMapper;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.Customer.FirstName + " " + s.Customer.LastName))
            .ForMember(d => d.Items, o => o.MapFrom(s => s.Lines));   // List<OrderLine> → List<OrderLineDto>

        CreateMap<OrderLine, OrderLineDto>().ReverseMap();
    }
}
```

```csharp
// Program.cs
builder.Services.AddVeloxMapper(typeof(Program));   // AddAutoMapper(...) da çalışır
```

```csharp
public class OrderService(IMapper mapper, ShopDbContext db)
{
    public OrderDto Get(Order order) => mapper.Map<OrderDto>(order);

    public Task<List<OrderDto>> ListAsync() =>
        db.Orders.ProjectTo<OrderDto>(mapper.ConfigurationProvider).ToListAsync();   // using VeloxMapper.QueryableExtensions;
}
```

```csharp
[Fact]
public void Mapping_configuration_is_valid()
{
    var config = new MapperConfiguration(cfg => cfg.AddMaps(typeof(OrderProfile).Assembly));
    config.AssertConfigurationIsValid();
}
```

## AutoMapper'dan geçiş

1. Değişikliklerinizi commit edin.
2. Geçiş betiğini indirip çalıştırın (önce `-DryRun` ile). Betik depoda `tools/` altında ve https://veloxmapper-website.netlify.app/migrate-from-automapper.ps1 adresinde bulunur:

   ```powershell
   Invoke-WebRequest https://veloxmapper-website.netlify.app/migrate-from-automapper.ps1 -OutFile migrate-from-automapper.ps1
   pwsh ./migrate-from-automapper.ps1 -Path ./src -DryRun
   pwsh ./migrate-from-automapper.ps1 -Path ./src
   ```

   Betik paket referanslarını, `using` satırlarını, `AddAutoMapper` çağrılarını ve AutoMapper istisna adlarını günceller;
   desteklenmeyen API kullanımlarını raporlar.
3. `dotnet build` ve testlerinizde `config.AssertConfigurationIsValid()`.
4. [Davranış farkları](https://veloxmapper-website.netlify.app/docs/behavior-differences) sayfasını gözden geçirin.

| AutoMapper | VeloxMapper |
| --- | --- |
| `using AutoMapper;` | `using VeloxMapper;` |
| `using AutoMapper.QueryableExtensions;` | `using VeloxMapper.QueryableExtensions;` |
| `services.AddAutoMapper(...)` | `services.AddVeloxMapper(...)` *(takma ad korunur)* |
| `AutoMapperMappingException` | `VeloxMapper.Exceptions.VeloxMappingException` |
| `AutoMapperConfigurationException` | `VeloxMapper.Exceptions.VeloxValidationException` |
| Diğer tüm tür ve metot adları | Aynı |

Doğrulama: AutoMapper 13 ile yazılmış örnek bir uygulama (DI, scoped bağımlılıklı resolver, value converter, EF Core `ProjectTo`,
`ReverseMap`, PATCH deseni, `Items`) betikten sonra kod değişikliği yapılmadan VeloxMapper ile derlendi ve aynı çıktıyı üretti.
Depodaki `tests/VeloxMapper.Tests/Migration` klasörü AutoMapper sözdizimiyle yazılmış parite testlerini içerir.

## VeloxMapper'a özgü özellikler

| Özellik | Kullanım |
| --- | --- |
| Derleme zamanı eşleme (Source Generator) | `[assembly: VeloxMap(typeof(Src), typeof(Dst))]` → üretilen `MapTo…()` extension metodu; `config.RegisterPrecompiledMapper<Src, Dst>(...)` |
| Eşleme planı ve CI snapshot | `config.GetMappingPlan(typeof(A), typeof(B))`, `MappingPlanReport.GenerateHash(plan)` |
| Kısmi güncelleme (PATCH) | `cfg.PatchMapping.IgnoreNullValues = true` |
| Teşhis logları | `cfg.DiagnosticsSink = ...` veya `ILoggerFactory` |
| Açık kurucu seçimi | `[VeloxConstructor]` |

## Proje yapısı

| Klasör | İçerik |
| --- | --- |
| `src/VeloxMapper.Core` | Genel API: arayüzler, yapılandırma ifadeleri, öznitelikler, istisnalar |
| `src/VeloxMapper` | Eşleme motoru, `MapperConfiguration`, `Mapper`, DI ve `ProjectTo` uzantıları (NuGet paketi) |
| `src/VeloxMapper.Generators` | Roslyn Source Generator ve analyzer (pakete gömülüdür) |
| `tests/VeloxMapper.Tests` | Birim, parite ve EF Core entegrasyon testleri |
| `tools/migrate-from-automapper.ps1` | AutoMapper → VeloxMapper geçiş betiği |
| `docs/` | Dokümantasyon kaynağı (web sitesi bu klasörden üretilir) |
| `website/` | Dokümantasyon sitesi (Next.js, statik) |

## Geliştirme

```bash
dotnet build VeloxMapper.slnx
dotnet test tests/VeloxMapper.Tests
dotnet pack src/VeloxMapper/VeloxMapper.csproj -c Release -o artifacts
```

## Lisans

[MIT](LICENSE) © 2026 Umut Özer

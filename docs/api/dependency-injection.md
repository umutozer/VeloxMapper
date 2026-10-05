---
title: "Dependency Injection API"
description: "AddVeloxMapper ve AddAutoMapper overload'ları, kaydedilen servisler, ömürleri ve birden çok çağrının birleştirilmesi."
section: api
order: 90
---

# Dependency Injection API

`AddVeloxMapper` genişletme metotları, VeloxMapper'ı `Microsoft.Extensions.DependencyInjection` kapsayıcısına kaydeder. AutoMapper'ın `AddAutoMapper` overload'larının tamamı aynı imzalarla sunulur. Metotlar `Microsoft.Extensions.DependencyInjection` namespace'indedir; ek `using` gerekmez ve ayrı bir DI paketi kurmanız gerekmez.

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class VeloxMapperServiceCollectionExtensions
```

```csharp title="Program.cs"
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddVeloxMapper(typeof(Program));
```

Kurulum rehberi için [Dependency Injection](../dependency-injection.md) sayfasına bakın.

## Overload'lar

Tüm overload'lar `IServiceCollection` döndürür ve zincirlenebilir.

### Assembly taraması

```csharp
IServiceCollection AddVeloxMapper(this IServiceCollection services, params Type[] profileAssemblyMarkerTypes);
IServiceCollection AddVeloxMapper(this IServiceCollection services, IEnumerable<Type> profileAssemblyMarkerTypes);
IServiceCollection AddVeloxMapper(this IServiceCollection services, params Assembly[] assemblies);
IServiceCollection AddVeloxMapper(this IServiceCollection services, IEnumerable<Assembly> assemblies);
```

Verilen assembly'leri (veya verilen türlerin bulunduğu assembly'leri) tarar:

- Parametresiz kurucusu olan tüm `Profile` alt sınıflarını yapılandırmaya ekler.
- `[AutoMap]` ile işaretli türler için eşleme kaydı oluşturur.
- Genişletme türlerini DI'a kaydeder ([aşağıda](#kaydedilen-servisler)).

```csharp
services.AddVeloxMapper(typeof(OrderProfile), typeof(InvoiceProfile)); // iki farklı assembly
services.AddVeloxMapper(AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName!.StartsWith("MyApp")));
```

### Yapılandırma delegesiyle

```csharp
IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction);
IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, params Assembly[] assemblies);
IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, IEnumerable<Assembly> assemblies);
IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, params Type[] profileAssemblyMarkerTypes);
IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<VeloxMapperOptions> configAction, IEnumerable<Type> profileAssemblyMarkerTypes);
```

Global ayarları ve satır içi eşlemeleri delegede verir, isteğe bağlı olarak assembly de tarar. Delege parametresi `VeloxMapperOptions`'tır (`IMapperConfigurationExpression` uygular).

```csharp
services.AddVeloxMapper(cfg =>
{
    cfg.AllowNullCollections = true;
    cfg.ValueTransformers.Add<string>(s => s.Trim());
    cfg.PatchMapping.IgnoreNullValues = true;
}, typeof(Program));
```

Yalnızca delege alan overload assembly taramaz; profilleri `cfg.AddProfile<T>()` veya `cfg.AddMaps(...)` ile ekleyin. Bu durumda genişletme türleri DI'a otomatik kaydedilmez; çözümlenirken `ActivatorUtilities` ile oluşturulurlar.

### Servis sağlayıcıya erişen delegeyle

```csharp
IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<IServiceProvider, VeloxMapperOptions> configAction, params Assembly[] assemblies);
IServiceCollection AddVeloxMapper(this IServiceCollection services, Action<IServiceProvider, VeloxMapperOptions> configAction, params Type[] profileAssemblyMarkerTypes);
```

Yapılandırma, DI'dan çözülen servislere (ayarlar, `IOptions<T>`, saat) bağlıysa bu overload'u kullanın. Delege, `MapperConfiguration` ilk kez çözüldüğünde kök servis sağlayıcıyla bir kez çalışır.

```csharp
services.Configure<MappingSettings>(builder.Configuration.GetSection("Mapping"));

services.AddVeloxMapper((sp, cfg) =>
{
    var settings = sp.GetRequiredService<IOptions<MappingSettings>>().Value;
    cfg.AddProfile(new PricingProfile(settings.DefaultCurrency));
}, typeof(Program));
```

> [!NOTE]
> Delege kök sağlayıcıyla çalıştığı için buradan scoped servis (ör. `DbContext`) çözmeyin. Çağrıya özel scoped bağımlılıkları resolver'ların kurucusunda alın.

## AddAutoMapper takma adları

```csharp
[EditorBrowsable(EditorBrowsableState.Never)]
IServiceCollection AddAutoMapper(this IServiceCollection services, ...);
```

Yukarıdaki her `AddVeloxMapper` overload'unun aynı parametrelerle bir `AddAutoMapper` karşılığı vardır. Geçiş sırasında mevcut kodun değişmeden derlenmesi içindir; IntelliSense'te gizlidir. Yeni kodda `AddVeloxMapper` kullanın.

<!-- tabs -->
```csharp title="AutoMapper"
using AutoMapper;

builder.Services.AddAutoMapper(typeof(Program));
```
```csharp title="VeloxMapper"
using VeloxMapper;

builder.Services.AddVeloxMapper(typeof(Program));
```
<!-- /tabs -->

## Kaydedilen servisler

| Servis | Ömür | Not |
| --- | --- | --- |
| `MapperConfiguration` | Singleton | Derlenmiş eşlemelerin önbelleği. `GetMappingPlan`, `RegisterPrecompiledMapper` için bu türü çözün. |
| `IConfigurationProvider` | Singleton | Aynı `MapperConfiguration` örneği. |
| `IMapper` | Transient | Bulunduğu scope'un `IServiceProvider`'ı ile oluşturulur. |
| `IVeloxMapper` | Transient | 5.x uyumluluğu; `IMapper` ile aynı davranış. |
| Taranan assembly'lerdeki `IValueResolver<,,>`, `IMemberValueResolver<,,,>`, `IValueConverter<,>`, `ITypeConverter<,>`, `IMappingAction<,>` uygulamaları (ve 5.x `IVelox*` karşılıkları) | Transient | Somut tür olarak kaydedilir (`services.TryAddTransient(typeof(TenantResolver))`). |

Kayıtlar `TryAdd` ile yapılır; aynı servisi daha önce kendiniz kaydettiyseniz sizin kaydınız korunur. Örneğin bir resolver'ı farklı bir ömürle kullanmak istiyorsanız `AddVeloxMapper`'dan önce kaydedin.

`MapperConfiguration` ilk kez çözüldüğünde oluşturulur. Yapılandırma hataları (ör. aynı tür çiftinin iki kez tanımlanması) bu anda fırlatılır. Uygulama başlarken hata almak ve eşlemeleri ısıtmak için:

```csharp
var app = builder.Build();
app.Services.GetRequiredService<IConfigurationProvider>().CompileMappings();
```

### Neden IMapper transient

`IMapper` yalnızca yapılandırmaya ve servis sağlayıcıya referans tutar; oluşturmak ucuzdur. Transient kayıt sayesinde bir controller veya servis içinde çözülen mapper, o isteğin scope'una bağlı servis sağlayıcıyı kullanır. Böylece resolver'ların kurucusunda istenen scoped servisler (`DbContext`, `IHttpContextAccessor` üzerinden kullanıcı bilgisi, kiracı bağlamı) doğru scope'tan gelir ve `ValidateScopes` etkinken hata oluşmaz.

```csharp
public sealed class TenantResolver(ITenantContext tenant) : IValueResolver<Order, OrderDto, string>
{
    public string Resolve(Order source, OrderDto destination, string destMember, ResolutionContext context)
        => tenant.Name;
}

builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddVeloxMapper(typeof(Program)); // TenantResolver otomatik kaydedilir
```

Kayıtlı olmayan bir resolver türü istendiğinde VeloxMapper onu `ActivatorUtilities.CreateInstance` ile oluşturur; kurucu parametreleri yine scope'tan çözülür.

## Birden çok çağrı

`AddVeloxMapper` birden çok kez çağrılabilir. Tüm çağrılardaki yapılandırma delegeleri ve assembly'ler **tek bir** `MapperConfiguration`'da birleşir. Modüler uygulamalarda her modül kendi kaydını yapabilir:

```csharp
// Orders modülü
services.AddVeloxMapper(typeof(OrdersModule));

// Billing modülü
services.AddVeloxMapper(cfg => cfg.AddProfile<InvoiceProfile>(), typeof(BillingModule));
```

- Aynı assembly birden çok çağrıda verilirse bir kez taranır.
- Delegeler çağrılma sırasıyla çalışır; global ayarlar (ör. `AllowNullCollections`) için son atanan değer geçerli olur.
- Aynı tür çifti farklı modüllerde iki kez tanımlanırsa `MapperConfiguration` çözülürken `VeloxConfigurationException` fırlatılır.

## Loglama

DI'da bir `ILoggerFactory` kayıtlıysa (ASP.NET Core ve Generic Host bunu otomatik yapar) `MapperConfiguration` teşhis olaylarını `VeloxMapper` kategorisine yazar. `cfg.DiagnosticsSink` verilmişse logger kullanılmaz. Bkz. [Teşhis](../diagnostics.md#ilogger-entegrasyonu).

## DI olmadan

DI kullanmayan uygulamalarda (konsol araçları, testler) yapılandırmayı kendiniz oluşturun ve tek örnek olarak saklayın:

```csharp
public static class Mapping
{
    public static readonly MapperConfiguration Configuration =
        new(cfg => cfg.AddMaps(typeof(Mapping).Assembly));

    public static IMapper CreateMapper() => Configuration.CreateMapper();
}
```

Resolver'ların bağımlılıkları varsa `Configuration.CreateMapper(type => ...)` ile bir fabrika verin.

## AutoMapper uyumluluğu

`AddVeloxMapper`, AutoMapper.Extensions.Microsoft.DependencyInjection (12.x ve öncesi) ile AutoMapper 13+ içindeki `AddAutoMapper` overload'larının tamamını karşılar; `AddAutoMapper` adı takma ad olarak derlenir. Kaydedilen servisler ve ömürleri (`IMapper` transient, yapılandırma singleton, genişletme türleri transient) AutoMapper ile aynıdır. `IVeloxMapper` kaydı VeloxMapper'a özgüdür.

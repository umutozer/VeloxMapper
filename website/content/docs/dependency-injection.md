---
title: "Dependency Injection"
description: "AddVeloxMapper ile VeloxMapper'ı ASP.NET Core ve Generic Host kapsayıcısına kaydedin; servis ömürlerini, assembly taramayı ve resolver'lara bağımlılık enjeksiyonunu öğrenin."
section: getting-started
order: 40
---

# Dependency Injection

`AddVeloxMapper` genişletme metodu, yapılandırmayı tek seferlik bir singleton olarak oluşturur ve `IMapper` arayüzünü enjekte edilebilir hale getirir. Ayrı bir DI paketi gerekmez; metot `VeloxMapper` paketinin içindedir.

## Temel kayıt

```csharp title="Program.cs"
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddVeloxMapper(typeof(Program));
```

`AddVeloxMapper`, `Microsoft.Extensions.DependencyInjection` namespace'inde tanımlıdır; ek `using` satırı gerekmez. Verdiğiniz türlerin bulunduğu assembly'ler taranır ve şunlar kaydedilir:

- `Profile` sınıfından türeyen tüm sınıflar
- `[AutoMap]` özniteliği taşıyan türler
- `IValueResolver`, `IMemberValueResolver`, `ITypeConverter`, `IValueConverter` ve `IMappingAction` uygulamaları

Ardından `IMapper` arayüzünü istediğiniz yerde enjekte edin:

```csharp title="OrderService.cs"
using VeloxMapper;

public class OrderService(ShopDbContext db, IMapper mapper)
{
    public async Task<OrderDto?> GetAsync(int id, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Lines)
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        return order is null ? null : mapper.Map<OrderDto>(order);
    }
}
```

## AddVeloxMapper overload'ları

| Overload | Kullanım |
| --- | --- |
| `AddVeloxMapper(params Type[] markerTypes)` | Belirtilen türlerin assembly'lerini tarar. En yaygın kullanım. |
| `AddVeloxMapper(IEnumerable<Type> markerTypes)` | Aynısı, koleksiyon olarak. |
| `AddVeloxMapper(params Assembly[] assemblies)` | Assembly'leri doğrudan tarar. |
| `AddVeloxMapper(IEnumerable<Assembly> assemblies)` | Aynısı, koleksiyon olarak. |
| `AddVeloxMapper(Action<VeloxMapperOptions> configAction)` | Yalnızca delege ile yapılandırır; tarama yapmaz. |
| `AddVeloxMapper(Action<VeloxMapperOptions> configAction, params Type[] / IEnumerable<Type>)` | Delege ve tarama birlikte. |
| `AddVeloxMapper(Action<VeloxMapperOptions> configAction, params Assembly[] / IEnumerable<Assembly>)` | Delege ve tarama birlikte. |
| `AddVeloxMapper(Action<IServiceProvider, VeloxMapperOptions> configAction, params Type[])` | Yapılandırma sırasında servislere erişim. |
| `AddVeloxMapper(Action<IServiceProvider, VeloxMapperOptions> configAction, params Assembly[])` | Yapılandırma sırasında servislere erişim. |

Yapılandırma delegesi (`cfg`) AutoMapper'daki `IMapperConfigurationExpression` ile aynı API'yi sunar:

```csharp title="Program.cs"
builder.Services.AddVeloxMapper(cfg =>
{
    cfg.AllowNullCollections = true;
    cfg.AddGlobalIgnore("Audit");
    cfg.CreateMap<Customer, CustomerDto>();
}, typeof(Program));
```

Taranan assembly'deki profiller otomatik eklenir. Aynı profili ayrıca `cfg.AddProfile<T>()` ile eklerseniz profil ikinci kez eklenmez.

### AddAutoMapper takma adı

`AddAutoMapper` aynı overload'larla bir takma ad olarak da vardır. Geçiş sırasında mevcut `services.AddAutoMapper(...)` satırları değişmeden derlenir. Takma ad IntelliSense'te gizlidir; yeni kodda `AddVeloxMapper` kullanın.

## Servis ömürleri

| Servis | Ömür | Not |
| --- | --- | --- |
| `MapperConfiguration` | Singleton | Derlenmiş eşleme delegeleri burada önbelleğe alınır. İlk çözümlemede oluşturulur. |
| `IConfigurationProvider` | Singleton | `MapperConfiguration` ile aynı örnek. |
| `IMapper` | Transient | Hafif bir sarmalayıcıdır; önbelleği `MapperConfiguration` üzerinden paylaşır. |
| `IVeloxMapper` | Transient | VeloxMapper 5.x uyumluluğu için `IMapper` ile eşdeğer. |
| Resolver, converter ve mapping action türleri | Transient | Yalnızca taranan assembly'lerdekiler. |

`IMapper` transient olduğu için singleton, scoped veya transient herhangi bir servise güvenle enjekte edilebilir. Her `IMapper` örneği, kendisini oluşturan kapsamın (scope) `IServiceProvider` örneğini taşır; bu sayede resolver'lar doğru kapsamdan çözülür.

## Resolver'lara bağımlılık enjeksiyonu

Taranan assembly'lerdeki resolver ve converter'lar kurucu enjeksiyonu ile oluşturulur. Scoped bir servis (ör. `DbContext` veya istek bağlamı) resolver'a, `IMapper`'ın çözüldüğü kapsamdan gelir:

```csharp title="CurrentUserResolver.cs"
using VeloxMapper;

public interface ICurrentUser
{
    string UserName { get; }
}

public class OrderAuditDto
{
    public int Id { get; set; }
    public string ViewedBy { get; set; } = "";
}

public class CurrentUserResolver(ICurrentUser currentUser) : IValueResolver<Order, OrderAuditDto, string>
{
    public string Resolve(Order source, OrderAuditDto destination, string destMember, ResolutionContext context)
        => currentUser.UserName;
}
```

```csharp title="OrderProfile.cs"
CreateMap<Order, OrderAuditDto>()
    .ForMember(d => d.ViewedBy, opt => opt.MapFrom<CurrentUserResolver>());
```

```csharp title="Program.cs"
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddVeloxMapper(typeof(Program));
```

Taranan assembly dışındaki bir resolver türü kapsayıcıda kayıtlı değilse VeloxMapper onu `ActivatorUtilities` ile oluşturur; kurucu parametreleri yine kapsayıcıdan çözülür.

## Yapılandırma sırasında servislere erişim

Yapılandırmanın bir servise (ör. `IOptions<T>`) bağlı olduğu durumlarda `(sp, cfg)` biçimindeki overload'u kullanın:

```csharp title="Program.cs"
builder.Services.AddVeloxMapper((sp, cfg) =>
{
    var options = sp.GetRequiredService<IOptions<PricingOptions>>().Value;
    cfg.CreateMap<OrderLine, OrderLineDto>()
        .ForMember(d => d.UnitPrice, opt => opt.MapFrom(s => s.UnitPrice * options.VatMultiplier));
}, typeof(Program));
```

Delege, `MapperConfiguration` ilk kez çözümlendiğinde bir kez çalışır.

## Modüler uygulamalar

`AddVeloxMapper` birden fazla kez çağrılabilir. Tüm çağrılardaki assembly'ler ve yapılandırma delegeleri **tek bir** `MapperConfiguration` içinde birleşir:

```csharp title="Program.cs"
builder.Services.AddVeloxMapper(typeof(OrdersModule));    // Orders modülü
builder.Services.AddVeloxMapper(typeof(CatalogModule));   // Catalog modülü
```

Aynı tür çifti iki farklı yerde `CreateMap` ile tanımlanırsa yapılandırma oluşturulurken `VeloxConfigurationException` fırlatılır. Bir assembly'nin birden fazla kez verilmesi sorun değildir; tekrar taranmaz.

## Loglama

Kapsayıcıda bir `ILoggerFactory` kayıtlıysa (ASP.NET Core'da varsayılan olarak kayıtlıdır) VeloxMapper teşhis mesajlarını `VeloxMapper` kategorisine yazar. Örneğin `ProjectTo` sırasında projeksiyondan çıkarılan bir üye için `Warning` seviyesinde kayıt oluşur. Bu mesajları görmek için log seviyesini ayarlayın:

```json title="appsettings.Development.json"
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "VeloxMapper": "Debug"
    }
  }
}
```

## Başlangıçta doğrulama ve ısıtma

Yapılandırma hatalarını ilk istekte değil, uygulama açılırken yakalamak için `MapperConfiguration` örneğini çözüp doğrulayabilirsiniz. İlk eşleme maliyetini başlangıca almak için `CompileMappings()` çağırın:

```csharp title="Program.cs"
var app = builder.Build();

var mapperConfiguration = app.Services.GetRequiredService<MapperConfiguration>();
if (app.Environment.IsDevelopment())
{
    mapperConfiguration.AssertConfigurationIsValid();
}
mapperConfiguration.CompileMappings();
```

Doğrulamanın asıl yeri birim testleridir (bkz. [Hızlı Başlangıç](./quickstart.md#7-yapılandırmayı-bir-testte-doğrulayın)); başlangıç kontrolü ek bir güvenlik ağıdır.

## DI olmadan kullanım

Konsol uygulamalarında, testlerde veya kapsayıcı kullanmayan kodda yapılandırmayı doğrudan oluşturun. `MapperConfiguration` pahalı bir nesnedir; uygulama boyunca bir kez oluşturup saklayın.

```csharp title="Program.cs"
using VeloxMapper;

var configuration = new MapperConfiguration(cfg =>
{
    cfg.AddProfile<OrderProfile>();
});

IMapper mapper = configuration.CreateMapper();
// veya: IMapper mapper = new Mapper(configuration);
```

Resolver'ların nasıl oluşturulacağını kendiniz belirlemek isterseniz `CreateMapper(Func<Type, object>)` overload'unu veya yapılandırmada `cfg.ConstructServicesUsing(...)` kullanın.

## İlgili sayfalar

- [AddVeloxMapper API referansı](./api/dependency-injection.md)
- [Yapılandırma ve Profiller](./configuration.md)
- [Sorun Giderme: scoped servisler](./troubleshooting.md#resolver-örneği-oluşturulamıyor)

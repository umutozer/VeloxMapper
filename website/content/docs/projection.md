---
title: "ProjectTo & EF Core"
description: "IQueryable sorgularını DTO'lara projekte ederek EF Core'un yalnızca gereken kolonları sorgulamasını sağlayın."
section: querying
order: 10
---

# ProjectTo & EF Core

`ProjectTo`, eşleme yapılandırmanızdan bir `Select` ifadesi üretir ve bunu `IQueryable` sorgusuna ekler. EF Core bu ifadeyi SQL'e çevirir; veritabanından yalnızca DTO'nun ihtiyaç duyduğu kolonlar okunur, varlıklar change tracker'a eklenmez ve `Include` yazmanız gerekmez.

## Map ile farkı

| | `mapper.Map<List<Dto>>(await query.ToListAsync())` | `await query.ProjectTo<Dto>(config).ToListAsync()` |
| --- | --- | --- |
| Veritabanından okunan | Varlığın tüm kolonları | Yalnızca DTO'nun kullandığı kolonlar |
| İlişkili veriler | `Include` gerekir | Gerekli `JOIN`'ler otomatik üretilir |
| Change tracking | Varlıklar izlenir (`AsNoTracking` yoksa) | İzlenmez |
| Desteklenen kurallar | Tüm eşleme özellikleri | SQL'e çevrilebilen kurallar ([aşağıda](#neler-sqle-çevrilir)) |

Okuma amaçlı uç noktalarda (listeleme, detay, raporlama) `ProjectTo` tercih edin. Varlığı güncelleyecekseniz önce varlığı yükleyip [mevcut nesneye eşleme](./map-to-existing.md) kullanın.

## Kurulum

`ProjectTo` genişletme metotları `VeloxMapper.QueryableExtensions` namespace'indedir. Ek paket gerekmez.

```csharp title="Models.cs"
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public Address? Address { get; set; }
    public List<Order> Orders { get; set; } = new();
}

public class Address { public int Id { get; set; } public string City { get; set; } = ""; }

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; }
}

public class CustomerListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? AddressCity { get; set; }       // flattening: Address.City
    public List<OrderDto> Orders { get; set; } = new();
}

public class OrderDto
{
    public int Id { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "";       // enum → string
}
```

```csharp title="CustomerProfile.cs"
public class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        CreateMap<Customer, CustomerListItemDto>();
        CreateMap<Order, OrderDto>();
    }
}
```

```csharp title="CustomersController.cs"
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VeloxMapper;
using VeloxMapper.QueryableExtensions;

[ApiController]
[Route("api/customers")]
public class CustomersController(ShopDbContext db, IMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<List<CustomerListItemDto>> List(string prefix, CancellationToken ct)
        => await db.Customers
            .Where(c => c.Name.StartsWith(prefix))
            .OrderBy(c => c.Name)
            .ProjectTo<CustomerListItemDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
}
```

`prefix = "A"` için EF Core'un SQLite sağlayıcısının ürettiği SQL:

```sql
SELECT "c"."Id", "c"."Name", "a"."City", "a"."Id", "o"."Id", "o"."Total", "o"."Status"
FROM "Customers" AS "c"
LEFT JOIN "Address" AS "a" ON "c"."AddressId" = "a"."Id"
LEFT JOIN "Orders" AS "o" ON "c"."Id" = "o"."CustomerId"
WHERE "c"."Name" LIKE 'A%'
ORDER BY "c"."Name", "c"."Id", "a"."Id"
```

`Email` kolonu DTO'da olmadığı için sorgulanmaz. `Address` ve `Orders` için `Include` yazılmamış olsa da gerekli `JOIN`'ler üretilir.

`Where`, `OrderBy`, `Skip` ve `Take` gibi filtreleri mümkünse `ProjectTo`'dan **önce**, varlık üzerinde yazın. `ProjectTo`'dan sonra yazılan filtreler DTO üyeleri üzerinden çalışır ve yalnızca sorguya çevrilebilen DTO üyeleri için geçerlidir.

## Overload'lar

`IConfigurationProvider` alan overload'lar AutoMapper ile aynıdır. `IMapper` alan kısayol VeloxMapper'a özgüdür.

| Çağrı | Açıklama |
| --- | --- |
| `query.ProjectTo<TDto>(config, params Expression<Func<TDto, object?>>[] membersToExpand)` | Temel kullanım. |
| `query.ProjectTo<TDto>(config, object? parameters, params Expression<Func<TDto, object?>>[] membersToExpand)` | Parametreler anonim nesneyle. |
| `query.ProjectTo<TDto>(config, IDictionary<string, object> parameters, params string[] membersToExpand)` | Parametreler sözlükle, genişletmeler string yollarla. |
| `query.ProjectTo(Type destinationType, config)` | Hedef tür çalışma zamanında belirlenir; `IQueryable` döner. |
| `query.ProjectTo(Type destinationType, config, IDictionary<string, object> parameters, params string[] membersToExpand)` | Yukarıdakinin parametreli hâli. |
| `query.ProjectTo<TDto>(IMapper mapper, params Expression<Func<TDto, object?>>[] membersToExpand)` | VeloxMapper kısayolu: `query.ProjectTo<TDto>(mapper)`. |
| `mapper.ProjectTo<TDto>(query, object? parameters = null, params ...)` | `IMapper` üzerindeki metot. Bkz. [IMapper](./api/imapper.md#projectto). |

`config` olarak `mapper.ConfigurationProvider`'ı veya DI'dan `IConfigurationProvider`/`MapperConfiguration` çözümünü verebilirsiniz.

## Neler SQL'e çevrilir

`ProjectTo`, eşlemeyi tek bir ifade ağacı olarak üretir. Bu ağaçta yalnızca sorgu sağlayıcısının çevirebileceği yapılar bulunabilir; bu nedenle bazı eşleme özellikleri projeksiyonda kullanılamaz.

| Özellik | `ProjectTo` |
| --- | --- |
| Konvansiyon, flattening, büyük/küçük harf duyarsız eşleşme | Desteklenir |
| İfade tabanlı `MapFrom(s => ...)` (string birleştirme, aritmetik, `Count()`, `Sum()`, metot çağrıları) | Desteklenir; sağlayıcının çevirebildiği sürece |
| `MapFrom("A.B")` | Desteklenir |
| İç içe eşlemeler ve koleksiyonlar | Desteklenir |
| `Ignore`, `ExplicitExpansion`, `NullSubstitute` | Desteklenir |
| İfade tabanlı `ConvertUsing(s => ...)` | Desteklenir |
| `IncludeMembers` | Desteklenir |
| Kurucu ile oluşturulan hedefler (record'lar) | Desteklenir |
| `MaxDepth` | Desteklenir; rekürsif modellerde zorunlu |
| Yerleşik dönüşümler (sayı → string, enum → string, enum ↔ tamsayı, `Nullable` sarma) | Desteklenir |
| Fonksiyon tabanlı `MapFrom((src, dest) => ...)`, value resolver, value converter | **Atlanır**, uyarı loglanır |
| `ForPath` | **Atlanır** |
| `Condition`, `PreCondition` | **Uygulanmaz**; üye koşulsuz sorguya girer |
| `BeforeMap`, `AfterMap`, `UseDestinationValue`, `SetMappingOrder` | **Uygulanmaz** |
| `ITypeConverter` veya fonksiyon tabanlı `ConvertUsing` | **Hata**: `VeloxProjectionException` |
| Polimorfik eşleme (`Include` ile soyut hedef) | **Hata**: `VeloxProjectionException` |
| Sözlük eşlemeleri | Desteklenmez |

Bir `MapFrom` ifadesinin SQL'e çevrilip çevrilemeyeceğine VeloxMapper değil, sorgu sağlayıcısı (EF Core) karar verir. Çevrilemeyen bir metot çağrısı, sorgu çalıştırıldığında EF Core'un kendi hatasıyla sonuçlanır.

> [!NOTE]
> Enum → enum dönüşümü projeksiyonda **sayısal değere** göre yapılır (SQL'de tür dönüşümü). Bellek içi `Map` çağrılarında ise önce isme göre eşlenir. İsimleri aynı ama değerleri farklı enum'lar kullanıyorsanız projeksiyonda `MapFrom` ile açık bir eşleme yazın.

### Atlanan üyeler ve uyarılar

Resolver, value converter veya fonksiyon tabanlı `MapFrom` kullanan bir üye projeksiyondan çıkarılır; hedefte varsayılan değerini alır. AutoMapper bu durumda hata fırlatır; VeloxMapper sorguyu çalıştırır ve [teşhis hedefine](./diagnostics.md#diagnosticssink) bir uyarı yazar:

```text
ProjectTo: 'Segment' üyesi özel resolver/converter kullandığı için projeksiyona dahil edilmedi.
```

Uyarı, projeksiyon ifadesi ilk kez üretildiğinde bir kez yazılır. `ILoggerFactory` kayıtlıysa `VeloxMapper` kategorisinde `Warning` seviyesinde loglanır. Böyle bir üyenin değerine ihtiyacınız varsa sorgudan sonra bellekte doldurun veya kuralı ifade tabanlı bir `MapFrom`'a çevirin.

## Parametreler

Sorgu anında belirlenen bir değeri (geçerli kullanıcı, kiracı, tarih) projeksiyonda kullanmak için profilde yerel bir değişkeni kapatan (closure) bir `MapFrom` yazın ve değeri `ProjectTo` çağrısında aynı adla verin:

```csharp title="CustomerProfile.cs"
public class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        int currentUserId = 0; // varsayılan; ProjectTo parametresiyle değiştirilir

        CreateMap<Customer, CustomerDetailDto>()
            .ForMember(d => d.DisplayName, o => o.MapFrom(s => s.Name + " (" + s.Id + ")"))
            .ForMember(d => d.IsMine, o => o.MapFrom(s => s.Id == currentUserId));
    }
}
```

```csharp
var dto = await db.Customers
    .Where(c => c.Name == "Ada")
    .ProjectTo<CustomerDetailDto>(mapper.ConfigurationProvider, new { currentUserId = 2 })
    .SingleAsync();
```

```sql
SELECT "c"."Id", "c"."Name", "c"."Name" || ' (' || CAST("c"."Id" AS TEXT) || ')' AS "DisplayName",
       "c"."Id" = @Value AS "IsMine", "c"."Email"
FROM "Customers" AS "c"
WHERE "c"."Name" = 'Ada'
```

Parametre adı, kapatılan değişkenin adıyla eşleşmelidir. Sözlük overload'unda anahtar aynı adı taşır:

```csharp
query.ProjectTo<CustomerDetailDto>(mapper.ConfigurationProvider, new Dictionary<string, object> { ["currentUserId"] = 2 });
```

Değer gerekirse değişkenin türüne dönüştürülür. Parametre verilmezse değişkenin profildeki değeri (yukarıda `0`) kullanılır.

> [!NOTE]
> Parametre değerleri ifadeye sabit olarak gömülmez; EF Core bunları SQL parametresi olarak gönderir (yukarıdaki `@Value`). Böylece farklı değerler aynı sorgu planını paylaşır.

## ExplicitExpansion

Bazı üyeler (büyük koleksiyonlar, maliyetli alt sorgular) her sorguda gerekmez. `ExplicitExpansion()` ile işaretlenen üyeler yalnızca `ProjectTo` çağrısında açıkça istendiğinde sorguya eklenir:

```csharp
CreateMap<Customer, CustomerDetailDto>()
    .ForMember(d => d.Orders, o => o.ExplicitExpansion());
```

```csharp
// Orders sorgulanmaz; dto.Orders boş liste kalır
var summary = await query.ProjectTo<CustomerDetailDto>(config).SingleAsync();

// Orders sorguya eklenir
var full = await query.ProjectTo<CustomerDetailDto>(config, null, d => d.Orders).SingleAsync();

// String yol ile (iç içe üyeler için "Orders.Lines" gibi)
var full2 = await query.ProjectTo<CustomerDetailDto>(config, new Dictionary<string, object>(), "Orders").SingleAsync();
```

İç içe bir yol istendiğinde (`"Orders.Lines"`) yoldaki üst üyeler de genişletilmiş sayılır. Yol eşleşmesi büyük/küçük harf duyarsızdır.

`ExplicitExpansion` yalnızca `ProjectTo`'yu etkiler; `Map` çağrılarında üye normal şekilde eşlenir.

## Null propagation

Bellek içi LINQ'ten farklı olarak SQL'de `NULL` bir ilişkinin üyesine erişmek hata vermez. Ancak aynı projeksiyon ifadesi LINQ to Objects ile çalıştırıldığında `NullReferenceException` oluşabilir. VeloxMapper bu nedenle ara üyeler için null kontrolü üretir. `CreateMap<Customer, CustomerListItemDto>()` için `query.Expression.ToString()` çıktısının ilgili kısmı:

```text
AddressCity = IIF((source.Address == null), default(String), source.Address.City)
```

Bu davranış `EnableNullPropagationForQueryMapping` ayarıyla yönetilir ve VeloxMapper'da varsayılan olarak açıktır (AutoMapper'da kapalıdır). Global olarak veya profil bazında kapatabilirsiniz:

```csharp
public class ReportingProfile : Profile
{
    public ReportingProfile()
    {
        EnableNullPropagationForQueryMapping = false;
        CreateMap<Customer, CustomerStatsDto>()
            .ForMember(d => d.OrderCount, o => o.MapFrom(s => s.Orders.Count));
    }
}
```

## Koleksiyonlar

Koleksiyon üyeleri, hedef türe göre `ToList()`, `ToArray()` veya `HashSet<T>` oluşturan bir ifadeye çevrilir. Öğe eşlemesi için ayrı bir `CreateMap` (yukarıda `Order → OrderDto`) tanımlı değilse öğeler konvansiyonla eşlenir.

```csharp
CreateMap<Customer, CustomerDto>()
    .ForMember(d => d.OrderIds, o => o.MapFrom(s => s.Orders.Select(x => x.Id)))   // List<int>
    .ForMember(d => d.RecentOrders, o => o.MapFrom(s => s.Orders.OrderByDescending(x => x.Id).Take(5)));
```

Kaynak koleksiyon üzerinde filtre ve sıralama ifade içinde yazılabilir; EF Core bunları alt sorguya çevirir.

## Rekürsif modeller ve MaxDepth

Kendine referans veren modellerde (kategori ağacı, organizasyon şeması) `ProjectTo` sonsuz bir ifade üretmemek için `MaxDepth` ister:

```csharp
CreateMap<Category, CategoryDto>().MaxDepth(2);
```

```sql
SELECT "c"."Name", "c0"."Id" IS NULL, "c0"."Name"
FROM "Categories" AS "c"
LEFT JOIN "Categories" AS "c0" ON "c"."ParentId" = "c0"."Id"
WHERE "c"."Name" = 'Telefon'
```

`MaxDepth` tanımlanmazsa sorgu üretilirken anlaşılır bir hata alırsınız:

```text
VeloxProjectionException: Category -> CategoryDto projeksiyonu kendini tekrar eden (rekürsif) bir model içeriyor.
Sonsuz sorgu üretimini önlemek için CreateMap(...).MaxDepth(n) tanımlayın.
```

## Üretilen SQL'i incelemek

EF Core'un `ToQueryString()` metodu, sorguyu çalıştırmadan üretilecek SQL'i döndürür. Yeni bir projeksiyon yazdığınızda veya bir eşlemeyi değiştirdiğinizde SQL'i kontrol etmek için kullanın:

```csharp
var sql = db.Customers.ProjectTo<CustomerListItemDto>(mapper.ConfigurationProvider).ToQueryString();
logger.LogDebug("Projeksiyon SQL: {Sql}", sql);
```

`ToQueryString()` ilişkisel sağlayıcılarda (SQL Server, PostgreSQL, SQLite) SQL döndürür; InMemory gibi ilişkisel olmayan sağlayıcılar SQL üretmez.

Projeksiyon ifadesinin kendisini görmek için `query.Expression.ToString()` kullanabilirsiniz; bu çıktı sağlayıcıdan bağımsızdır.

## Performans

- Projeksiyon ifadesi kaynak tür, hedef tür ve genişletilen üyeler için bir kez üretilir ve `MapperConfiguration` üzerinde önbelleklenir. Parametreler her çağrıda bu ifadeye uygulanır.
- `ProjectTo` derlenmiş bir delege çalıştırmaz; asıl iş sorgu sağlayıcısında yapılır. Performansı belirleyen, üretilen SQL'dir.
- Yalnızca projeksiyonda kullanılan eşlemeler için `CreateProjection<TSource, TDestination>()` yazabilirsiniz. VeloxMapper'da bu `CreateMap` ile aynı kaydı oluşturur; okunabilirlik için kullanılır.

## AutoMapper uyumluluğu

`IConfigurationProvider` alan tüm `ProjectTo` overload'ları, parametre deseni ve `ExplicitExpansion` AutoMapper ile aynıdır; `using AutoMapper.QueryableExtensions;` satırını `using VeloxMapper.QueryableExtensions;` olarak değiştirmeniz yeterlidir. Farklar:

| Konu | AutoMapper | VeloxMapper |
| --- | --- | --- |
| Resolver/converter/fonksiyon `MapFrom` içeren üye | Hata fırlatır | Üyeyi atlar, uyarı loglar |
| `EnableNullPropagationForQueryMapping` varsayılanı | `false` | `true` |
| Rekürsif model, `MaxDepth` yok | Derin/sonsuz ifade | `VeloxProjectionException` |
| `query.ProjectTo<T>(mapper)` kısayolu | Yok | Var |
| Polimorfik projeksiyon (`Include`) | Desteklenir | 6.0.0'da desteklenmez |

Tüm farklar için [Davranış Farkları](./behavior-differences.md) sayfasına bakın.

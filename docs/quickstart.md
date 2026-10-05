---
title: "Hızlı Başlangıç"
description: "Bir ASP.NET Core uygulamasında profil tanımlayın, VeloxMapper'ı DI'a kaydedin, Map ve ProjectTo ile ilk eşlemelerinizi çalıştırın."
section: getting-started
order: 30
---

# Hızlı Başlangıç

Bu rehberde sipariş (Order) ve müşteri (Customer) varlıklarını DTO'lara eşleyen küçük bir ASP.NET Core API'si kurarsınız. Sonunda `Map`, `ProjectTo` ve yapılandırma doğrulamasını kullanan, çalışır bir uygulamanız olur.

## Ön koşullar

- .NET 8, 9 veya 10 SDK
- Bir kod düzenleyici (Visual Studio, Rider veya VS Code)

## 1. Projeyi oluşturun

```bash
dotnet new web -n Shop
cd Shop
dotnet add package VeloxMapper --version 6.0.0
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

EF Core InMemory sağlayıcısı yalnızca bu örnekte veritabanı kurmadan `ProjectTo` göstermek için kullanılır. Kendi projenizde SQL Server, PostgreSQL veya başka bir sağlayıcı kullanabilirsiniz.

## 2. Varlıkları ve DTO'ları tanımlayın

```csharp title="Models.cs"
namespace Shop;

public class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
}

public enum OrderStatus { Pending, Paid, Shipped, Cancelled }

public class Order
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public List<OrderLine> Lines { get; set; } = new();

    public decimal GetTotal() => Lines.Sum(l => l.UnitPrice * l.Quantity);
}

public class OrderLine
{
    public int Id { get; set; }
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
```

```csharp title="Dtos.cs"
namespace Shop;

public class OrderDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public decimal Total { get; set; }
    public List<OrderLineDto> Lines { get; set; } = new();
}

public class OrderLineDto
{
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class OrderSummaryDto
{
    public int Id { get; set; }
    public string CustomerEmail { get; set; } = "";
    public int LinesCount { get; set; }
    public OrderStatus Status { get; set; }
}
```

## 3. Bir profil yazın

Eşleme kuralları `Profile` sınıfından türeyen sınıflarda, kurucu metot içinde tanımlanır:

```csharp title="OrderProfile.cs"
using VeloxMapper;

namespace Shop;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<OrderLine, OrderLineDto>();

        CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerName,
                opt => opt.MapFrom(s => s.Customer.FirstName + " " + s.Customer.LastName));

        CreateMap<Order, OrderSummaryDto>();
    }
}
```

Yalnızca `CustomerName` için açık bir kural yazdınız. Diğer üyeler konvansiyonla eşlenir:

| Hedef üye | Kaynak | Kural |
| --- | --- | --- |
| `Id`, `CreatedAt` | `Id`, `CreatedAt` | Aynı ad |
| `Status` (`string`) | `Status` (`OrderStatus`) | Enum → string dönüşümü |
| `CustomerEmail` | `Customer.Email` | [Flattening](./flattening.md) |
| `Total` | `GetTotal()` | `Get` ön ekli metot ([Konvansiyonlar](./conventions.md)) |
| `Lines` (`List<OrderLineDto>`) | `Lines` (`List<OrderLine>`) | [Koleksiyon](./collections.md) eşleme |
| `LinesCount` | `Lines.Count` | Flattening |

## 4. VeloxMapper'ı kaydedin

```csharp title="Program.cs" {8}
using Microsoft.EntityFrameworkCore;
using Shop;
using VeloxMapper;
using VeloxMapper.QueryableExtensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ShopDbContext>(o => o.UseInMemoryDatabase("shop"));
builder.Services.AddVeloxMapper(typeof(Program));

var app = builder.Build();
```

`AddVeloxMapper(typeof(Program))`, `Program` türünün bulunduğu assembly'yi tarar; buradaki tüm `Profile` sınıflarını, `[AutoMap]` özniteliklerini ve resolver/converter türlerini kaydeder. Ayrıntılar için [Dependency Injection](./dependency-injection.md) sayfasına bakın.

`ShopDbContext` sınıfını ekleyin:

```csharp title="ShopDbContext.cs"
using Microsoft.EntityFrameworkCore;

namespace Shop;

public class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Customer> Customers => Set<Customer>();
}
```

## 5. IMapper ile eşleyin

`IMapper` arayüzünü endpoint'e, controller'a veya servise enjekte edin. `Program.cs` dosyasının geri kalanı:

```csharp title="Program.cs"
// Örnek veri
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
    db.Orders.Add(new Order
    {
        Id = 1,
        CreatedAt = new DateTime(2026, 10, 1),
        Status = OrderStatus.Paid,
        Customer = new Customer { Id = 1, FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com" },
        Lines =
        {
            new OrderLine { Id = 1, ProductName = "Klavye", UnitPrice = 1200m, Quantity = 1 },
            new OrderLine { Id = 2, ProductName = "Mouse", UnitPrice = 450m, Quantity = 2 }
        }
    });
    db.SaveChanges();
}

// Tek sipariş: varlığı yükle, IMapper ile DTO'ya eşle
app.MapGet("/orders/{id:int}", async (int id, ShopDbContext db, IMapper mapper) =>
{
    var order = await db.Orders
        .Include(o => o.Customer)
        .Include(o => o.Lines)
        .SingleOrDefaultAsync(o => o.Id == id);

    return order is null ? Results.NotFound() : Results.Ok(mapper.Map<OrderDto>(order));
});

// Liste: ProjectTo ile yalnızca DTO'nun ihtiyaç duyduğu kolonlar sorgulanır
app.MapGet("/orders", async (ShopDbContext db, IMapper mapper) =>
    await db.Orders
        .ProjectTo<OrderSummaryDto>(mapper.ConfigurationProvider)
        .ToListAsync());

app.Run();

public partial class Program { }
```

## 6. Çalıştırın

```bash
dotnet run
```

`GET /orders/1` isteği şu yanıtı döndürür:

```json
{
  "id": 1,
  "createdAt": "2026-10-01T00:00:00",
  "status": "Paid",
  "customerName": "Ada Lovelace",
  "customerEmail": "ada@example.com",
  "total": 2100,
  "lines": [
    { "productName": "Klavye", "unitPrice": 1200, "quantity": 1 },
    { "productName": "Mouse", "unitPrice": 450, "quantity": 2 }
  ]
}
```

`GET /orders` isteği `ProjectTo` ile üretilen sorgunun sonucunu döndürür:

```json
[{ "id": 1, "customerEmail": "ada@example.com", "linesCount": 2, "status": 1 }]
```

`Map` ile `ProjectTo` arasındaki fark önemlidir: `Map` bellekteki bir nesneyi dönüştürür, bu yüzden önce varlığı (ve `Include` ile ilişkilerini) yüklemeniz gerekir. `ProjectTo` ise eşleme kurallarını LINQ sorgusuna çevirir; veritabanından yalnızca DTO'nun ihtiyaç duyduğu kolonlar okunur. Liste endpoint'lerinde `ProjectTo` tercih edin.

## 7. Yapılandırmayı bir testte doğrulayın

`AssertConfigurationIsValid()`, her hedef üyenin bir kaynağı olup olmadığını denetler ve bulunan tüm sorunları tek bir istisnada listeler. Bu çağrıyı bir birim testine ekleyin; DTO'ya yeni bir üye eklenip eşlenmeyi unutulduğunda test başarısız olur.

```csharp title="MappingConfigurationTests.cs"
using Shop;
using VeloxMapper;
using Xunit;

public class MappingConfigurationTests
{
    [Fact]
    public void Mapping_configuration_is_valid()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddMaps(typeof(OrderProfile)));

        configuration.AssertConfigurationIsValid();
    }
}
```

Doğrulama başarısız olduğunda `VeloxMapper.Exceptions.VeloxValidationException` fırlatılır. Mesaj, sorunlu her eşlemeyi profil, tür çifti ve üye adıyla listeler. Hataların nasıl giderileceği için [Sorun Giderme](./troubleshooting.md) sayfasına bakın.

## Sonraki adımlar

- [Yapılandırma ve Profiller](./configuration.md): profilleri düzenleme, global ayarlar
- [Eşleme Konvansiyonları](./conventions.md): hangi üyelerin otomatik eşlendiği
- [Mevcut Nesneye Eşleme](./map-to-existing.md): `Map(source, destination)` ile güncelleme senaryoları
- [Geçiş Rehberi](./migration-guide.md): AutoMapper kullanan bir projeyi taşımak

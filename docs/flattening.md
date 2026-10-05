---
title: "Flattening ve Unflattening"
description: "İç içe nesne yollarını düz DTO üyelerine otomatik eşleyin, ReverseMap ve ForPath ile düz modelleri yeniden iç içe nesnelere çevirin."
section: core-concepts
order: 30
---

# Flattening ve Unflattening

Flattening, iç içe bir nesne grafiğini düz bir DTO'ya eşlemektir: `Order.Customer.Email` değeri `OrderDto.CustomerEmail` üyesine yazılır. Unflattening bunun tersidir. VeloxMapper her ikisini de konvansiyonla yapar.

## Flattening

Hedef üye adı, kaynaktaki bir üye yolunun adlarının birleşimiyse değer bu yoldan okunur:

```csharp
public class Order
{
    public int Id { get; set; }
    public Customer Customer { get; set; } = null!;
}

public class Customer
{
    public string Email { get; set; } = "";
    public Address? Address { get; set; }
}

public class Address
{
    public string City { get; set; } = "";
}

public class OrderDto
{
    public int Id { get; set; }
    public string CustomerEmail { get; set; } = "";       // Customer.Email
    public string? CustomerAddressCity { get; set; }       // Customer.Address.City
}
```

```csharp
cfg.CreateMap<Order, OrderDto>(); // ek kural gerekmez
```

Kurallar:

- Eşleştirme büyük/küçük harfe duyarsızdır ve tanımlı ön ek, son ek ve isimlendirme kurallarını dikkate alır.
- Birden fazla seviye desteklenir (`CustomerAddressCity` → `Customer.Address.City`).
- Yol üzerindeki üyeler property, field veya `Get` ön ekli metot olabilir. Örneğin `Order.GetCustomer()` metodu `CustomerEmail` için `Customer` adımı olarak kullanılabilir.
- Koleksiyon üyeleri de bir adım olabilir: `LinesCount` → `Lines.Count`.
- Aynı adlı doğrudan bir kaynak üye varsa flattening uygulanmaz; doğrudan üye önceliklidir.

### Null ara değerler

Yol üzerindeki bir ara nesne `null` ise eşleme hata vermez; hedef üye türünün varsayılan değerini alır (referans türleri için `null`). Yukarıdaki örnekte `Customer.Address` null ise `CustomerAddressCity` null olur.

Null yerine bir değer istiyorsanız `NullSubstitute` kullanın:

```csharp
cfg.CreateMap<Order, OrderDto>()
    .ForMember(d => d.CustomerAddressCity, opt => opt.NullSubstitute("Bilinmiyor"));
```

### Açık yol tanımlama

Konvansiyona uymayan adlar için `MapFrom` ile yolu açıkça yazın. İfade gövdesindeki ara üyeler de null-güvenli okunur:

```csharp
cfg.CreateMap<Order, OrderDto>()
    .ForMember(d => d.CustomerAddressCity, opt => opt.MapFrom(s => s.Customer.Address!.City));
```

Yolu metin olarak da verebilirsiniz:

```csharp
.ForMember("CustomerAddressCity", opt => opt.MapFrom("Customer.Address.City"))
```

## IncludeMembers

Bir alt nesnenin üyelerini ad ön eki olmadan düz hedefe almak için `IncludeMembers` kullanın. Alt nesne için ayrı bir eşleme tanımlamanız gerekir:

```csharp
public class Shipment
{
    public int Id { get; set; }
    public ShippingDetails Details { get; set; } = new();
}

public class ShippingDetails
{
    public string Carrier { get; set; } = "";
    public string TrackingNumber { get; set; } = "";
}

public class ShipmentDto
{
    public int Id { get; set; }
    public string Carrier { get; set; } = "";           // Details.Carrier
    public string TrackingNumber { get; set; } = "";    // Details.TrackingNumber
}
```

```csharp
cfg.CreateMap<Shipment, ShipmentDto>().IncludeMembers(s => s.Details);
cfg.CreateMap<ShippingDetails, ShipmentDto>(MemberList.None);
```

Alt nesne eşlemesinde (`ShippingDetails → ShipmentDto`) tanımlanan `MapFrom` kuralları da kullanılır. `MemberList.None`, bu ara eşlemede `Id` gibi alt nesnede olmayan üyelerin doğrulama hatası üretmesini önler.

## Unflattening

### ReverseMap ile

`ReverseMap()` ters yöndeki eşlemeyi oluşturur ve flattening'i unflattening olarak tersine çevirir:

```csharp
cfg.CreateMap<Order, OrderDto>().ReverseMap();

var order = mapper.Map<Order>(new OrderDto { Id = 7, CustomerEmail = "ada@example.com", CustomerAddressCity = "Londra" });
// order.Customer.Email == "ada@example.com"
// order.Customer.Address.City == "Londra"
```

Ara nesneler (`Customer`, `Address`) null ise oluşturulur. İleri yönde `MapFrom(s => s.Customer.Email)` gibi tek yollu ifadelerle tanımlanmış kurallar da ters yönde otomatik olarak `ForPath` kurallarına çevrilir:

```csharp
cfg.CreateMap<Order, OrderDto>()
    .ForMember(d => d.CustomerAddressCity, opt => opt.MapFrom(s => s.Customer.Address!.City))
    .ReverseMap(); // ters yönde: ForPath(s => s.Customer.Address.City, o => o.MapFrom(d => d.CustomerAddressCity))
```

`ReverseMap()` ters yöndeki `IMappingExpression` nesnesini döndürür; ters yöne özel kuralları zincirleyebilirsiniz. Zincirlenen kurallar otomatik ters çevrilen kurallardan önceliklidir:

```csharp
cfg.CreateMap<Customer, CustomerDto>()
    .ForMember(d => d.FullName, opt => opt.MapFrom(s => s.FirstName))
    .ReverseMap()
    .ForMember(s => s.LastName, opt => opt.Ignore());
```

`ReverseMap` hakkında bilmeniz gerekenler:

- Ters eşleme varsayılan olarak doğrulanmaz (`MemberList.None`). Doğrulamak için `.ReverseMap().ValidateMemberList(MemberList.Destination)` kullanın.
- İleri yöndeki `Ignore()` kuralları ters çevrilmez.
- Birden fazla kaynak üyeyi birleştiren ifadeler (ör. `s => s.FirstName + " " + s.LastName`) ters çevrilemez; bu üyeler için ters yönde açık kural yazın.

Ayrıntılar için [Reverse Mapping](./reverse-mapping.md) sayfasına bakın.

### ForPath ile

`ReverseMap` kullanmadan iç içe bir hedef yoluna yazmak için `ForPath` kullanın:

```csharp
cfg.CreateMap<OrderDto, Order>(MemberList.None)
    .ForPath(d => d.Customer.Email, opt => opt.MapFrom(s => s.CustomerEmail));
```

`ForMember` yalnızca hedefin doğrudan üyelerini kabul eder. `ForMember(d => d.Customer.Email, ...)` yazarsanız VeloxMapper `ForPath` kullanmanızı öneren bir `ArgumentException` fırlatır.

## ProjectTo ile flattening

Flattening kuralları `ProjectTo` sorgularında da çalışır ve SQL'de `JOIN` ile okunan kolonlara dönüşür. `EnableNullPropagationForQueryMapping` varsayılan olarak açık olduğu için isteğe bağlı (nullable) navigasyonlardaki null değerler güvenle `null` olarak döner. Bkz. [Projection](./projection.md).

## İlgili sayfalar

- [Eşleme Konvansiyonları](./conventions.md)
- [Nested Mapping](./nested-mapping.md)
- [Reverse Mapping](./reverse-mapping.md)

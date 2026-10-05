---
title: "ForPath & IncludeMembers"
description: "İç içe hedef üyelerini ForPath ile doldurun, kaynağın alt nesnelerini IncludeMembers ile düzleştirin."
section: advanced
order: 30
---

# ForPath & IncludeMembers

İç içe nesneler iki yönde sorun çıkarır: düz bir kaynaktan iç içe bir hedefi doldurmak ve iç içe bir kaynaktan düz bir hedef üretmek. `ForPath` birincisini, `IncludeMembers` ikincisini çözer. Konvansiyonla yapılan otomatik düzleştirme için [Flattening & Unflattening](./flattening.md) sayfasına bakın.

## ForPath: iç içe hedef üyeleri

`ForMember` yalnızca hedef türün doğrudan üyelerini kabul eder. İç içe bir hedef yolunu yapılandırmak için `ForPath` kullanın:

```csharp title="Models.cs"
public class CreateOrderRequest
{
    public string CustomerName { get; set; } = "";
    public string ShipToCity { get; set; } = "";
    public string ShipToStreet { get; set; } = "";
}

public class Order
{
    public Customer Customer { get; set; } = new();
    public Address? Shipping { get; set; }
}

public class Customer { public string Name { get; set; } = ""; }
public class Address { public string City { get; set; } = ""; public string Street { get; set; } = ""; }
```

```csharp title="OrderProfile.cs"
CreateMap<CreateOrderRequest, Order>()
    .ForPath(d => d.Customer.Name, o => o.MapFrom(s => s.CustomerName))
    .ForPath(d => d.Shipping!.City, o => o.MapFrom(s => s.ShipToCity))
    .ForPath(d => d.Shipping!.Street, o => o.MapFrom(s => s.ShipToStreet));
```

```csharp
var order = mapper.Map<Order>(new CreateOrderRequest { CustomerName = "Ada", ShipToCity = "İzmir", ShipToStreet = "Kordon" });
// order.Shipping → yeni Address { City = "İzmir", Street = "Kordon" }
```

Davranış:

- Yoldaki ara nesneler (`Shipping`) `null` ise parametresiz kurucuyla oluşturulur.
- `ForPath` kuralları, normal üye atamalarından sonra ve `AfterMap`'ten önce uygulanır.
- `ForPath` ile yapılandırılan bir yolun ilk üyesi (`Shipping`) [doğrulamada](./configuration-validation.md) eşlenmiş sayılır.
- `MapFrom` ifadesinin yanında fonksiyon tabanlı `MapFrom` overload'ları, `Condition` ve `Ignore` da kullanılabilir.

Yoldaki bir üye bulunamazsa veya yazılabilir değilse eşleme ifadesi üretilirken `VeloxConfigurationException` fırlatılır.

> [!TIP]
> `ReverseMap()` kullanıyorsanız çoğu `ForPath` kuralını elle yazmanız gerekmez: ileri yöndeki `MapFrom(s => s.Customer.Name)` kuralları ve flattening, ters yönde otomatik olarak `ForPath` kurallarına çevrilir. Bkz. [ReverseMap](./reverse-mapping.md).

## IncludeMembers: alt nesneleri düzleştirmek

Kaynak nesne, hedef üyelerinin değerlerini bir veya daha fazla alt nesnede taşıyorsa `IncludeMembers` ile bu alt nesneleri kaynak üye havuzuna ekleyin:

```csharp title="Models.cs"
public class Envelope
{
    public int Id { get; set; }
    public Header Header { get; set; } = new();
    public Body Body { get; set; } = new();
}

public class Header { public string Title { get; set; } = ""; public DateTime SentAt { get; set; } }
public class Body { public string Text { get; set; } = ""; }

public class MessageDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime SentAt { get; set; }
    public string Text { get; set; } = "";
}
```

```csharp title="MessageProfile.cs"
CreateMap<Envelope, MessageDto>().IncludeMembers(s => s.Header, s => s.Body);
CreateMap<Header, MessageDto>(MemberList.None);
CreateMap<Body, MessageDto>(MemberList.None);
```

Bir hedef üye için kaynak şu sırayla aranır:

1. Kök kaynağın kendi üyeleri (ve flattening).
2. `IncludeMembers` ile verilen alt nesneler, verilme sırasıyla. Alt nesnenin eşlemesinde (`CreateMap<Header, MessageDto>()`) o üye için bir `MapFrom` kuralı varsa önce o kullanılır.

Alt nesne eşlemelerini `MemberList.None` ile tanımlamanız önerilir: her biri hedefin yalnızca bir kısmını doldurduğu için kendi başına doğrulanırsa eşlenmemiş üye hatası verir. Kök eşleme (`Envelope → MessageDto`) ise tüm üyeleri kapsadığı için normal şekilde doğrulanır.

Alt nesne `null` ise o alt nesneden gelen üyeler hedef türün varsayılan değerini alır.

`IncludeMembers` [ProjectTo](./projection.md) sorgularında da çalışır; alt nesne üyeleri null-güvenli üye erişimlerine çevrilir.

## Hangisini kullanmalı

| Kaynak | Hedef | Yöntem |
| --- | --- | --- |
| Düz | İç içe | `ForPath`, veya ters yönde flattening + `ReverseMap` |
| İç içe (adlar önekli: `CustomerName`) | Düz | Konvansiyon (flattening), ek yapılandırma gerekmez |
| İç içe (adlar öneksiz: `Title`) | Düz | `IncludeMembers` |
| İç içe | İç içe, farklı türler | İç tür çifti için `CreateMap` veya `MapFrom(s => s.Inner)` |

## Sınırlamalar

`ForPath` kuralları [ProjectTo](./projection.md) sorgularında uygulanmaz; projeksiyonda iç içe hedefleri doldurmak için iç tür çifti için ayrı bir eşleme tanımlayın ve `MapFrom(s => s)` gibi bir ifadeyle bağlayın.

## AutoMapper uyumluluğu

`ForPath` ve `IncludeMembers` AutoMapper ile aynı imzalara ve arama sırasına sahiptir.

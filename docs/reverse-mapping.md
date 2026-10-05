---
title: "ReverseMap"
description: "Tek bir CreateMap tanımından ters yönde eşleme üretin ve ters yöne özel kurallar ekleyin."
section: advanced
order: 20
---

# ReverseMap

`ReverseMap()`, bir eşleme tanımından ters yöndeki eşlemeyi üretir. DTO'dan varlığa geri dönüş (form gönderimi, güncelleme komutları) için aynı kuralları iki kez yazmanız gerekmez.

## Temel kullanım

```csharp title="CustomerProfile.cs"
CreateMap<Customer, CustomerDto>()
    .ForMember(d => d.FullName, o => o.MapFrom(s => s.FirstName))
    .ReverseMap();
```

Bu tanım iki eşleme oluşturur: `Customer → CustomerDto` ve `CustomerDto → Customer`.

## Neler tersine çevrilir

| İleri yöndeki kural | Ters yönde |
| --- | --- |
| Konvansiyonla eşlenen aynı adlı üyeler | Konvansiyonla eşlenir |
| `MapFrom(s => s.FirstName)` (tek üye) | `FirstName ← FullName` |
| `MapFrom(s => s.Address.City)` (üye zinciri) | `ForPath(d => d.Address.City, ...)` kuralına dönüşür |
| Flattening (`AddressCity ← Address.City`) | Unflattening: `Address.City ← AddressCity` |
| `Include` / `IncludeBase` | Tür çiftleri yer değiştirerek korunur |
| `ConvertUsingEnumMapping` içindeki `MapValue` eşlemeleri | Ters çevrilir |
| `MaxDepth`, `PreserveReferences` | Korunur |

Tersine **çevrilmeyen** kurallar:

- `Ignore()`: İleri yönde yok sayılan üye ters yönde konvansiyonla eşlenir.
- Hesaplanmış ifadeler (`s => s.FirstName + " " + s.LastName`), fonksiyonlar, resolver'lar ve converter'lar: Tersinin ne olacağı belirsizdir; ters yönde bu üyeler konvansiyonla eşlenir veya eşlenmez.
- `BeforeMap`, `AfterMap`, `Condition`, `NullSubstitute`.

## Ters yöne özel kurallar

`ReverseMap()` ters yöndeki eşleme ifadesini döndürür. Zincire eklediğiniz kurallar yalnızca ters yöne uygulanır ve otomatik üretilen kurallardan önceliklidir:

```csharp
CreateMap<Customer, CustomerDto>()
    .ForMember(d => d.FullName, o => o.MapFrom(s => s.FirstName))
    .ReverseMap()
    .ForMember(s => s.CreatedAt, o => o.Ignore())
    .ForMember(s => s.LastName, o => o.MapFrom(d => d.FullName.Substring(d.FullName.IndexOf(' ') + 1)));
```

```csharp
var customer = mapper.Map<Customer>(new CustomerDto { FullName = "Ada Lovelace", AddressCity = "Londra" });
// customer.FirstName    → "Ada Lovelace"  (MapFrom(s => s.FirstName) tersine çevrildi)
// customer.LastName     → "Lovelace"      (ters yöne özel kural)
// customer.Address.City → "Londra"        (unflattening; Address oluşturuldu)
```

## Doğrulama

Ters eşleme varsayılan olarak `MemberList.None` ile oluşturulur; [AssertConfigurationIsValid](./configuration-validation.md) ters yöndeki eşlenmemiş üyeler için hata vermez. Ters yönü de doğrulamak istiyorsanız zincirde `ValidateMemberList` çağırın:

```csharp
CreateMap<Customer, CustomerDto>()
    .ReverseMap()
    .ValidateMemberList(MemberList.Destination);
```

## Açık tanımla birlikte

Aynı ters tür çifti için ayrıca `CreateMap<CustomerDto, Customer>()` yazarsanız açık tanım kullanılır ve `ReverseMap` ile üretilen eşleme yok sayılır. Bu durum "aynı tür çifti iki kez tanımlandı" hatası oluşturmaz.

## Open generic ve enum eşlemeleri

`ReverseMap` open generic eşlemelerde ve enum eşlemelerinde de çalışır:

```csharp
cfg.CreateMap(typeof(Page<>), typeof(PageDto<>)).ReverseMap();

cfg.CreateMap<OrderStatus, OrderStatusDto>()
    .ConvertUsingEnumMapping(o => o.MapValue(OrderStatus.Pending, OrderStatusDto.AwaitingPayment))
    .ReverseMap(); // OrderStatusDto.AwaitingPayment → OrderStatus.Pending
```

## AutoMapper uyumluluğu

`ReverseMap()` AutoMapper ile aynı şekilde ters ifadeyi döndürür ve zincirlenebilir. Tersine çevrilen kurallar (tek üye `MapFrom`, üye zinciri, unflattening), ters eşlemenin `MemberList.None` ile oluşturulması ve `Ignore` kurallarının ters çevrilmemesi AutoMapper davranışıyla aynıdır.

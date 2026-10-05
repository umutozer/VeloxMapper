---
title: "Enum Eşleme"
description: "Enum değerlerinin ada, sayısal değere ve string'e göre nasıl eşlendiğini öğrenin; ConvertUsingEnumMapping ile özel eşleme kuralları tanımlayın."
section: core-concepts
order: 50
---

# Enum Eşleme

VeloxMapper enum türleri arasında, enum ile `string` arasında ve enum ile tamsayılar arasında yapılandırma gerektirmeden dönüşüm yapar. Özel kurallar için AutoMapper'ın EnumMapping eklentisindeki `ConvertUsingEnumMapping` API'si yerleşik olarak gelir.

## Enum → enum

Varsayılan kural AutoMapper ile aynıdır:

1. Önce **ada göre** eşlenir (büyük/küçük harfe duyarsız).
2. Hedefte aynı adlı bir değer yoksa **sayısal değere göre** eşlenir.

```csharp
public enum OrderStatus    { Pending = 0, Paid = 1, Shipped = 2 }
public enum OrderStatusDto { Shipped = 0, Pending = 1, Paid = 2, Unknown = 3 }
```

```csharp
mapper.Map<OrderStatusDto>(OrderStatus.Paid);    // OrderStatusDto.Paid  (ada göre; sayısal değerler farklı)
mapper.Map<OrderStatusDto>((OrderStatus)3);      // OrderStatusDto.Unknown (adı yok, sayısal değer 3)
```

Hedefte karşılığı olmayan bir sayısal değer sessizce hedef enum'a dönüştürülür (ör. `(OrderStatusDto)7`). Bunu istemiyorsanız `ConvertUsingEnumMapping` ile açık kural tanımlayın.

`[Flags]` enum'larda birleşik değerlerin (`Read | Write`) tek bir adı olmadığı için bu değerler sayısal değere göre eşlenir. Bayrak değerlerinin her iki enum'da aynı olduğundan emin olun.

## Enum ↔ string

| Yön | Davranış |
| --- | --- |
| Enum → `string` | `ToString()` (ör. `"Paid"`) |
| `string` → enum | `Enum.Parse` ile, büyük/küçük harfe duyarsız (`"paid"` → `Paid`) |
| Boş veya null `string` → enum | Enum'un varsayılan değeri (`default`) |
| Geçersiz `string` → enum | `VeloxMappingException` (iç istisna: `ArgumentException`) |

```csharp
public class OrderDto
{
    public string Status { get; set; } = "";   // OrderStatus → "Paid"
}
```

Geçersiz değerlerde hata yerine bir varsayılan istiyorsanız global bir dönüştürücü tanımlayın. `out var` ifade ağacında (expression tree) kullanılamadığı için iki parametreli `Func` overload'unu kullanın:

```csharp
cfg.CreateMap<string, OrderStatus>()
    .ConvertUsing((s, _) => Enum.TryParse<OrderStatus>(s, true, out var status) ? status : OrderStatus.Pending);
```

Bu dönüştürücü, `string` → `OrderStatus` gereken tüm üyelerde kullanılır.

## Enum ↔ tamsayı

Enum ile tamsayı türleri (`int`, `long`, `byte` ve diğerleri) arasında doğrudan sayısal dönüşüm yapılır:

```csharp
public class OrderRow
{
    public int Status { get; set; }   // OrderStatus.Shipped → 2
}
```

## ConvertUsingEnumMapping

Varsayılan kural yetmediğinde enum çifti için açık bir eşleme tanımlayın. AutoMapper'daki `AutoMapper.Extensions.EnumMapping` paketinin API'si aynen kullanılabilir; ayrı paket veya `using` gerekmez.

```csharp
cfg.CreateMap<OrderStatus, OrderStatusDto>()
    .ConvertUsingEnumMapping(opt => opt
        .MapByName()
        .MapValue(OrderStatus.Pending, OrderStatusDto.Unknown))
    .ReverseMap();
```

| Metot | Açıklama |
| --- | --- |
| `MapByName(bool ignoreCase = false)` | Ada göre eşler. Varsayılan olarak büyük/küçük harfe **duyarlıdır**; `ignoreCase: true` ile duyarsız yapılır. Adı bulunamayan değerler sayısal değere göre eşlenir. |
| `MapByValue()` | Yalnızca sayısal değere göre eşler. |
| `MapValue(source, destination)` | Belirli bir değer için açık eşleme. Diğer kurallardan önceliklidir. |

`ReverseMap()` çağrıldığında `MapValue` kuralları ters çevrilir (`OrderStatusDto.Unknown` → `OrderStatus.Pending`).

`ConvertUsingEnumMapping` yalnızca iki enum türü (veya nullable enum türleri) arasında kullanılabilir; aksi halde `ArgumentException` fırlatılır.

## Nullable enum'lar

`OrderStatus?` ↔ `OrderStatusDto?` ve `OrderStatus` ↔ `OrderStatus?` eşlemeleri yukarıdaki kurallarla yapılır. `null` kaynak, nullable hedefte `null`, nullable olmayan hedefte enum'un varsayılan değeri olur.

## ProjectTo ile enum'lar

`ProjectTo` sorgularında enum → enum dönüşümü SQL'e çevrilebilmesi için **sayısal değere göre** yapılır; ada göre eşleme uygulanmaz. Enum'larınızın sayısal değerleri farklıysa projeksiyonda `MapFrom` ile açık bir ifade yazın:

```csharp
cfg.CreateMap<Order, OrderSummaryDto>()
    .ForMember(d => d.Status, opt => opt.MapFrom(s =>
        s.Status == OrderStatus.Paid ? OrderStatusDto.Paid :
        s.Status == OrderStatus.Shipped ? OrderStatusDto.Shipped :
        OrderStatusDto.Pending));
```

`string` → enum dönüşümü `ProjectTo` içinde desteklenmez.

## İlgili sayfalar

- [Eşleme Konvansiyonları](./conventions.md#tür-dönüşümleri)
- [Type Converters](./type-converters.md)
- [Reverse Mapping](./reverse-mapping.md)

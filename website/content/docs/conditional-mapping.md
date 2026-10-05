---
title: "Koşullu Eşleme"
description: "Condition ve PreCondition ile bir üyenin hangi durumda atanacağını belirleyin."
section: customization
order: 20
---

# Koşullu Eşleme

Bir hedef üyenin yalnızca belirli bir koşul sağlandığında atanmasını istiyorsanız `Condition` veya `PreCondition` kullanırsınız. Koşul `false` döndüğünde üye atlanır ve hedefteki mevcut değer korunur.

## Condition ve PreCondition farkı

İki yöntem de üyeyi atlamaya yarar; fark, değerin ne zaman hesaplandığıdır.

| | `PreCondition` | `Condition` |
| --- | --- | --- |
| Değerlendirilme anı | Kaynak değer hesaplanmadan **önce** | Kaynak değer hesaplandıktan **sonra** |
| Koşul `false` ise | `MapFrom` ifadesi, resolver veya converter hiç çalışmaz | Değer hesaplanır ama hedefe yazılmaz |
| Çözülen değere erişim | Yok | Var (üçüncü parametre) |
| Uygun olduğu durum | Pahalı veya yan etkili değer üretimini atlamak | Değerin kendisine göre karar vermek |

```csharp title="ProductProfile.cs"
CreateMap<Product, ProductDto>()
    // Stok bilgisi yalnızca aktif ürünler için hesaplanır; resolver pasif ürünlerde hiç çalışmaz.
    .ForMember(d => d.StockLevel, o =>
    {
        o.PreCondition(s => s.IsActive);
        o.MapFrom<StockLevelResolver>();
    })
    // Fiyat yalnızca pozitifse atanır.
    .ForMember(d => d.Price, o => o.Condition(s => s.Price > 0));
```

## Condition overload'ları

| İmza | Parametreler |
| --- | --- |
| `Condition(Func<TSource, bool>)` | Kaynak nesne |
| `Condition(Func<TSource, TDestination, bool>)` | Kaynak, hedef |
| `Condition(Func<TSource, TDestination, TMember, bool>)` | Kaynak, hedef, **çözülen kaynak değer** |
| `Condition(Func<TSource, TDestination, TMember, TMember, bool>)` | Kaynak, hedef, çözülen kaynak değer, hedef üyenin mevcut değeri |
| `Condition(Func<TSource, TDestination, TMember, TMember, ResolutionContext, bool>)` | Yukarıdakiler ve çalışma zamanı bağlamı |

Üçüncü parametre, kaynak nesnedeki aynı adlı üye değil, `MapFrom` veya konvansiyonla **çözülmüş değerdir**. Bu nedenle `ForAllMembers` ile birlikte genel bir kural yazabilirsiniz.

## PreCondition overload'ları

| İmza | Parametreler |
| --- | --- |
| `PreCondition(Func<TSource, bool>)` | Kaynak nesne |
| `PreCondition(Func<ResolutionContext, bool>)` | Çalışma zamanı bağlamı |
| `PreCondition(Func<TSource, ResolutionContext, bool>)` | Kaynak, bağlam |
| `PreCondition(Func<TSource, TDestination, ResolutionContext, bool>)` | Kaynak, hedef, bağlam |

Bağlam alan overload'lar, kararı çağrı anında vermenizi sağlar:

```csharp
CreateMap<Customer, CustomerDto>()
    .ForMember(d => d.Email, o => o.PreCondition(ctx =>
        ctx.Items.TryGetValue("IncludeContact", out var include) && (bool)include));

var full = mapper.Map<CustomerDto>(customer, opt => opt.Items["IncludeContact"] = true);
var limited = mapper.Map<CustomerDto>(customer); // Email atanmaz
```

## PATCH: null değerleri atlamak

Kısmi güncelleme isteklerinde gönderilmeyen alanlar `null` gelir ve hedefteki değeri ezmemelidir. `ForAllMembers` ile çözülen değer üzerinden tek bir kural yazın:

```csharp
public class UpdateUserRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public int? Age { get; set; }
}

CreateMap<UpdateUserRequest, User>()
    .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));

var user = new User { Name = "Ayşe", Email = "ayse@ornek.com", Age = 30 };
mapper.Map(new UpdateUserRequest { Email = "yeni@ornek.com" }, user);
// user.Name  → "Ayşe"            (korundu)
// user.Email → "yeni@ornek.com"  (güncellendi)
// user.Age   → 30                (korundu)
```

Bu davranışı tüm eşlemeler için genel olarak istiyorsanız VeloxMapper'a özgü `PatchMapping.IgnoreNullValues` ayarını kullanabilirsiniz. Bkz. [Null Yönetimi](./null-handling.md#patch-için-null-değerleri-atlamak).

## ProjectTo'da koşullar

`Condition` ve `PreCondition`, [ProjectTo](./projection.md) ile üretilen sorgulara uygulanmaz; üye koşuldan bağımsız olarak sorguya dahil edilir. Sorgu tarafında koşullu bir değer gerekiyorsa koşulu ifadenin içine yazın:

```csharp
.ForMember(d => d.Price, o => o.MapFrom(s => s.Price > 0 ? s.Price : (decimal?)null))
```

## AutoMapper uyumluluğu

`Condition` ve `PreCondition` overload'ları AutoMapper ile aynıdır. Üçüncü parametrenin çözülen kaynak değer olması AutoMapper davranışıyla aynıdır; bu sayede `ForAllMembers` PATCH deseni değişiklik gerektirmeden çalışır.

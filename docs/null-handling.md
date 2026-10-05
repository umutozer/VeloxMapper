---
title: "Null Yönetimi"
description: "Null kaynak değerlerinin hedefe nasıl yansıyacağını global, profil ve üye düzeyinde kontrol edin."
section: customization
order: 30
---

# Null Yönetimi

Kaynakta `null` olan bir değerin hedefe nasıl yazılacağı hedef üyenin türüne ve yapılandırmanıza bağlıdır. Bu sayfa varsayılan davranışı, bunu değiştiren ayarları ve PATCH senaryosunu anlatır.

## Varsayılan davranış

| Kaynak değer | Hedef üye türü | Sonuç |
| --- | --- | --- |
| `null` | Referans türü (`string`, sınıf) | `null` |
| `null` | Koleksiyon (`List<T>`, dizi, `IEnumerable<T>`...) | Boş koleksiyon |
| `null` (`int?`) | Değer türü (`int`) | Varsayılan değer (`0`) |
| `null` iç nesne yolu (`s.Customer.Name`, `Customer` null) | Herhangi | Hedef türün varsayılan değeri; `NullReferenceException` oluşmaz |

Kök nesne için de aynı kurallar geçerlidir: `mapper.Map<OrderDto>(null)` `null`, `mapper.Map<List<OrderDto>>(null)` boş liste döndürür. `Map(source, destination)` çağrısında `source` `null` ise hedef değiştirilmeden döner.

## Global ve profil ayarları

İki ayar varsayılan davranışı değiştirir. Her ikisi de global yapılandırmada veya bir profilin kurucusunda ayarlanabilir; profilde verilen değer yalnızca o profilin eşlemelerine uygulanır.

| Ayar | Varsayılan | `false`/`true` yapıldığında |
| --- | --- | --- |
| `AllowNullCollections` | `false` | `true`: `null` kaynak koleksiyon hedefe boş koleksiyon yerine `null` olarak yazılır. |
| `AllowNullDestinationValues` | `true` | `false`: `null` kaynak nesne, hedefte parametresiz kurucuyla oluşturulmuş boş bir nesneye dönüşür. `string` üyeler `null` kalır. |

```csharp title="Program.cs"
var config = new MapperConfiguration(cfg =>
{
    cfg.AllowNullCollections = true;
    cfg.AddProfile<OrderProfile>();
});
```

```csharp title="LegacyProfile.cs"
public class LegacyProfile : Profile
{
    public LegacyProfile()
    {
        // Yalnızca bu profildeki eşlemelerde iç nesneler hiçbir zaman null olmaz.
        AllowNullDestinationValues = false;
        CreateMap<LegacyOrder, OrderDto>();
    }
}
```

## Üye düzeyinde ayar

Tek bir üye için global/profil ayarını ezmek istiyorsanız `AllowNull()` veya `DoNotAllowNull()` kullanın:

```csharp
CreateMap<Order, OrderDto>()
    .ForMember(d => d.Tags, o => o.AllowNull())          // null kaynak → null (boş liste değil)
    .ForMember(d => d.Shipping, o => o.DoNotAllowNull()); // null kaynak → yeni ShippingDto
```

## NullSubstitute

Çözülen kaynak değer `null` olduğunda hedefe sabit bir değer yazmak için `NullSubstitute` kullanın. Değer hedef üyenin türüne dönüştürülür:

```csharp
CreateMap<Order, OrderDto>()
    .ForMember(d => d.Note, o => o.NullSubstitute("(not yok)"))
    .ForMember(d => d.Quantity, o => o.NullSubstitute(-1)); // kaynak int?, hedef int
```

`NullSubstitute` değeri hedef türe dönüştürülemiyorsa (ör. `int` üyeye `"abc"`), eşleme ifadesi üretilirken `VeloxConfigurationException` fırlatılır.

`NullSubstitute`, [ProjectTo](./projection.md) sorgularında da çalışır ve SQL'de `CASE WHEN ... IS NULL` ifadesine çevrilir. Attribute ile kullanım için [`[NullSubstitute]`](./api/attributes.md#nullsubstitute) özniteliğine bakın.

## PATCH için null değerleri atlamak

Mevcut bir nesneyi güncellerken (`mapper.Map(source, destination)`) varsayılan davranış AutoMapper ile aynıdır: kaynaktaki `null` değer hedefe yazılır. Kısmi güncelleme isteklerinde bu genellikle istenmez. İki yol vardır:

<!-- tabs -->
```csharp title="AutoMapper"
// Eşleme bazında: çözülen kaynak değer null ise üyeyi atla
CreateMap<UpdateUserRequest, User>()
    .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));
```
```csharp title="VeloxMapper"
// AutoMapper yazımı aynen çalışır. Ek olarak tüm mevcut nesneye eşlemeler için tek ayar:
var config = new MapperConfiguration(cfg =>
{
    cfg.PatchMapping.IgnoreNullValues = true;
    cfg.CreateMap<UpdateUserRequest, User>();
});
```
<!-- /tabs -->

`PatchMapping.IgnoreNullValues` yalnızca `Map(source, destination)` çağrılarını etkiler; yeni nesne oluşturan `Map<TDestination>(source)` çağrılarında `null` değerler normal şekilde yazılır.

| Çağrı | `IgnoreNullValues = false` (varsayılan) | `IgnoreNullValues = true` |
| --- | --- | --- |
| `mapper.Map(new UpdateUserRequest { Age = 31 }, user)` | `user.Name` ve `user.Email` `null` olur | `user.Name` ve `user.Email` korunur |

## ProjectTo'da null propagation

`ProjectTo` ile üretilen ifadelerde ara üyeler için null kontrolü `EnableNullPropagationForQueryMapping` ayarıyla yönetilir. VeloxMapper'da varsayılan `true`'dur (AutoMapper'da `false`). Ayrıntılar için [ProjectTo & EF Core](./projection.md#null-propagation) sayfasına bakın.

## AutoMapper uyumluluğu

`AllowNullCollections`, `AllowNullDestinationValues`, `AllowNull`, `DoNotAllowNull` ve `NullSubstitute` AutoMapper ile aynı varsayılanlara ve davranışa sahiptir. Farklar:

- `PatchMapping.IgnoreNullValues` yalnızca VeloxMapper'da vardır.
- `EnableNullPropagationForQueryMapping` varsayılanı VeloxMapper'da `true`'dur.
- [Value transformer](./value-transformers.md)'lar `null` değerlere uygulanmaz.

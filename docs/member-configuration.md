---
title: "ForMember & MapFrom"
description: "Hedef üyelerin değerini nereden alacağını ForMember, MapFrom, Ignore ve ForAllMembers ile belirleyin."
section: customization
order: 10
---

# ForMember & MapFrom

Konvansiyonla eşlenemeyen veya farklı bir kaynaktan gelmesi gereken hedef üyeleri `ForMember` ile tek tek yapılandırırsınız. Bu sayfa `MapFrom` overload'larını, üyeleri yok saymayı ve tüm üyelere toplu kural uygulamayı anlatır.

## Temel kullanım

`ForMember`, ilk parametrede hedef üyeyi, ikinci parametrede o üyeye ait kuralları alır. Kurallar `IMappingExpression` üzerinde zincirlenir:

```csharp title="OrderProfile.cs"
using VeloxMapper;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.Customer.FirstName + " " + s.Customer.LastName))
            .ForMember(d => d.LineCount, o => o.MapFrom(s => s.Lines.Count))
            .ForMember(d => d.InternalCode, o => o.Ignore());
    }
}
```

Hedef üye adını derleme zamanında bilmediğiniz durumlarda (ör. `ForAllMaps` içinde) string overload'unu kullanabilirsiniz:

```csharp
CreateMap<Order, OrderDto>()
    .ForMember("CustomerName", o => o.MapFrom("Customer.FirstName"));
```

> [!NOTE]
> `ForMember` yalnızca hedef türün doğrudan üyelerini kabul eder. `d => d.Customer.Name` gibi iç içe bir yol verirseniz yapılandırma sırasında `ForPath` kullanmanızı öneren bir `ArgumentException` alırsınız. İç içe hedefler için [ForPath & IncludeMembers](./nested-mapping.md) sayfasına bakın.

## MapFrom overload'ları

| Overload | Ne zaman kullanılır | `ProjectTo` |
| --- | --- | --- |
| `MapFrom(s => s.A.B)` | Kaynaktan hesaplanabilen her değer. En yaygın biçim. | Evet |
| `MapFrom("A.B")` | Üye yolu çalışma zamanında belirlenen kurallar. | Evet |
| `MapFrom((src, dest) => ...)` | Hedefin mevcut durumuna bağlı hesaplamalar. | Hayır (atlanır) |
| `MapFrom((src, dest, destMember) => ...)` | Hedef üyenin mevcut değerine göre karar. | Hayır (atlanır) |
| `MapFrom((src, dest, destMember, ctx) => ...)` | `ctx.Items` gibi çağrı parametrelerine erişim. | Hayır (atlanır) |
| `MapFrom<TResolver>()` ve diğer resolver overload'ları | Yeniden kullanılabilir, DI destekli mantık. [Value Resolver](./value-resolvers.md) sayfasına bakın. | Hayır (atlanır) |

İfade tabanlı `MapFrom` (`Expression<Func<TSource, TMember>>`) hem bellek içi eşlemede hem de [ProjectTo](./projection.md) ile SQL'e çevrilen sorgularda çalışır. Fonksiyon tabanlı overload'lar ve resolver'lar yalnızca `Map` çağrılarında çalışır; `ProjectTo` bu üyeleri sorgudan çıkarır.

### İfade tabanlı MapFrom

İfadenin türü hedef üyenin türüyle aynı olmak zorunda değildir. Değer, normal eşleme kurallarıyla hedef türe çevrilir: iç içe nesneler eşlenir, koleksiyonlar dönüştürülür, sayısal ve string dönüşümleri uygulanır.

```csharp
CreateMap<Order, OrderDto>()
    .ForMember(d => d.Buyer, o => o.MapFrom(s => s.Customer)) // Customer → CustomerDto
    .ForMember(d => d.Items, o => o.MapFrom(s => s.Lines));   // List<OrderLine> → List<OrderLineDto>
```

İfadedeki ara üyeler null-güvenlidir. `s => s.Customer.Address.City` ifadesinde `Customer` veya `Address` `null` ise sonuç `NullReferenceException` değil, hedef türün varsayılan değeridir.

### Bağlam alan MapFrom

Çağrıya özel bir değere ihtiyacınız varsa dört parametreli overload ile `ResolutionContext`'e erişin:

```csharp
CreateMap<Order, OrderDto>()
    .ForMember(d => d.Currency, o => o.MapFrom((src, dest, member, ctx) => (string)ctx.Items["Currency"]));

var dto = mapper.Map<OrderDto>(order, opt => opt.Items["Currency"] = "TRY");
```

`Items` sözlüğü hakkında ayrıntılar için [IMapper](./api/imapper.md#çağrı-seçenekleri) referansına bakın.

## Üyeleri yok saymak

`Ignore()` hedef üyeyi hem eşlemeden hem de [yapılandırma doğrulamasından](./configuration-validation.md) çıkarır. VeloxMapper, aynı işi yapan kısa bir yazım da sunar:

<!-- tabs -->
```csharp title="AutoMapper"
CreateMap<Order, OrderDto>()
    .ForMember(d => d.InternalCode, o => o.Ignore());
```
```csharp title="VeloxMapper"
// AutoMapper yazımı aynen çalışır; ek olarak kısayol:
CreateMap<Order, OrderDto>()
    .Ignore(d => d.InternalCode);
```
<!-- /tabs -->

Üyeyi eşlemeye devam edip yalnızca doğrulamadan muaf tutmak istiyorsanız `DoNotValidate()` kullanın:

| Kural | Üye eşlenir mi? | Doğrulamada hata verir mi? |
| --- | --- | --- |
| `Ignore()` | Hayır | Hayır |
| `DoNotValidate()` | Evet, konvansiyonla eşlenebiliyorsa | Hayır |

Belirli bir önekle başlayan üyeleri tüm eşlemelerde yok saymak için `cfg.AddGlobalIgnore("Audit")` kullanabilirsiniz; `AuditTrail`, `AuditUser` gibi tüm üyeler yok sayılır.

## Tüm üyelere kural uygulamak

`ForAllMembers` bir eşlemenin tüm hedef üyelerine aynı kuralı uygular. En yaygın kullanım, `null` kaynak değerlerin hedefi ezmediği PATCH senaryosudur:

```csharp
CreateMap<UpdateCustomerRequest, Customer>()
    .ForAllMembers(o => o.Condition((src, dest, srcMember) => srcMember != null));
```

`ForAllOtherMembers` ise yalnızca `ForMember` veya `ForPath` ile açıkça yapılandırılmamış üyelere uygulanır:

```csharp
CreateMap<Customer, CustomerSummaryDto>()
    .ForMember(d => d.Id, o => o.MapFrom(s => s.Id))
    .ForAllOtherMembers(o => o.Ignore());
```

Kural lambdasında `o.DestinationMember` ile üyenin `MemberInfo`'suna erişip üye bazında karar verebilirsiniz.

Profil veya global düzeyde, birden çok eşlemeye aynı anda kural uygulamak için `ForAllMaps` ve `ForAllPropertyMaps` kullanılır. Bunlar [MapperConfiguration API](./api/mapper-configuration.md) sayfasında anlatılır.

## Hedef değeri ve atama sırası

| Kural | Etki |
| --- | --- |
| `UseDestinationValue()` | Hedef üyedeki mevcut nesneyi veya koleksiyonu korur ve kaynağı bu örneğe eşler. |
| `DoNotUseDestinationValue()` | Mevcut değeri kullanmaz; her eşlemede yeni değer atanır. |
| `SetMappingOrder(int)` | Üyelerin atanma sırasını belirler; küçük değerler önce atanır. |

`Map(source, destination)` çağrısında iç içe nesneler ve koleksiyonlar varsayılan olarak zaten mevcut örneklerine eşlenir. Ayrıntılar için [Mevcut Nesneye Eşleme](./map-to-existing.md) sayfasına bakın.

## Kaynak üyeleri yapılandırmak

`ForSourceMember`, `MemberList.Source` ile doğrulama yaptığınızda bir kaynak üyeyi doğrulamadan muaf tutar:

```csharp
CreateMap<Customer, CustomerDto>(MemberList.Source)
    .ForSourceMember(s => s.PasswordHash, o => o.DoNotValidate());
```

Ayrıntılar için [Yapılandırma Doğrulama](./configuration-validation.md#kaynak-üyelerini-doğrulamak) sayfasına bakın.

## Kurucu parametreleri

Hedef tür bir record veya yalnızca kurucuyla değer alan bir sınıfsa, parametre değerlerini `ForCtorParam` ile belirlersiniz:

```csharp
CreateMap<PersonEntity, PersonRecord>()
    .ForCtorParam("LastName", o => o.MapFrom(s => s.Surname.ToUpperInvariant()));
```

Kurucu seçimi kuralları [Constructor & Record](./constructors.md) sayfasında anlatılır.

## AutoMapper uyumluluğu

`ForMember`, `MapFrom`'un tüm overload'ları, `Ignore`, `DoNotValidate`, `ForAllMembers`, `ForAllOtherMembers`, `ForSourceMember`, `ForCtorParam`, `UseDestinationValue` ve `SetMappingOrder` AutoMapper ile aynı imzalara sahiptir. Fark yaratan noktalar:

- `Ignore(d => d.Member)` kısayolu yalnızca VeloxMapper'da vardır.
- `ProjectTo`, fonksiyon tabanlı `MapFrom` ve resolver kullanan üyeleri hata fırlatmak yerine sorgudan çıkarır ve bir uyarı loglar. Bkz. [Davranış Farkları](./behavior-differences.md).

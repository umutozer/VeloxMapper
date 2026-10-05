---
title: "Value Resolver"
description: "Hedef üye değerini üreten mantığı DI destekli, test edilebilir resolver sınıflarına taşıyın."
section: customization
order: 40
---

# Value Resolver

Bir üyenin değerini üreten mantık tek satırlık bir `MapFrom` ifadesine sığmıyorsa, servis gerektiriyorsa veya birden çok eşlemede tekrar ediyorsa bir value resolver yazın. Resolver'lar DI konteynerinden çözülür; scoped servisleri (ör. `DbContext`, istek bağlamı) kurucudan alabilir.

## Resolver türleri

| Arayüz | Girdi | Kullanım |
| --- | --- | --- |
| `IValueResolver<TSource, TDestination, TDestMember>` | Kaynak nesnenin tamamı | `o.MapFrom<TResolver>()` |
| `IMemberValueResolver<TSource, TDestination, TSourceMember, TDestMember>` | Seçilen tek bir kaynak üye | `o.MapFrom<TResolver, TSourceMember>(s => s.Member)` |

## IValueResolver

```csharp title="TenantNameResolver.cs"
using VeloxMapper;

public sealed class TenantNameResolver : IValueResolver<Order, OrderDto, string>
{
    private readonly ITenantContext _tenant;

    public TenantNameResolver(ITenantContext tenant) => _tenant = tenant;

    public string Resolve(Order source, OrderDto destination, string destMember, ResolutionContext context)
        => $"{_tenant.Name}/{source.Id}";
}
```

```csharp title="OrderProfile.cs"
CreateMap<Order, OrderDto>()
    .ForMember(d => d.TenantName, o => o.MapFrom<TenantNameResolver>());
```

`Resolve` metodunun parametreleri:

| Parametre | Açıklama |
| --- | --- |
| `source` | Kaynak nesne. |
| `destination` | Oluşturulmakta olan hedef nesne. |
| `destMember` | Hedef üyenin mevcut değeri (`Map(source, destination)` çağrısında doludur). |
| `context` | Çağrı bağlamı: `Items`, `State`, `Mapper`. |

## IMemberValueResolver

Aynı mantığı farklı kaynak üyelere uygulamak istediğinizde member value resolver kullanın. Resolver, kaynak nesnenin yanında seçilen üyenin değerini de alır:

```csharp title="UpperCaseResolver.cs"
public sealed class UpperCaseResolver : IMemberValueResolver<object, object, string?, string>
{
    public string Resolve(object source, object destination, string? sourceMember, string destMember, ResolutionContext context)
        => sourceMember?.ToUpperInvariant() ?? string.Empty;
}
```

```csharp
CreateMap<Customer, CustomerDto>()
    .ForMember(d => d.CityCode, o => o.MapFrom<UpperCaseResolver, string?>(s => s.Address!.City))
    .ForMember(d => d.CountryCode, o => o.MapFrom<UpperCaseResolver, string?>("Country"));
```

Arayüzün `TSource` ve `TDestination` parametreleri kontravaryanttır (`in`); `object` ile tanımlanmış bir resolver her eşlemede kullanılabilir.

## Resolver örneği vermek

Resolver'ın durum taşıması gerekiyorsa veya DI kullanmıyorsanız bir örnek verebilirsiniz:

```csharp
public sealed class ConstantResolver<TSource, TDestination>(string value) : IValueResolver<TSource, TDestination, string>
{
    public string Resolve(TSource source, TDestination destination, string destMember, ResolutionContext context) => value;
}

CreateMap<Order, OrderDto>()
    .ForMember(d => d.Channel, o => o.MapFrom(new ConstantResolver<Order, OrderDto>("web")))
    .ForMember(d => d.Code, o => o.MapFrom(new UpperCaseResolver(), s => s.Code));
```

Türü çalışma zamanında belirlenen resolver'lar için `o.MapFrom(typeof(TenantNameResolver))` overload'u vardır.

## Resolver örnekleri nasıl oluşturulur

Tür olarak verilen resolver'lar (`MapFrom<TResolver>()`, `MapFrom(Type)`) her çağrıda şu sırayla çözülür:

1. Fabrika: çağrıya özel `opt.ConstructServicesUsing(...)` verilmişse o, yoksa mapper'ın fabrikası (`config.CreateMapper(type => ...)` veya `cfg.ConstructServicesUsing(...)`). Fabrika `null` döndürürse sonraki adıma geçilir.
2. DI konteyneri: `AddVeloxMapper` ile çözülen `IMapper`, bulunduğu scope'un `IServiceProvider`'ını kullanır.
3. DI varsa ama tür kayıtlı değilse: `ActivatorUtilities` ile kurucu enjeksiyonu yapılarak oluşturulur.
4. DI yoksa: parametresiz kurucu. Parametresiz kurucu da yoksa `VeloxMappingException` fırlatılır.

`AddVeloxMapper` ile taranan assembly'lerdeki tüm `IValueResolver` ve `IMemberValueResolver` uygulamaları transient olarak otomatik kaydedilir. Ayrıntılar için [Dependency Injection API](./api/dependency-injection.md) sayfasına bakın.

> [!TIP]
> DI kullanmadan test yazarken `config.CreateMapper(type => ...)` ile resolver'ları kendiniz oluşturabilirsiniz. Bu fabrika her resolver, converter ve mapping action türü için çağrılır.

## Çağrı parametrelerini okumak

Kullanıcıya, isteğe veya kültüre bağlı değerleri resolver'a `Items` ile aktarın:

```csharp
public sealed class LocalizedNameResolver : IValueResolver<Product, ProductDto, string>
{
    public string Resolve(Product source, ProductDto destination, string destMember, ResolutionContext context)
        => context.Items.TryGetValue("Culture", out var culture) && (string)culture == "en"
            ? source.NameEn
            : source.NameTr;
}

var dto = mapper.Map<ProductDto>(product, opt => opt.Items["Culture"] = "en");
```

`context.Items`'a seçenek verilmeden erişildiğinde boş bir sözlük döner; hata fırlatılmaz. Sözlüğün oluşturulmasını önlemek için `context.TryGetItems(out var items)` kullanabilirsiniz.

## Sınırlamalar

- Resolver'lar [ProjectTo](./projection.md) sorgularına çevrilemez. Resolver kullanan üyeler projeksiyondan çıkarılır ve `DiagnosticsSink`/`ILogger`'a bir uyarı yazılır.
- Resolver içinden `context.Mapper.Map(...)` çağırırsanız yeni bir eşleme bağlamı başlar; `Items` iç çağrıya aktarılmaz.

## AutoMapper uyumluluğu

`IValueResolver`, `IMemberValueResolver` imzaları ve tüm `MapFrom` resolver overload'ları AutoMapper ile aynıdır. VeloxMapper 5.x'ten gelen `IVeloxValueResolver` ve `IVeloxMemberValueResolver` arayüzleri de desteklenir; bkz. [Arayüzler](./api/interfaces.md#velox-5x-arayüzleri).

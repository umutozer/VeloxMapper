---
title: "Value Converter"
description: "Tek bir üye değerini başka bir türe çeviren, yeniden kullanılabilir dönüştürücüler yazın."
section: customization
order: 50
---

# Value Converter

Value converter, bir kaynak üye değerini hedef üyenin türüne çeviren küçük bir sınıftır. Resolver'dan farkı, kaynak ve hedef nesneyi görmemesidir: yalnızca üye değerini alır. Bu nedenle farklı eşlemelerde aynı dönüşümü (para birimi biçimlendirme, birim çevirme, maskeleme) tekrar kullanmak için uygundur.

## Ne zaman hangisi

| İhtiyaç | Önerilen |
| --- | --- |
| Tek bir üye değerini başka türe/biçime çevirmek | `IValueConverter<TSourceMember, TDestinationMember>` |
| Değer üretmek için kaynak nesnenin birden çok üyesi veya servis gerekiyor | [`IValueResolver`](./value-resolvers.md) |
| Bir türün her yerde aynı şekilde dönüşmesi (`string` → `DateTime`) | [Type Converter](./type-converters.md) |
| Bir türün tüm değerlerine son işlem (`Trim`) | [Value Transformer](./value-transformers.md) |

## Converter yazmak

```csharp title="CurrencyConverter.cs"
using System.Globalization;
using VeloxMapper;

public sealed class CurrencyConverter : IValueConverter<decimal, string>
{
    public string Convert(decimal sourceMember, ResolutionContext context)
        => sourceMember.ToString("N2", CultureInfo.GetCultureInfo("tr-TR")) + " TL";
}
```

## Converter'ı üyeye bağlamak

Converter'ı tür olarak (DI ile çözülür) veya örnek olarak verebilirsiniz. Kaynak üyeyi üç şekilde belirtirsiniz:

```csharp title="OrderProfile.cs"
CreateMap<Order, OrderDto>()
    // 1. Aynı adlı kaynak üye (Order.Total → OrderDto.Total)
    .ForMember(d => d.Total, o => o.ConvertUsing<CurrencyConverter, decimal>())
    // 2. İfadeyle seçilen kaynak üye
    .ForMember(d => d.DiscountText, o => o.ConvertUsing<CurrencyConverter, decimal>(s => s.Discount))
    // 3. Örnek + ifade
    .ForMember(d => d.ShippingText, o => o.ConvertUsing(new CurrencyConverter(), s => s.ShippingCost));
```

| Overload | Kaynak üye |
| --- | --- |
| `ConvertUsing<TConverter, TSourceMember>()` | Hedefle aynı adlı kaynak üye |
| `ConvertUsing<TConverter, TSourceMember>(s => s.Member)` | İfadeyle seçilen üye |
| `ConvertUsing<TConverter, TSourceMember>("Member")` | Adıyla verilen üye |
| `ConvertUsing(converter)` | Hedefle aynı adlı kaynak üye |
| `ConvertUsing(converter, s => s.Member)` | İfadeyle seçilen üye |
| `ConvertUsing(converter, "Member")` | Adıyla verilen üye |

Tür olarak verilen converter'lar, [resolver'larla aynı sırayla](./value-resolvers.md#resolver-örnekleri-nasıl-oluşturulur) oluşturulur. `AddVeloxMapper` ile taranan assembly'lerdeki `IValueConverter` uygulamaları transient olarak otomatik kaydedilir.

## Bağlamı kullanmak

`Convert` metodunun ikinci parametresi çağrı bağlamıdır. Kültür gibi çağrıya özel bilgileri `Items` ile aktarabilirsiniz:

```csharp
public sealed class LocalizedCurrencyConverter : IValueConverter<decimal, string>
{
    public string Convert(decimal sourceMember, ResolutionContext context)
    {
        var culture = context.Items.TryGetValue("Culture", out var name)
            ? CultureInfo.GetCultureInfo((string)name)
            : CultureInfo.InvariantCulture;
        return sourceMember.ToString("C", culture);
    }
}

var dto = mapper.Map<OrderDto>(order, opt => opt.Items["Culture"] = "tr-TR");
```

## Attribute ile kullanım

`[AutoMap]` ile tanımlanan eşlemelerde `[ValueConverter(typeof(CurrencyConverter))]` özniteliğini kullanabilirsiniz. Bkz. [Attribute ile Eşleme](./attribute-mapping.md).

## Sınırlamalar

Value converter'lar SQL'e çevrilemez. [ProjectTo](./projection.md) sorgularında converter kullanan üyeler projeksiyondan çıkarılır ve bir uyarı loglanır. Sorguda da çalışması gereken dönüşümler için ifade tabanlı `MapFrom` kullanın.

## AutoMapper uyumluluğu

`IValueConverter<TSourceMember, TDestinationMember>` arayüzü ve tüm `ConvertUsing` üye overload'ları AutoMapper ile aynıdır. VeloxMapper 5.x'ten gelen `IVeloxValueConverter` arayüzü de `ConvertUsing(converter, s => s.Member)` overload'u ile desteklenir.

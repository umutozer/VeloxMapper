---
title: "Type Converter"
description: "Bir tür çiftinin eşlemesini tamamen kendi kodunuza veya bir ifadeye devredin."
section: customization
order: 60
---

# Type Converter

Type converter, bir tür çiftinin eşlemesini üye üye konvansiyon yerine tek bir dönüşüm mantığına devreder. Bir kez tanımlandığında, bu tür çifti nerede geçerse (kök nesne, iç içe üye, koleksiyon öğesi) aynı dönüştürücü kullanılır.

## ConvertUsing biçimleri

| Biçim | Örnek | `ProjectTo` |
| --- | --- | --- |
| İfade | `.ConvertUsing(s => new Money(s.Amount, s.Currency))` | Evet |
| Fonksiyon | `.ConvertUsing((src, dest) => ...)` | Hayır |
| Bağlamlı fonksiyon | `.ConvertUsing((src, dest, ctx) => ...)` | Hayır |
| Örnek | `.ConvertUsing(new DateTimeTypeConverter())` | Hayır |
| Tür (DI ile çözülür) | `.ConvertUsing<DateTimeTypeConverter>()` | Hayır |
| Çalışma zamanı türü / open generic | `.ConvertUsing(typeof(PageConverter<>))` | Hayır |

Tek parametreli `ConvertUsing`, `Expression<Func<TSource, TDestination>>` alır; bu nedenle gövdesi tek bir ifade olan lambda yazmalısınız. Blok gövdeli (`{ ... }`) bir mantık gerekiyorsa iki parametreli fonksiyon overload'unu kullanın.

## İfade ile dönüştürmek

```csharp
CreateMap<string, Uri>().ConvertUsing(s => new Uri(s, UriKind.RelativeOrAbsolute));
CreateMap<MoneyEntity, Money>().ConvertUsing(s => new Money(s.Amount, s.Currency));
```

İfade tabanlı dönüştürücüler [ProjectTo](./projection.md) sorgularına da gömülür, sorgu sağlayıcısının çevirebildiği ifadeler kullandığınız sürece SQL'e dönüşür.

## ITypeConverter yazmak

Servis gerektiren veya birden çok satırlık dönüşümler için `ITypeConverter<TSource, TDestination>` uygulayın:

```csharp title="DateTimeTypeConverter.cs"
using System.Globalization;
using VeloxMapper;

public sealed class DateTimeTypeConverter : ITypeConverter<string?, DateTime>
{
    public DateTime Convert(string? source, DateTime destination, ResolutionContext context)
        => string.IsNullOrWhiteSpace(source)
            ? DateTime.MinValue
            : DateTime.Parse(source, CultureInfo.InvariantCulture);
}
```

```csharp title="ConvertersProfile.cs"
public class ConvertersProfile : Profile
{
    public ConvertersProfile()
    {
        CreateMap<string?, DateTime>().ConvertUsing<DateTimeTypeConverter>();
        CreateMap<ImportRow, Invoice>(); // ImportRow.IssuedAt (string) → Invoice.IssuedAt (DateTime)
    }
}
```

`CreateMap<string?, DateTime>()` tanımı global bir kural olur: `ImportRow` → `Invoice` eşlemesinde `string` türündeki `IssuedAt` üyesi de bu dönüştürücüyle `DateTime`'a çevrilir.

`Convert` metodunun `destination` parametresi, `Map(source, destination)` çağrısında mevcut hedeftir; yeni nesne oluşturulurken türün varsayılan değeridir.

> [!NOTE]
> Type converter tanımlı bir tür çiftinde kaynak `null` olsa bile dönüştürücü çağrılır. `null` girdiyi dönüştürücünün içinde ele alın.

## Fonksiyon ile dönüştürmek

```csharp
CreateMap<int, string>().ConvertUsing((src, dest) => "#" + src);

CreateMap<Price, string>().ConvertUsing((src, dest, ctx) =>
    src.Amount.ToString("C", CultureInfo.GetCultureInfo((string)ctx.Items["Culture"])));
```

## Open generic dönüştürücüler

Generic bir tür ailesi için tek dönüştürücü tanımlayabilirsiniz. Dönüştürücünün tür parametreleri eşlenen kapalı türlerden doldurulur:

```csharp
public sealed class PageConverter<T> : ITypeConverter<Page<T>, PageDto<T>>
{
    public PageDto<T> Convert(Page<T> source, PageDto<T> destination, ResolutionContext context)
        => new() { Items = source.Items, Total = source.TotalCount };
}

cfg.CreateMap(typeof(Page<>), typeof(PageDto<>)).ConvertUsing(typeof(PageConverter<>));
```

Ayrıntılar için [Open Generic](./open-generics.md) sayfasına bakın.

## Enum'dan enum'a özel eşleme

Enum'lar varsayılan olarak önce isme, bulunamazsa sayısal değere göre eşlenir. Bu davranışı bir tür çifti için değiştirmek üzere `ConvertUsingEnumMapping` kullanın:

```csharp
CreateMap<OrderStatus, OrderStatusDto>()
    .ConvertUsingEnumMapping(o => o
        .MapByName(ignoreCase: true)
        .MapValue(OrderStatus.Pending, OrderStatusDto.AwaitingPayment))
    .ReverseMap();
```

`MapValue` ile verilen özel eşlemeler `ReverseMap` ile tersine çevrilir. `ConvertUsingEnumMapping` AutoMapper.Extensions.EnumMapping paketindeki API ile aynıdır ve VeloxMapper'a dahildir. Ayrıntılar için [Enum Eşleme](./enums.md) sayfasına bakın.

## AutoMapper uyumluluğu

`ITypeConverter` arayüzü ve `ConvertUsing` overload'ları AutoMapper ile aynıdır. VeloxMapper 5.x'ten gelen `IVeloxTypeConverter` örnekleri de `ConvertUsing(converter)` ve `cfg.AddCustomConverter(converter)` ile kabul edilir.

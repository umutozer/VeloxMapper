---
title: "Eşleme Konvansiyonları"
description: "VeloxMapper'ın hangi kaynak üyeyi hangi hedef üyeye otomatik eşlediğini belirleyen ad eşleştirme, metot, ön ek ve isimlendirme kurallarını öğrenin."
section: core-concepts
order: 20
---

# Eşleme Konvansiyonları

`CreateMap<TSource, TDestination>()` tanımladığınızda VeloxMapper, her hedef üye için bir kaynak değeri konvansiyonlarla arar. Açık bir kural (`ForMember`, `ForPath`, öznitelik) yazmadığınız her üye bu sayfadaki kurallara göre eşlenir.

## Kaynak arama sırası

Bir hedef üye için kaynak şu sırayla aranır; ilk eşleşme kullanılır:

1. **Açık kural:** `ForMember`, `ForPath`, `[SourceMember]` gibi bir yapılandırma varsa o kullanılır.
2. **Aynı adlı üye:** Aynı adlı property, field veya parametresiz metot.
3. **Extension metot:** `IncludeSourceExtensionMethods` ile eklenen sınıflardaki uygun extension metot.
4. **Flattening:** `CustomerName` → `Customer.Name` gibi iç içe yol. Bkz. [Flattening](./flattening.md).
5. **IncludeMembers:** `IncludeMembers(s => s.Details)` ile eklenen alt nesnelerdeki üyeler.

Hiçbir kaynak bulunamazsa üye atanmaz ve [`AssertConfigurationIsValid()`](./configuration-validation.md) bu üyeyi "eşlenmemiş" olarak raporlar.

## Ad eşleştirme

Ad karşılaştırması **büyük/küçük harfe duyarsızdır**. `Email`, `email` ve `EMAIL` aynı üyeye eşlenir. Hem tam (büyük/küçük harf dahil) eşleşen hem de yalnızca harf duyarsız eşleşen iki kaynak üye varsa tam eşleşen tercih edilir.

```csharp
public class Customer
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
}

public class CustomerDto
{
    public int ID { get; set; }       // Id ile eşlenir
    public string EMAIL { get; set; } = "";  // Email ile eşlenir
}
```

## Hangi üyeler dikkate alınır

| Taraf | Dikkate alınan üyeler | Ayar |
| --- | --- | --- |
| Kaynak | Getter'ı olan ve getter'ı veya setter'ı public olan property'ler | `ShouldMapProperty` |
| Kaynak | Public field'lar | `ShouldMapField` |
| Kaynak | Değer döndüren, parametresiz public instance metotlar (`object` metotları hariç) | `ShouldMapMethod` |
| Hedef | Setter'ı olan property'ler (getter public ise private setter dahil) | `ShouldMapProperty` |
| Hedef | Setter'ı olmayan ama değiştirilebilir koleksiyon property'leri (yerinde doldurulur) | — |
| Hedef | `readonly` olmayan public field'lar | `ShouldMapField` |

Varsayılanları değiştirmek için yapılandırmada veya profilde ilgili ayarı verin:

```csharp
cfg.ShouldMapProperty = p => p.GetMethod?.IsPublic == true;
cfg.ShouldMapField = f => false; // field'ları hiç eşleme
```

## Get ön ekli metotlar

`Get` ile başlayan parametresiz bir kaynak metodu, ön eki olmayan hedef üyeye eşlenir:

```csharp
public class Order
{
    public List<OrderLine> Lines { get; set; } = new();
    public decimal GetTotal() => Lines.Sum(l => l.UnitPrice * l.Quantity);
}

public class OrderDto
{
    public decimal Total { get; set; }   // GetTotal() sonucuyla doldurulur
}
```

Metot adı `Get` ile tam olarak bu harflerle başlamalıdır (ör. `GetTotal`). Aynı profilde `ClearPrefixes()` çağrısı bu davranışı kapatır.

## Extension metotlar

Kaynak türünü genişleten extension metotlar da kaynak üye olarak kullanılabilir. Bunun için metotların bulunduğu sınıfı kaydedin:

```csharp
public static class CustomerExtensions
{
    public static string GetDisplayName(this Customer customer)
        => $"{customer.FirstName} {customer.LastName} <{customer.Email}>";
}
```

```csharp
var configuration = new MapperConfiguration(cfg =>
{
    cfg.IncludeSourceExtensionMethods(typeof(CustomerExtensions));
    cfg.CreateMap<Customer, CustomerSummaryDto>(); // DisplayName ← GetDisplayName()
});
```

Extension metot tek parametreli olmalı ve değer döndürmelidir. `Get` ön eki burada da tanınır.

## Ön ek ve son ekler

Üye adlarında sistematik ön veya son ekler varsa bunları tanıtın:

```csharp
cfg.RecognizePrefixes("str", "int");          // kaynak: strName → Name
cfg.RecognizePostfixes("Field");              // kaynak: NameField → Name
cfg.RecognizeDestinationPrefixes("dto");      // hedef: dtoName → Name
cfg.RecognizeDestinationPostfixes("Value");   // hedef: NameValue → Name
```

Ön ek eşleşmesi büyük/küçük harfe duyarsızdır. `ClearPrefixes()` tanımlı tüm kaynak ön eklerini ve `Get` metot ön ekini temizler.

## Üye adı değiştirme

Kaynak üye adlarında karakter veya metin değişimi gerekiyorsa `ReplaceMemberName` kullanın. Türkçe karakterli eski şemalarda kullanışlıdır:

```csharp
cfg.ReplaceMemberName("Ü", "U");   // kaynak Ünvan → hedef Unvan
cfg.ReplaceMemberName("ı", "i");
```

## Adlandırma kuralları

Kaynak ve hedef farklı adlandırma stilleri kullanıyorsa (ör. veritabanından gelen `snake_case` satırlar) isimlendirme kuralı tanımlayın:

```csharp title="LegacyImportProfile.cs"
public class LegacyImportProfile : Profile
{
    public LegacyImportProfile()
    {
        SourceMemberNamingConvention = new LowerUnderscoreNamingConvention();
        DestinationMemberNamingConvention = new PascalCaseNamingConvention();

        CreateMap<LegacyCustomerRow, Customer>(); // first_name → FirstName
    }
}
```

| Kural | Örnek ad |
| --- | --- |
| `PascalCaseNamingConvention` | `FirstName` |
| `LowerUnderscoreNamingConvention` | `first_name` |
| `ExactMatchNamingConvention` | Adı olduğu gibi kullanır |

Her kuralın paylaşılan bir örneği `Instance` özelliğinden alınabilir (ör. `LowerUnderscoreNamingConvention.Instance`). Kendi kuralınız için `VeloxMapper.Abstractions.ICustomNamingConvention` arayüzünü uygulayın.

## Global ignore

Adı belirli bir metinle başlayan hedef üyeleri tüm eşlemelerde yok saymak için:

```csharp
cfg.AddGlobalIgnore("Audit");   // Audit, AuditTrail, AuditedBy ... yok sayılır
```

Eşleşme bir **ön ek** eşleşmesidir; yalnızca tam adı yok saymak istiyorsanız daha belirgin bir metin verin veya `ForMember(..., o => o.Ignore())` kullanın.

## Tür dönüşümleri

Ad eşleşen ama türü farklı üyeler için yerleşik dönüşümler uygulanır:

| Kaynak | Hedef | Davranış |
| --- | --- | --- |
| Herhangi bir tür | `string` | `ToString()`; null kaynak için `null` |
| `string` | `int`, `decimal`, `bool`, `DateTime`, `Guid`, `TimeSpan`, `DateTimeOffset` ve diğer `IConvertible` türler | Ayrıştırma; boş veya null metin için türün varsayılan değeri |
| Sayısal tür | Başka sayısal tür | `System.Convert` (ör. `double` 2.5 → `int` 2, banker's rounding) |
| `T` | `T?` ve tersi | `Nullable` sarma/açma; null → varsayılan değer |
| Enum | Enum, `string`, tamsayı | Bkz. [Enum Eşleme](./enums.md) |
| Kullanıcı tanımlı tür | Hedef tür | Tanımlıysa `implicit` / `explicit` dönüşüm operatörü |
| Sınıf | Sınıf | İç içe eşleme ([Nested Mapping](./nested-mapping.md)) |
| Koleksiyon | Koleksiyon | Eleman eşleme ([Koleksiyonlar](./collections.md)) |

`string` ayrıştırmaları geçerli kültürü (`CultureInfo.CurrentCulture`) kullanır. Kültürden bağımsız bir dönüşüm gerekiyorsa global bir tür dönüştürücü tanımlayın:

```csharp
cfg.CreateMap<string, DateTime>()
    .ConvertUsing(s => DateTime.Parse(s, CultureInfo.InvariantCulture));
```

`ConvertUsing` ile tanımlanan tür dönüştürücüler iç içe üyeler dahil tüm eşlemelerde kullanılır. Bkz. [Type Converters](./type-converters.md).

## İlgili sayfalar

- [Flattening ve Unflattening](./flattening.md)
- [Üye Yapılandırması](./member-configuration.md)
- [Naming Conventions](./naming-conventions.md)

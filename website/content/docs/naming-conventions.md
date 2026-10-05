---
title: "İsimlendirme Kuralları"
description: "snake_case, önek/sonek ve özel isimlendirme kurallarına sahip modeller arasında konvansiyonla eşleme yapın."
section: customization
order: 90
---

# İsimlendirme Kuralları

Kaynak ve hedef üye adları farklı kurallarla yazılmışsa (veritabanından gelen `first_name` ile C# tarafındaki `FirstName` gibi) her üye için `ForMember` yazmak yerine bir isimlendirme kuralı tanımlarsınız. VeloxMapper üye adlarını karşılaştırmadan önce bu kurallara göre normalize eder.

## Eşleştirme nasıl yapılır

Bir kaynak üye ile hedef üye şu adımlarla karşılaştırılır:

1. Her iki taraf için aday adlar üretilir: özgün ad, `ReplaceMemberName` uygulanmış ad, tanınan önek ve sonekleri atılmış adlar.
2. İsimlendirme kuralı tanımlıysa her aday ayrıca kurala göre normalize edilir.
3. Kaynak ve hedef adaylarından herhangi ikisi **büyük/küçük harf duyarsız** eşitse üyeler eşleşir.

Karşılaştırma zaten büyük/küçük harf duyarsız olduğu için `customerId` ile `CustomerId` arasında kural tanımlamanız gerekmez.

## Hazır kurallar

| Sınıf | Davranış |
| --- | --- |
| `LowerUnderscoreNamingConvention` | `first_name` → `FirstName` |
| `PascalCaseNamingConvention` | Adı olduğu gibi kullanır (boşlukları atar). |
| `ExactMatchNamingConvention` | Adı değiştirmez. |

Her sınıfın tekil bir örneği `Instance` özelliğiyle de erişilebilir: `LowerUnderscoreNamingConvention.Instance`.

## snake_case kaynaktan PascalCase hedefe

```csharp title="LegacyProfile.cs"
public class LegacyProfile : Profile
{
    public LegacyProfile()
    {
        SourceMemberNamingConvention = new LowerUnderscoreNamingConvention();
        DestinationMemberNamingConvention = new PascalCaseNamingConvention();

        CreateMap<legacy_user, UserDto>(); // first_name → FirstName, user_age → UserAge
    }
}
```

Ters yön için kuralları yer değiştirin: kaynak `PascalCaseNamingConvention`, hedef `LowerUnderscoreNamingConvention`.

Profil düzeyindeki kural yalnızca o profilin eşlemelerine uygulanır. Global yapılandırmada `cfg.SourceMemberNamingConvention` ve `cfg.DestinationMemberNamingConvention` ile tüm profillere varsayılan verebilirsiniz; profilde verilen değer globali ezer.

VeloxMapper'a özgü `cfg.NamingConvention` özelliği, taraf bazlı bir kural verilmemişse her iki tarafa da aynı kuralı uygular.

## Önek ve sonekler

| Yöntem | Etki |
| --- | --- |
| `RecognizePrefixes("str", "m_")` | Kaynak üye adlarından önekleri atar: `strName` → `Name` |
| `RecognizePostfixes("Field")` | Kaynak üye adlarından sonekleri atar: `NameField` → `Name` |
| `RecognizeDestinationPrefixes(...)` | Hedef üye adlarından önekleri atar. |
| `RecognizeDestinationPostfixes(...)` | Hedef üye adlarından sonekleri atar. |
| `ClearPrefixes()` | Tanınan önekleri temizler; varsayılan `Get` önekini de kapatır. |

Varsayılan olarak parametresiz `GetX()` metotları `X` hedef üyesine eşlenir (`GetTotal()` → `Total`). Bu davranışı `ClearPrefixes()` ile kapatabilirsiniz.

```csharp
var config = new MapperConfiguration(cfg =>
{
    cfg.RecognizePrefixes("src");
    cfg.CreateMap<ImportRow, ProductDto>(); // srcTitle → Title
});
```

## Karakter değiştirme

`ReplaceMemberName`, karşılaştırmadan önce kaynak üye adlarında metin değiştirir. Birkaç karakteri veya kısaltmayı eşlemek için kullanışlıdır:

```csharp
cfg.ReplaceMemberName("Ü", "U");
cfg.ReplaceMemberName("Qty", "Quantity");
cfg.CreateMap<Personel, PersonnelDto>(); // Ünvan → Unvan, StockQty → StockQuantity
```

Çok sayıda değiştirme gerekiyorsa bir [özel kural](#özel-kural-yazmak) yazmak daha okunaklıdır.

## Özel kural yazmak

`VeloxMapper.Abstractions.ICustomNamingConvention` arayüzünü uygulayarak kendi kuralınızı yazabilirsiniz. `Normalize` metodu bir aday adı alır ve karşılaştırmada kullanılacak adı döndürür:

```csharp title="TurkishAsciiNamingConvention.cs"
using System.Text;
using VeloxMapper.Abstractions;

// Ünvan → Unvan, SicilNo → SicilNo, GörevYeri → GorevYeri
public sealed class TurkishAsciiNamingConvention : ICustomNamingConvention
{
    public string Normalize(string propertyName)
    {
        var builder = new StringBuilder(propertyName);
        builder.Replace('ç', 'c').Replace('Ç', 'C').Replace('ğ', 'g').Replace('Ğ', 'G')
               .Replace('ı', 'i').Replace('İ', 'I').Replace('ö', 'o').Replace('Ö', 'O')
               .Replace('ş', 's').Replace('Ş', 'S').Replace('ü', 'u').Replace('Ü', 'U');
        return builder.ToString();
    }
}
```

```csharp
public class PersonnelProfile : Profile
{
    public PersonnelProfile()
    {
        SourceMemberNamingConvention = new TurkishAsciiNamingConvention();
        CreateMap<Personel, PersonnelDto>();
    }
}
```

Normalize edilmiş ad, özgün adın yerine geçmez; aday listesine eklenir. Bu nedenle bir kural tanımlamak mevcut eşleşmeleri bozmaz.

## Üye keşfi

Hangi üyelerin eşlemeye katılacağını şu ayarlarla kontrol edebilirsiniz:

| Ayar | Varsayılan |
| --- | --- |
| `ShouldMapProperty` | Getter'ı veya setter'ı public olan property'ler |
| `ShouldMapField` | Public field'lar |
| `ShouldMapMethod` | Parametresiz, değer döndüren tüm public instance metotlar (kaynak tarafında) |
| `IncludeSourceExtensionMethods(typeof(T))` | Verilen statik sınıftaki extension metotları kaynak üye olarak kullanır: `GetFullName(this Customer c)` → `FullName` |

```csharp
cfg.ShouldMapField = f => false;                       // field'ları hiç eşleme
cfg.ShouldMapMethod = m => m.Name.StartsWith("Get");  // yalnızca GetX() metotları
```

## AutoMapper uyumluluğu

`SourceMemberNamingConvention`, `DestinationMemberNamingConvention`, hazır kural sınıfları, önek/sonek yöntemleri, `ReplaceMemberName`, `ShouldMapProperty/Field/Method` ve `IncludeSourceExtensionMethods` AutoMapper ile aynıdır. Farklar:

- AutoMapper'da özel kurallar `INamingConvention` arayüzüyle yazılır; VeloxMapper'da `ICustomNamingConvention` (`Normalize` metodu) kullanılır. Hazır kural sınıflarını kullanan kod değişiklik gerektirmez.
- `cfg.NamingConvention` (iki taraf için tek kural) yalnızca VeloxMapper'da vardır.

---
title: "Dictionary Eşleme"
description: "Sözlükleri tipli nesnelere ve sözlükleri sözlüklere eşleyin; nesneden sözlüğe dönüşüm için doğru yaklaşımı seçin."
section: advanced
order: 50
---

# Dictionary Eşleme

VeloxMapper iki sözlük senaryosunu yerleşik olarak destekler: bir sözlükten tipli bir nesneye eşleme ve bir sözlükten başka bir sözlüğe eşleme. Bu sayfa her ikisini ve desteklenmeyen ters yön için önerilen yaklaşımı anlatır.

## Sözlükten nesneye

`IDictionary` uygulayan bir kaynak (ör. `Dictionary<string, object>`), hedef nesnenin yazılabilir üyelerine anahtar adıyla eşlenir. `CreateMap` tanımı gerekmez:

```csharp
var values = new Dictionary<string, object>
{
    ["id"] = "42",                 // string → int
    ["Name"] = "Klavye",
    ["Stock"] = 15L,               // long → int
    ["Status"] = "active",         // string → enum (büyük/küçük harf duyarsız)
    ["Supplier"] = new Dictionary<string, object> { ["Name"] = "Acme" }
};

var product = mapper.Map<Product>(values);
// product.Id → 42, product.Stock → 15, product.Status → ProductStatus.Active, product.Supplier.Name → "Acme"
```

Kurallar:

| Konu | Davranış |
| --- | --- |
| Anahtar eşleşmesi | Önce tam eşleşme, sonra büyük/küçük harf duyarsız eşleşme |
| Eksik anahtar | Üye atlanır; hedefteki varsayılan değer korunur |
| Sayısal ve `string` değerler | `Convert.ChangeType` ile hedef türe çevrilir |
| Enum üyeler | `string` değer isimle (büyük/küçük harf duyarsız), sayısal değer değerle çevrilir |
| İç içe nesne üyeler | Değer bir sözlükse veya eşlenebilir bir nesneyse çalışma zamanında eşlenir |
| Hedef türün kurucusu | Parametresiz kurucu gereklidir |

> [!NOTE]
> Sayısal dönüşümlerde geçerli kültür kullanılır. `"3.5"` gibi ondalık metinlerin kültürden bağımsız çözülmesi gerekiyorsa değerleri sözlüğe sayı olarak koyun veya bir [type converter](./type-converters.md) tanımlayın.

Anahtarların büyük/küçük harf duyarsız çözülmesi, JSON veya form verisinden gelen sözlüklerde ek yapılandırma gerektirmez.

## Sözlükten sözlüğe

Kaynak ve hedef ikisi de sözlükse anahtarlar ve değerler ayrı ayrı eşlenir. Hedef olarak `Dictionary<,>`, `IDictionary<,>` ve `IReadOnlyDictionary<,>` desteklenir:

```csharp
var prices = new Dictionary<string, int> { ["TRY"] = 100 };
var asLong = mapper.Map<Dictionary<string, long>>(prices);

var byCode = new Dictionary<string, Product> { ["P1"] = new Product { Name = "Kalem" } };
IReadOnlyDictionary<string, ProductDto> dtos = mapper.Map<IReadOnlyDictionary<string, ProductDto>>(byCode);
```

Sözlük türündeki üyeler de aynı şekilde eşlenir; `Dictionary<string, int>` türündeki bir kaynak üye `IReadOnlyDictionary<string, long>` türündeki bir hedef üyeye doğrudan eşlenir.

## Nesneden sözlüğe

Bir nesne, üye adları anahtar olacak şekilde `Dictionary<string, object>`, `IDictionary<string, object>`, `IDictionary` veya `ExpandoObject` (`dynamic`) hedefine yapılandırma gerekmeden eşlenir. Değerler olduğu gibi kopyalanır; iç nesneler düzleştirilmez ve dönüştürülmez (AutoMapper ile aynı).

```csharp
using System.Dynamic;

var values = mapper.Map<Dictionary<string, object>>(product);
// values["Code"] == product.Code, values["Price"] == product.Price, values["Category"] == product.Category (aynı referans)

dynamic expando = mapper.Map<ExpandoObject>(product);
Console.WriteLine(expando.Code);
```

Hangi üyelerin dahil edileceğini `ShouldMapProperty` / `ShouldMapField` ile, anahtar adlarını ise bir type converter ile özelleştirebilirsiniz:

```csharp
CreateMap<Product, Dictionary<string, object?>>()
    .ConvertUsing((src, _, _) => new Dictionary<string, object?> { ["code"] = src.Code, ["price"] = src.Price });
```

## ProjectTo ile

Sözlük eşlemeleri [ProjectTo](./projection.md) sorgularında kullanılamaz; sorgu sağlayıcıları sözlük oluşturan ifadeleri SQL'e çeviremez.

## Doğrulama

Kaynak türü bir sözlük olan eşlemeler [AssertConfigurationIsValid](./configuration-validation.md) tarafından atlanır, çünkü anahtarlar ancak çalışma zamanında bilinir.

## AutoMapper uyumluluğu

Sözlükten nesneye, nesneden sözlüğe (`IDictionary<string, object>`, `ExpandoObject`/`dynamic`) ve sözlükten sözlüğe eşleme AutoMapper ile aynı sonucu verir. `ExpandoObject` kaynaklar da (yalnızca `IDictionary<string, object>` uygulayan türler) sözlük olarak okunur.

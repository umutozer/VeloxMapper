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

Bir nesneyi `Dictionary<string, object>`'e dönüştürme VeloxMapper 6.0.0'da yerleşik olarak desteklenmez; `mapper.Map<Dictionary<string, object>>(product)` çağrısı `VeloxConfigurationException` fırlatır. Bu dönüşümü bir type converter ile tanımlayın:

```csharp title="ObjectToDictionaryConverter.cs"
using System.Reflection;
using VeloxMapper;

public sealed class ObjectToDictionaryConverter<T> : ITypeConverter<T, Dictionary<string, object?>>
{
    private static readonly PropertyInfo[] Properties =
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead).ToArray();

    public Dictionary<string, object?> Convert(T source, Dictionary<string, object?> destination, ResolutionContext context)
        => Properties.ToDictionary(p => p.Name, p => p.GetValue(source));
}
```

```csharp
CreateMap<Product, Dictionary<string, object?>>().ConvertUsing(new ObjectToDictionaryConverter<Product>());

var values = mapper.Map<Dictionary<string, object?>>(product);
```

## ProjectTo ile

Sözlük eşlemeleri [ProjectTo](./projection.md) sorgularında kullanılamaz; sorgu sağlayıcıları sözlük oluşturan ifadeleri SQL'e çeviremez.

## Doğrulama

Kaynak türü bir sözlük olan eşlemeler [AssertConfigurationIsValid](./configuration-validation.md) tarafından atlanır, çünkü anahtarlar ancak çalışma zamanında bilinir.

## AutoMapper uyumluluğu

Sözlükten nesneye ve sözlükten sözlüğe eşleme AutoMapper ile aynı sonucu verir. Fark: AutoMapper bir nesneyi `IDictionary<string, object>` hedefine üye adlarıyla eşleyebilir; VeloxMapper 6.0.0'da bu yön için yukarıdaki gibi bir type converter gerekir. `ExpandoObject`/`dynamic` hedefler de desteklenmez.

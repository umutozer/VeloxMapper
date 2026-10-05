---
title: "Value Transformer"
description: "Belirli bir türdeki tüm hedef değerlere global, profil, eşleme veya üye düzeyinde son işlem uygulayın."
section: customization
order: 80
---

# Value Transformer

Value transformer, belirli bir türdeki her değere hedefe yazılmadan hemen önce uygulanan bir dönüşümdür. Tüm `string` değerleri kırpmak veya tüm `DateTime` değerlerini UTC olarak işaretlemek gibi kesişen kurallar için kullanılır.

## Tanımlama düzeyleri

Transformer'ları dört düzeyde tanımlayabilirsiniz. Hepsi `Expression<Func<T, T>>` alır.

```csharp title="Program.cs"
var config = new MapperConfiguration(cfg =>
{
    // Global: tüm profillerdeki tüm eşlemeler
    cfg.ValueTransformers.Add<string>(s => s.Trim());
    cfg.AddProfile<CatalogProfile>();
});
```

```csharp title="CatalogProfile.cs"
public class CatalogProfile : Profile
{
    public CatalogProfile()
    {
        // Profil: yalnızca bu profildeki eşlemeler
        ValueTransformers.Add<DateTime>(d => DateTime.SpecifyKind(d, DateTimeKind.Utc));

        CreateMap<Product, ProductDto>()
            // Eşleme: yalnızca Product → ProductDto
            .AddTransform<string>(s => s.Replace("\t", " "))
            // Üye: yalnızca bu hedef üye
            .ForMember(d => d.Sku, o => o.AddTransform(s => s.ToUpperInvariant()));
    }
}
```

## Uygulama sırası

Bir üyeye birden çok düzeyde transformer uygulanıyorsa sıra en özelden en genele doğrudur. Aynı düzeydeki transformer'lar eklenme sırasıyla zincirlenir.

1. Üye (`ForMember(..., o => o.AddTransform(...))`)
2. Eşleme (`CreateMap<,>().AddTransform<T>(...)`)
3. Profil (`ValueTransformers.Add<T>(...)` profil kurucusunda)
4. Global (`cfg.ValueTransformers.Add<T>(...)`)

Örneğin dört düzeyde de `s => s + "[düzey]"` tanımlıysa `"a"` değeri hedefe `"a[üye][eşleme][profil][global]"` olarak yazılır.

Bir transformer, hedef üyenin türü transformer türüne atanabiliyorsa uygulanır. `Add<object>(...)` gibi geniş bir tür seçmek tüm üyeleri etkiler; transformer'ları olabildiğince dar türlerle tanımlayın.

## Null değerler

Transformer'lar `null` değerlere uygulanmaz; `null` değer olduğu gibi hedefe yazılır. Bu sayede `s => s.Trim()` gibi bir transformer `null` kontrolü gerektirmez.

> [!NOTE]
> AutoMapper transformer'ı `null` değerle de çağırır. VeloxMapper'da `null` değerleri dönüştürmek istiyorsanız [NullSubstitute](./null-handling.md#nullsubstitute) kullanın.

## Transformer mı, converter mı?

| | Value transformer | [Value converter](./value-converters.md) / [Type converter](./type-converters.md) |
| --- | --- | --- |
| Girdi ve çıktı türü | Aynı (`T → T`) | Farklı olabilir |
| Kapsam | Bir türün tüm değerleri | Seçilen üye veya tür çifti |
| Amaç | Son işlem, normalizasyon | Tür veya biçim dönüşümü |

## ProjectTo ile kullanım
Transformer'lar `ProjectTo` sorgularında da uygulanır ve SQL'e çevrilir (ör. `Trim()` → `trim(...)`); `null` değerler korunur. Transformer gövdesinde yalnızca sorgu sağlayıcınızın çevirebildiği ifadeleri kullanın.

## AutoMapper uyumluluğu

`ValueTransformers.Add<T>`, `AddTransform<T>` ve üye düzeyinde `AddTransform` AutoMapper ile aynı imzalara ve uygulama sırasına sahiptir. Fark: VeloxMapper transformer'ları `null` değerlere uygulamaz.

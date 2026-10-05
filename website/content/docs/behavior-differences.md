---
title: "Davranış Farkları"
description: "AutoMapper ile VeloxMapper arasındaki bilinen çalışma zamanı farklarını, her birinin etkisini ve geçişte yapmanız gerekenleri inceleyin."
section: migration
order: 40
---

# Davranış Farkları

VeloxMapper, AutoMapper'ın belgelenmiş davranışlarının büyük çoğunluğunu aynen uygular. Bu sayfa, aynı kodun farklı sonuç verebildiği durumları listeler. Geçişten önce her maddeyi kod tabanınızla karşılaştırın.

## Özet

| # | Konu | AutoMapper | VeloxMapper | Etki |
| --- | --- | --- | --- | --- |
| 1 | [Tanımlanmamış tür çifti](#1-tanımlanmamış-tür-çiftleri) | "Missing type map" hatası | Konvansiyonla örtük eşler | Hata yerine sonuç |
| 2 | [`ProjectTo` ve resolver'lar](#2-projectto-içinde-resolver-ve-converter-kuralları) | Hata fırlatır | Üyeyi projeksiyondan çıkarır, uyarı loglar | Üye varsayılan değerde kalır |
| 3 | [Sorgu null yayılımı](#3-sorgularda-null-yayılımı) | Varsayılan kapalı | Varsayılan açık | Farklı SQL |
| 4 | [`Items` erişimi](#4-opts-olmadan-items-erişimi) | Seçenek yoksa hata | Boş sözlük | Hata yerine boş sözlük |
| 5 | [İç içe `context.Mapper.Map`](#5-resolver-içinden-contextmappermap) | Bağlamı paylaşır | Yeni bağlam başlatır | `Items` aktarılmaz |
| 6 | [Value transformer ve null](#6-value-transformerlar-ve-null) | Transformer çalışabilir | `null` korunur | Transformer çalışmaz |
| 7 | [Rekürsif `ProjectTo`](#7-rekürsif-modellerde-projectto) | Derinlik sınırı olmadan ifade üretir | `MaxDepth` ister | Açıklayıcı hata |
| 8 | [İstisnalar](#değişen-istisna-türleri-ve-mesajlar) | `AutoMapper*Exception` | `Velox*Exception`, Türkçe mesaj | `catch` blokları |
| 9 | [Desteklenmeyen API'ler](#desteklenmeyen-apiler) | Var | Yok | Elle yeniden yazım |

## 1. Tanımlanmamış tür çiftleri

**AutoMapper:** Kök veya iç içe bir tür çifti için `CreateMap` tanımı yoksa çalışma zamanında "Missing type map configuration or unsupported mapping" hatası fırlatır.

**VeloxMapper:** Tanımsız tür çiftlerini konvansiyonlarla (aynı ad, flattening, koleksiyon) örtük olarak eşler. `AssertConfigurationIsValid()` da bu iç içe örtük eşlemeleri hata saymaz.

```csharp
var configuration = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<Order, OrderDto>();
    // CreateMap<OrderLine, OrderLineDto>() tanımlanmadı
});

var dto = configuration.CreateMapper().Map<OrderDto>(order);
// AutoMapper: AutoMapperMappingException
// VeloxMapper: dto.Lines konvansiyonla eşlenir
```

**Ne yapmalısınız:** Bir testin "Missing type map" hatası beklediği durumlar varsa bu testi güncelleyin. Tüm iç içe türler için açık `CreateMap` tanımı tutmak istiyorsanız bunu kod incelemesinde uygulayın; `AssertConfigurationIsValid()` açıkça tanımlanmış eşlemelerin her üyesini yine doğrular.

## 2. ProjectTo içinde resolver ve converter kuralları

**AutoMapper:** `ProjectTo` sırasında `IValueResolver`, `IValueConverter` veya `MapFrom((src, dest) => ...)` gibi fonksiyon tabanlı kurallar sorguya çevrilemediği için hata fırlatır.

**VeloxMapper:** Bu üyeleri projeksiyondan çıkarır ve `Warning` seviyesinde bir teşhis kaydı yazar:

```text
ProjectTo: 'ViewedBy' üyesi özel resolver/converter kullandığı için projeksiyona dahil edilmedi.
```

Kayıt, `ILoggerFactory` varsa `VeloxMapper` kategorisine, `cfg.DiagnosticsSink` tanımlıysa ona gider. İlgili DTO üyesi varsayılan değerinde kalır.

**Ne yapmalısınız:** `ProjectTo` ile kullanılan eşlemelerde ifade tabanlı `MapFrom(s => ...)` kullanın. Bir üye yalnızca bellekte hesaplanabiliyorsa `ProjectTo` sonrasında ayrıca doldurun veya o sorgu için `Map` kullanın. Geçişten sonra logları `ProjectTo:` önekli uyarılar için tarayın.

## 3. Sorgularda null yayılımı

**AutoMapper:** `EnableNullPropagationForQueryMapping` varsayılan olarak `false`'tur.

**VeloxMapper:** Varsayılan `true`'dur. `ProjectTo` ifadesinde ara navigasyon üyeleri null-güvenli okunur (ör. `Customer` null ise `CustomerEmail` için `null` döner). Bu, bazı sağlayıcılarda üretilen SQL'e `CASE WHEN` ifadeleri ekleyebilir.

**Ne yapmalısınız:** Üretilen SQL'i [Geçiş Rehberi](./migration-guide.md#projectto-sorgularını-karşılaştırın)'ndeki gibi karşılaştırın. AutoMapper ile aynı ifadeyi üretmek için:

```csharp
cfg.EnableNullPropagationForQueryMapping = false;
```

## 4. opts olmadan Items erişimi

**AutoMapper:** `Map` çağrısında `opts` verilmeden bir resolver içinde `context.Items` okunursa hata fırlatır.

**VeloxMapper:** Boş bir sözlük oluşturup döndürür. `context.TryGetItems(out var items)` her iki kütüphanede de vardır ve sözlüğün daha önce oluşturulup oluşturulmadığını bildirir.

**Ne yapmalısınız:** Genellikle bir şey yapmanız gerekmez. `Items` erişiminin hata vermesine dayanan bir mantık varsa `TryGetItems` kullanın.

## 5. Resolver içinden context.Mapper.Map

**AutoMapper:** Bir resolver içinden `context.Mapper.Map(...)` çağrıldığında dış eşlemenin bağlamı (`Items` dahil) iç çağrıya aktarılır.

**VeloxMapper:** İç çağrı yeni bir bağlam başlatır; `Items` paylaşılmaz.

**Ne yapmalısınız:** İç eşlemenin `Items` değerlerine ihtiyacı varsa bunları açıkça aktarın:

```csharp
public string Resolve(Order source, OrderDto destination, string destMember, ResolutionContext context)
{
    var culture = (string)context.Items["culture"];
    var customer = context.Mapper.Map<CustomerDto>(source.Customer, opt => opt.Items["culture"] = culture);
    return customer.FullName;
}
```

Daha iyisi, iç nesneyi resolver yerine `ForMember(d => d.Customer, o => o.MapFrom(s => s.Customer))` ile eşlemektir; bu durumda bağlam paylaşılır.

## 6. Value transformer'lar ve null

**AutoMapper:** `ValueTransformers` ile tanımlanan dönüşüm, değer `null` olsa da çalışabilir.

**VeloxMapper:** Transformer'lar `null` değerlere uygulanmaz; `null` olduğu gibi korunur.

```csharp
cfg.ValueTransformers.Add<string>(s => s.Trim());
// Kaynak null ise hedef null kalır; s.Trim() çağrılmaz.
```

**Ne yapmalısınız:** `null` değeri başka bir değere çeviren bir transformer kullanıyorsanız (ör. `s => s ?? ""`), bunun yerine üye düzeyinde `NullSubstitute("")` veya `ForAllPropertyMaps` ile toplu `NullSubstitute` kullanın.

## 7. Rekürsif modellerde ProjectTo

**AutoMapper:** Kendine referans veren modellerde (`Category.Parent`, `Employee.Manager`) `MaxDepth` tanımlanmamışsa derinlik sınırı olmadan ifade üretmeye çalışır.

**VeloxMapper:** Açıklayıcı bir `VeloxProjectionException` fırlatır:

```text
Category -> CategoryDto projeksiyonu kendini tekrar eden (rekürsif) bir model içeriyor.
Sonsuz sorgu üretimini önlemek için CreateMap(...).MaxDepth(n) tanımlayın.
```

**Ne yapmalısınız:** Eşlemeye `MaxDepth` ekleyin:

```csharp
CreateMap<Category, CategoryDto>().MaxDepth(3);
```

Bellek içi `Map` çağrılarında bu sınır gerekmez: döngüsel tür grafiklerinde `PreserveReferences` otomatik açılır ve `StackOverflowException` oluşmaz.

## Değişen istisna türleri ve mesajlar

| Durum | AutoMapper | VeloxMapper |
| --- | --- | --- |
| Eşleme sırasında hata (ör. resolver istisnası) | `AutoMapperMappingException` | `VeloxMappingException` |
| `AssertConfigurationIsValid()` başarısız | `AutoMapperConfigurationException` | `VeloxValidationException` |
| Aynı tür çifti iki kez tanımlandı | `DuplicateTypeMapConfigurationException` | `VeloxConfigurationException` |
| Hedef türün kurucusu seçilemedi | `AutoMapperMappingException` / `AutoMapperConfigurationException` | `VeloxAmbiguousConstructorException` |
| `ProjectTo` ifadesi üretilemedi | `AutoMapperMappingException` | `VeloxProjectionException` |

Tüm VeloxMapper istisnaları `VeloxMapper.Exceptions` namespace'indeki `VeloxException` sınıfından türer. Mesajlar Türkçedir ve tür ile üye bilgisini içerir; örneğin bir resolver hata fırlattığında:

```text
Eşleştirme hatası: Order -> OrderDto (üye: ViewedBy). InvalidOperationException: ...
```

Asıl istisna `InnerException` özelliğindedir. `VeloxException` türevleri sarmalanmaz; doğrudan fırlatılır.

**Ne yapmalısınız:** `catch` bloklarını yeni türlere güncelleyin ([geçiş betiği](./migration-script.md) bunu otomatik yapar). İstisna **mesajını** karşılaştıran testler veya kodlar varsa güncelleyin; mesaj metinleri sürümler arasında sabit kabul edilmemelidir.

## Desteklenmeyen API'ler

| API | Kaynak | Alternatif |
| --- | --- | --- |
| `UseAsDataSource(...)` | `AutoMapper.Extensions.ExpressionMapping` | DTO üzerinden sorgu yazmak yerine varlık üzerinde filtreleyip `ProjectTo` kullanın. |
| `EqualityComparison(...)` | `AutoMapper.Collection` | Koleksiyonlar Clear + Add ile eşlenir. Anahtara göre güncelleme için [Mevcut Nesneye Eşleme](./map-to-existing.md#koleksiyonları-anahtara-göre-güncelleme) sayfasındaki deseni kullanın. |
| `cfg.Internal()` | AutoMapper iç API'si | `cfg.ForAllMaps(...)` ve `cfg.ForAllPropertyMaps(...)` |
| Arayüz proxy'leri (`AsProxy`) | AutoMapper | Hedef soyut tür veya arayüz ise somut bir tür belirtin (`As<T>()`, `Include`, `ConstructUsing`). `[AutoMap(..., AsProxy = true)]` derlenir ancak proxy üretmez. |

[Geçiş betiği](./migration-script.md) bu kullanımları bulduğunda uyarı verir.

## Aynı kalan davranışlar

Aşağıdaki davranışlar AutoMapper ile aynıdır ve VeloxMapper test paketinde doğrulanmıştır:

- Büyük/küçük harfe duyarsız ad eşleştirme, çok seviyeli flattening, `GetX()` metotlarının `X` üyesine eşlenmesi
- `ReverseMap` ile `MapFrom` kurallarının ve flattening'in tersine çevrilmesi
- Enum eşleme: önce ada, bulunamazsa sayısal değere göre
- Yerleşik tür dönüşümleri (string, sayısal türler, `Nullable`, implicit/explicit operatörler)
- `Map(source, destination)` ile iç nesnelerin ve koleksiyon örneklerinin korunması
- Kurucu seçimi, record desteği, `ForCtorParam`
- `Include` / `IncludeBase` / `IncludeAllDerived` ile kalıtım ve çalışma zamanı türüne göre seçim
- `Condition` üçüncü parametresi ile PATCH deseni
- Profil düzeyinde konvansiyon ayarları

Ayrıntılar için [Temel Kavramlar](./configuration.md) bölümüne bakın.

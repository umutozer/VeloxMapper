---
title: "İstisnalar"
description: "VeloxMapper'ın fırlattığı istisna türleri, hangi durumda fırlatıldıkları ve AutoMapper karşılıkları."
section: api
order: 80
---

# İstisnalar

VeloxMapper'ın kendi istisnaları `VeloxMapper.Exceptions` namespace'indedir ve ortak bir taban sınıftan türer. Mesajlar Türkçedir; tür çifti, profil ve üye adı gibi hatayı bulmanızı sağlayan bilgileri içerir.

## Hiyerarşi

```text
System.Exception
└── VeloxException (abstract)
    ├── VeloxMappingException
    ├── VeloxConfigurationException
    │   └── VeloxAmbiguousConstructorException
    ├── VeloxValidationException
    └── VeloxProjectionException
```

Tüm VeloxMapper hatalarını tek yerde yakalamak için `VeloxException` kullanın:

```csharp
using VeloxMapper.Exceptions;

try
{
    var dto = mapper.Map<Order, OrderDto>(order);
}
catch (VeloxException ex)
{
    logger.LogError(ex, "Sipariş {OrderId} eşlenemedi", order.Id);
    throw;
}
```

## Ne zaman hangisi

| İstisna | Ne zaman | Tipik neden |
| --- | --- | --- |
| `VeloxMappingException` | `Map` çağrısı sırasında | Resolver, converter, `MapFrom` fonksiyonu veya bir dönüşüm hata fırlattı; soyut hedef için `Include` eşlemesi yok; resolver örneği oluşturulamadı. |
| `VeloxConfigurationException` | `MapperConfiguration` oluşturulurken veya bir tür çifti ilk kez derlenirken | Aynı tür çifti iki kez tanımlandı; tür çifti eşlenemiyor; `ForPath` yolu geçersiz; `NullSubstitute` değeri dönüştürülemiyor; dönüştürücü beklenen arayüzü uygulamıyor. |
| `VeloxAmbiguousConstructorException` | Bir tür çifti ilk kez derlenirken | Hedef türün hiçbir kurucusu kaynaktan çözülemiyor veya birden fazla `[VeloxConstructor]` var. |
| `VeloxValidationException` | `AssertConfigurationIsValid()` çağrısında | Eşlenmemiş hedef/kaynak üyeler, çözülemeyen kurucular, dönüştürülemeyen üye türleri. |
| `VeloxProjectionException` | `ProjectTo` ifadesi üretilirken | Rekürsif model için `MaxDepth` yok; `ITypeConverter` veya fonksiyon tabanlı `ConvertUsing`; soyut hedef türü; hedef kurucusu çözülemiyor. |

## VeloxMappingException

```csharp
public class VeloxMappingException : VeloxException
```

Eşleme sırasında oluşan beklenmeyen bir hata, tür çifti ve üye bilgisiyle sarılır. Asıl hata `InnerException`'dadır:

```text
VeloxMappingException: Eşleştirme hatası: Order -> OrderDto (üye: CustomerName). NullReferenceException: Object reference not set to an instance of an object.
```

`MapFrom` ifadelerindeki ara üyeler null-güvenli olduğu için bu hata genellikle fonksiyon tabanlı `MapFrom`, resolver veya converter içindeki koddan gelir.

AutoMapper karşılığı: `AutoMapperMappingException`.

## VeloxConfigurationException

```csharp
public class VeloxConfigurationException : VeloxException
```

Yapılandırmanın tutarsız veya eksik olduğunu bildirir. İki aşamada fırlatılabilir:

- **`MapperConfiguration` kurucusunda:** aynı tür çiftinin iki kez `CreateMap` ile tanımlanması.

  ```text
  'MyApp.Order' -> 'MyApp.OrderDto' eşleştirmesi birden fazla kez tanımlanmış ([MyApp.OrderProfile] ve [Global]). Her tür çifti yalnızca bir kez tanımlanabilir.
  ```

- **Tür çifti ilk kez derlenirken** (ilk `Map` çağrısı veya `CompileMappings()`):

  ```text
  'MyApp.Order' türü 'System.Guid' türüne eşlenemiyor. Bir CreateMap tanımı veya ConvertUsing ile özel dönüştürücü ekleyin.
  ```

AutoMapper karşılığı: `DuplicateTypeMapConfigurationException` (tekrarlanan tanım) ve `AutoMapperConfigurationException`'ın bazı kullanımları.

## VeloxAmbiguousConstructorException

```csharp
public class VeloxAmbiguousConstructorException : VeloxConfigurationException
{
    public Type? TargetType { get; }
}
```

Hedef nesne için kullanılabilir bir kurucu seçilemediğinde fırlatılır. `TargetType`, kurucusu seçilemeyen türdür.

```text
'MyApp.Invoice' için parametreleri kaynak 'MyApp.InvoiceRow' türünden çözülebilen bir kurucu bulunamadı. Kurucular: (Guid id). ForCtorParam, ConstructUsing veya parametresiz kurucu kullanın.
```

Çözüm: `ForCtorParam` ile eksik parametreleri bağlayın, `ConstructUsing` ile fabrika verin veya kullanılacak kurucuyu `[VeloxConstructor]` ile işaretleyin. `VeloxConfigurationException`'dan türediği için onu yakalayan kod bu hatayı da yakalar.

VeloxMapper'a özgü.

## VeloxValidationException

```csharp
public class VeloxValidationException : VeloxException
```

`AssertConfigurationIsValid()` bir veya daha fazla sorun bulduğunda fırlatılır. Mesaj tüm sorunları profil ve tür çifti bazında listeler. Örnek çıktı için [Yapılandırma Doğrulama](../configuration-validation.md#hata-çıktısı).

AutoMapper karşılığı: `AutoMapperConfigurationException`.

## VeloxProjectionException

```csharp
public class VeloxProjectionException : VeloxException
```

`ProjectTo` ifadesi üretilemediğinde, sorgu sağlayıcısına gönderilmeden önce fırlatılır:

```text
Category -> CategoryDto projeksiyonu kendini tekrar eden (rekürsif) bir model içeriyor. Sonsuz sorgu üretimini önlemek için CreateMap(...).MaxDepth(n) tanımlayın.
```

```text
Order -> OrderDto: ProjectTo yalnızca ifade tabanlı ConvertUsing(src => ...) dönüştürücülerini destekler. ITypeConverter veya fonksiyon tabanlı dönüştürücüler sorguya çevrilemez.
```

Projeksiyon ifadesi üretildikten sonra sorgu sağlayıcısının (EF Core) verdiği çeviri hataları (`InvalidOperationException` gibi) sarılmaz; sağlayıcının kendi istisnası olarak gelir.

VeloxMapper'a özgü. AutoMapper bu durumların bir kısmında `AutoMapperMappingException` fırlatır.

## VeloxMapper dışı istisnalar

| İstisna | Durum |
| --- | --- |
| `ArgumentException` | `ForMember` iç içe yol aldı (`ForPath` kullanın); resolver/converter türü beklenen arayüzü uygulamıyor; `ConvertUsingEnumMapping` enum olmayan türlerde; `ProjectTo`/`Mapper`'a `MapperConfiguration` olmayan bir `IConfigurationProvider` verildi. |
| `ArgumentNullException` | Zorunlu bir parametre `null` (yapılandırma delegesi, tür, assembly listesi...). |

## AutoMapper'dan geçiş

| AutoMapper | VeloxMapper |
| --- | --- |
| `AutoMapperMappingException` | `VeloxMappingException` |
| `AutoMapperConfigurationException` | `VeloxValidationException` |
| `DuplicateTypeMapConfigurationException` | `VeloxConfigurationException` |

[Otomatik geçiş betiği](../migration-script.md) `catch` bloklarındaki bu adları otomatik olarak değiştirir. `ex.Types`, `ex.MemberMap` gibi AutoMapper istisnalarına özgü özellikleri okuyan kod varsa bu bilgileri istisna mesajından alacak şekilde güncellemeniz gerekir.

---
title: "Constructor ve Record"
description: "VeloxMapper'ın hedef türün kurucusunu nasıl seçtiğini öğrenin; record'lar, ForCtorParam, ConstructUsing ve [VeloxConstructor] ile oluşturmayı kontrol edin."
section: core-concepts
order: 60
---

# Constructor ve Record

Hedef türün parametresiz kurucusu yoksa veya değerleri kurucu üzerinden almak istiyorsanız VeloxMapper parametreleri kaynaktan çözerek uygun kurucuyu çağırır. Positional record'lar ve değişmez (immutable) türler ek yapılandırma olmadan desteklenir.

## Record'lar

```csharp
public class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
}

public record CustomerRecord(int Id, string FirstName, string LastName, string Email);
```

```csharp
cfg.CreateMap<Customer, CustomerRecord>();

var record = mapper.Map<CustomerRecord>(customer);
```

Kurucu parametreleri kaynak üyelerle **ada göre** (büyük/küçük harfe duyarsız) eşlenir. Flattening, tür dönüşümleri ve iç içe eşlemeler kurucu parametreleri için de geçerlidir.

## Kurucu seçim kuralları

VeloxMapper hedef türün public kurucularını şu sırayla değerlendirir:

1. **`[VeloxConstructor]`:** Bu öznitelikle işaretli bir kurucu varsa o kullanılır. Birden fazla kurucu işaretlenirse hata oluşur.
2. **En çok parametreli çözülebilir kurucu:** Kurucular parametre sayısına göre büyükten küçüğe sıralanır. **Tüm** parametreleri çözülebilen ilk kurucu seçilir.
3. **Parametresiz kurucu:** Parametreli kurucuların hiçbiri çözülemezse (veya hiç yoksa) parametresiz kurucu kullanılır.

Bir parametre şu kaynaklardan çözülebilir (öncelik sırasıyla):

| Kaynak | Örnek |
| --- | --- |
| `ForCtorParam` kuralı | `.ForCtorParam("fullName", o => o.MapFrom(s => s.FirstName + " " + s.LastName))` |
| Aynı adlı hedef üyenin `MapFrom` ifadesi | `.ForMember(d => d.FullName, o => o.MapFrom(...))` ve kurucuda `fullName` parametresi |
| Konvansiyon | Aynı adlı kaynak üye, `Get` metodu, flattening |
| Parametrenin varsayılan değeri | `string? note = null` |

Kurucu ile atanan üyeler, kurucu çağrısından sonra tekrar atanmaz. Kurucuda olmayan yazılabilir üyeler ise normal şekilde atanır.

```csharp
public class CustomerSummary
{
    public CustomerSummary() { }

    public CustomerSummary(int id, string email)
    {
        Id = id;
        Email = email;
    }

    public int Id { get; }
    public string Email { get; } = "";
    public string? FirstName { get; set; }  // kurucuda yok: nesne oluşturulduktan sonra atanır
}
```

Bu örnekte `(int id, string email)` kurucusunun iki parametresi de kaynaktan çözülebildiği için parametresiz kurucu yerine bu kurucu seçilir.

## ForCtorParam

Adı kaynakla eşleşmeyen veya hesaplanması gereken parametreler için `ForCtorParam` kullanın:

```csharp
public record CustomerCard(int Id, string DisplayName, string Email);
```

```csharp
cfg.CreateMap<Customer, CustomerCard>()
    .ForCtorParam("displayName", opt => opt.MapFrom(s => s.FirstName + " " + s.LastName));
```

| Overload | Açıklama |
| --- | --- |
| `MapFrom(s => ...)` | İfade. `ProjectTo` ile de çalışır. |
| `MapFrom((s, context) => ...)` | `ResolutionContext` erişimi olan fonksiyon (ör. `context.Items`). `ProjectTo` ile çalışmaz. |
| `MapFrom("Customer.Email")` | Metin olarak üye yolu. |

Parametre adı büyük/küçük harfe duyarsız karşılaştırılır. Record'larda `nameof(CustomerCard.DisplayName)` kullanabilirsiniz.

## ConstructUsing

Nesnenin nasıl oluşturulacağını tamamen kendiniz belirlemek için `ConstructUsing` kullanın. Fabrika metodu olan veya kurucusu karmaşık türler için uygundur:

```csharp
public class Shipment
{
    private Shipment(int orderId, string carrier) { OrderId = orderId; Carrier = carrier; }

    public static Shipment Create(int orderId, string carrier) => new(orderId, carrier.ToUpperInvariant());

    public int OrderId { get; }
    public string Carrier { get; }
    public string? TrackingNumber { get; set; }
}
```

```csharp
cfg.CreateMap<ShipmentRequest, Shipment>()
    .ConstructUsing(src => Shipment.Create(src.OrderId, src.Carrier));
// TrackingNumber, nesne oluşturulduktan sonra konvansiyonla atanır
```

`ResolutionContext` gerekiyorsa `ConstructUsing((src, context) => ...)` overload'unu kullanın. Oluşturulan nesnenin yazılabilir üyeleri ardından normal kurallarla atanır; fabrikada zaten atadığınız üyeleri `Ignore()` ile işaretleyin.

DI kapsayıcısından örnek almak için `ConstructUsingServiceLocator()` kullanın; hedef tür kapsayıcıda kayıtlı olmalıdır.

## [VeloxConstructor]

Otomatik seçim, parametreleri çözülebilen en geniş kurucuyu kullanır. Farklı bir kurucunun kullanılmasını istiyorsanız onu öznitelikle işaretleyin:

```csharp
using VeloxMapper.Attributes;

public class Invoice
{
    // İçe aktarma senaryoları için; otomatik seçim bunu tercih ederdi
    public Invoice(int orderId, string number)
    {
        OrderId = orderId;
        Number = number;
    }

    // Eşlemede her zaman bu kurucu kullanılır; numara burada üretilir
    [VeloxConstructor]
    public Invoice(int orderId)
    {
        OrderId = orderId;
        Number = $"INV-{orderId:D6}";
    }

    public int OrderId { get; }
    public string Number { get; }
}
```

İşaretli kurucunun kaynaktan çözülemeyen parametreleri, parametre türünün varsayılan değerini (`default`) alır.

`[VeloxConstructor]` VeloxMapper'a özgüdür; AutoMapper'da karşılığı yoktur. Paketle gelen analyzer, birden fazla `[VeloxConstructor]` kullanımını (`VM001`, hata) ve belirsiz kurucu seçimini (`VM002`, uyarı) derleme sırasında raporlar.

## Kurucu eşlemesini kapatma

Tüm eşlemelerde yalnızca parametresiz kurucuları kullanmak için:

```csharp
cfg.DisableConstructorMapping();
```

Bu ayar profil düzeyinde de verilebilir. Hedef türün parametresiz kurucusu yoksa eşleme `VeloxAmbiguousConstructorException` ile başarısız olur.

Değerlendirilecek kurucuları filtrelemek için `ShouldUseConstructor` kullanın:

```csharp
cfg.ShouldUseConstructor = ctor => !ctor.IsDefined(typeof(ObsoleteAttribute), false);
```

## Doğrulama

`AssertConfigurationIsValid()`, kurucusu çözülemeyen hedef türleri de raporlar. Örneğin `ForCtorParam` olmadan `CreateMap<Customer, CustomerCard>()` tanımlandığında doğrulama şu maddeleri listeler:

```text
1. [Global] Customer -> CustomerCard (Destination üye listesi)
     - 'Shop.CustomerCard' için parametreleri kaynak 'Shop.Customer' türünden çözülebilen bir kurucu bulunamadı. Kurucular: (Int32 Id, String DisplayName, String Email). ForCtorParam, ConstructUsing veya parametresiz kurucu kullanın.
     - 'DisplayName' (String): eşleşen kaynak üye bulunamadı.
```

Belirli bir eşlemede kurucu doğrulamasını kapatmak için `DisableCtorValidation()` kullanın. Bu yalnızca kurucu denetimini kapatır; üye doğrulaması (yukarıdaki ikinci madde) devam eder.

## Hata durumları

Hiçbir kurucu çözülemediğinde eşleme sırasında `VeloxAmbiguousConstructorException` fırlatılır. Mesaj, hedef türün kurucularını listeler:

```text
'Shop.CustomerCard' için parametreleri kaynak 'Shop.Customer' türünden çözülebilen bir kurucu bulunamadı. Kurucular: (Int32 Id, String DisplayName, String Email). ForCtorParam, ConstructUsing veya parametresiz kurucu kullanın.
```

Hedef tür soyut bir sınıf veya arayüz ise doğrudan oluşturulamaz. `Include`, `As<T>()` veya `ConstructUsing` ile somut bir tür belirtin (bkz. [Kalıtım](./inheritance.md)).

## Struct'lar

Değer türleri (`struct`) de hedef olabilir. Çözülebilir bir parametreli kurucu yoksa varsayılan değerle (`new T()`) oluşturulur ve üyeleri atanır.

## İlgili sayfalar

- [Inheritance](./inheritance.md)
- [Yapılandırma Doğrulama](./configuration-validation.md)
- [Öznitelikler API referansı](./api/attributes.md)

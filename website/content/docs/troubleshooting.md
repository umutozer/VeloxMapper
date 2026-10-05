---
title: "Sorun Giderme"
description: "VeloxMapper'da sık karşılaşılan hataları istisna türü ve mesajıyla tanıyın; her birinin nedenini ve çözümünü bulun."
section: resources
order: 10
---

# Sorun Giderme

Bu sayfa VeloxMapper'ın fırlattığı istisnaları ve sık karşılaşılan sorunları, gerçek hata mesajlarıyla birlikte listeler. Tüm VeloxMapper istisnaları `VeloxMapper.Exceptions` namespace'indeki `VeloxException` sınıfından türer ve mesajları Türkçedir.

## Hızlı başvuru

| İstisna | Ne zaman | Bölüm |
| --- | --- | --- |
| `VeloxValidationException` | `AssertConfigurationIsValid()` | [Yapılandırma doğrulaması başarısız](#yapılandırma-doğrulaması-başarısız) |
| `VeloxConfigurationException` | `MapperConfiguration` oluşturulurken veya eşleme kodu üretilirken | [Aynı tür çifti birden fazla kez tanımlanmış](#aynı-tür-çifti-birden-fazla-kez-tanımlanmış), [Tür eşlenemiyor](#tür-eşlenemiyor) |
| `VeloxAmbiguousConstructorException` | Hedef nesne oluşturulurken | [Kurucu seçilemiyor](#kurucu-seçilemiyor) |
| `VeloxProjectionException` | `ProjectTo` ifadesi üretilirken | [ProjectTo hataları](#projectto-hataları) |
| `VeloxMappingException` | Eşleme çalışırken | [Eşleme sırasında hata](#eşleme-sırasında-hata), [Resolver örneği oluşturulamıyor](#resolver-örneği-oluşturulamıyor) |
| `ArgumentException` | `CreateMap` zincirinde hatalı kullanım | [ForMember iç içe yol kabul etmiyor](#formember-iç-içe-yol-kabul-etmiyor) |

## Yapılandırma doğrulaması başarısız

**Belirti:** `AssertConfigurationIsValid()` çağrısı `VeloxValidationException` fırlatır.

```text
VeloxMapper yapılandırma doğrulaması başarısız. Aşağıdaki eşleştirmeleri gözden geçirin:

  1. [Global] Customer -> CustomerDto (Destination üye listesi)
       - 'FullName' (String): eşleşen kaynak üye bulunamadı.

Çözüm: eksik üyeler için ForMember(d => d.Uye, o => o.MapFrom(...)) veya o.Ignore() kullanın; kaynak doğrulaması için ForSourceMember(...).DoNotValidate(), doğrulamayı kapatmak için ValidateMemberList(MemberList.None).
```

**Neden:** Mesajdaki her madde bir eşlemedir: köşeli parantez içinde profil adı, ardından tür çifti ve doğrulanan üye listesi. Altındaki satırlar sorunlu üyelerdir. Tüm sorunlar tek istisnada listelenir.

**Çözüm:** Her üye için uygun olanı seçin:

| Durum | Çözüm |
| --- | --- |
| Üye farklı bir kaynaktan gelmeli | `.ForMember(d => d.FullName, o => o.MapFrom(s => s.FirstName + " " + s.LastName))` |
| Üye bu eşlemede doldurulmamalı | `.ForMember(d => d.FullName, o => o.Ignore())` veya `.Ignore(d => d.FullName)` |
| Üye konvansiyonla doldurulur ama doğrulama dışında kalmalı | `.ForMember(d => d.FullName, o => o.DoNotValidate())` |
| `MemberList.Source` ile kullanılmayan kaynak üye | `.ForSourceMember(s => s.Password, o => o.DoNotValidate())` |
| Eşleme hiç doğrulanmamalı | `CreateMap<A, B>(MemberList.None)` |

Yalnızca bir profili doğrulamak için `AssertConfigurationIsValid<OrderProfile>()` kullanın. Profil adını mesajdaki köşeli parantezden okuyabilirsiniz.

## Aynı tür çifti birden fazla kez tanımlanmış

**Belirti:** `new MapperConfiguration(...)` veya DI'dan ilk `IMapper` çözümlemesi `VeloxConfigurationException` fırlatır.

```text
'Shop.Order' -> 'Shop.OrderDto' eşleştirmesi birden fazla kez tanımlanmış ([Shop.OrderProfile] ve [Shop.ReportingProfile]). Her tür çifti yalnızca bir kez tanımlanabilir.
```

**Neden:** Aynı kaynak/hedef tür çifti için iki `CreateMap` çağrısı var. Mesaj, tanımların bulunduğu profilleri gösterir. Sık nedenler: bir eşlemenin iki profile kopyalanması veya `[AutoMap]` özniteliği ile tanımlanan bir çift için ayrıca `CreateMap` yazılması.

**Çözüm:** Tanımlardan birini kaldırın. Farklı senaryolar için farklı kurallar gerekiyorsa farklı hedef türler (ör. `OrderDto` ve `OrderReportDto`) kullanın.

## Kurucu seçilemiyor

**Belirti:** `VeloxAmbiguousConstructorException`.

```text
'Shop.CustomerCard' için parametreleri kaynak 'Shop.Customer' türünden çözülebilen bir kurucu bulunamadı. Kurucular: (Int32 Id, String DisplayName, String Email). ForCtorParam, ConstructUsing veya parametresiz kurucu kullanın.
```

**Neden:** Hedef türün parametresiz kurucusu yok ve hiçbir kurucunun tüm parametreleri kaynaktan çözülemiyor. Mesajda listelenen kurucuların parametre adlarını kaynak üyelerle karşılaştırın.

**Çözüm:**

- Çözülemeyen parametre için `ForCtorParam("displayName", o => o.MapFrom(s => ...))` ekleyin.
- Nesneyi kendiniz oluşturmak için `ConstructUsing(s => new CustomerCard(...))` kullanın.
- Birden fazla kurucu varsa kullanılacak olanı `[VeloxConstructor]` ile işaretleyin.

Aynı istisna şu mesajlarla da görülebilir:

| Mesaj | Çözüm |
| --- | --- |
| `... türünde birden fazla [VeloxConstructor] kurucusu var; yalnızca birini işaretleyin.` | Özniteliği tek kurucuda bırakın. |
| `... türünün parametresiz kurucusu yok ve kurucu eşleştirmesi kapalı (DisableConstructorMapping).` | `DisableConstructorMapping()` ayarını kaldırın veya `ConstructUsing` kullanın. |

Ayrıntılar: [Constructor ve Record](./constructors.md).

## Soyut hedef türü için Include bulunamadı

**Belirti:** `VeloxMappingException`.

```text
'PaymentDto' soyut hedef türü için 'CardPayment' kaynağına uygun bir Include<,>() eşleştirmesi bulunamadı.
```

**Neden:** Hedef tür soyut bir sınıf veya arayüz; kaynak nesnenin çalışma zamanı türü için türetilmiş bir eşleme tanımlanmamış.

**Çözüm:** Türetilmiş eşlemeyi tanımlayıp tabana bağlayın:

```csharp
cfg.CreateMap<Payment, PaymentDto>()
    .Include<CardPayment, CardPaymentDto>();
cfg.CreateMap<CardPayment, CardPaymentDto>();
```

Ayrıntılar: [Inheritance](./inheritance.md).

## Tür eşlenemiyor

**Belirti:** `VeloxConfigurationException`.

```text
'Shop.Customer' türü 'System.Int32' türüne eşlenemiyor. Bir CreateMap tanımı veya ConvertUsing ile özel dönüştürücü ekleyin.
```

**Neden:** İki tür arasında yerleşik bir dönüşüm yok ve konvansiyonla eşlenebilecek bir yapı da bulunamadı (ör. bir sınıftan bir değer türüne).

**Çözüm:** Tür çifti için bir dönüştürücü tanımlayın:

```csharp
cfg.CreateMap<Customer, int>().ConvertUsing(c => c.Id);
```

## ProjectTo hataları

### Rekürsif model

```text
Category -> CategoryDto projeksiyonu kendini tekrar eden (rekürsif) bir model içeriyor. Sonsuz sorgu üretimini önlemek için CreateMap(...).MaxDepth(n) tanımlayın.
```

**Çözüm:** `CreateMap<Category, CategoryDto>().MaxDepth(3)` gibi bir derinlik sınırı ekleyin. `MaxDepth` sorguda kaç seviye `JOIN` üretileceğini belirler; ihtiyacınız olan en küçük değeri seçin.

### Fonksiyon tabanlı tür dönüştürücü

```text
Customer -> CustomerDto: ProjectTo yalnızca ifade tabanlı ConvertUsing(src => ...) dönüştürücülerini destekler. ITypeConverter veya fonksiyon tabanlı dönüştürücüler sorguya çevrilemez.
```

**Neden:** Projeksiyondaki bir tür çifti `ConvertUsing<TConverter>()`, `ConvertUsing((src, dest) => ...)` veya `ITypeConverter` ile tanımlanmış. Bunlar SQL'e çevrilemez.

**Çözüm:** Tek parametreli, ifade gövdeli bir `ConvertUsing(src => new CustomerDto { ... })` kullanın veya projeksiyon için ayrı bir hedef tür tanımlayın.

### ProjectTo sonucunda bir üye boş geliyor

**Belirti:** `Map` ile dolu gelen bir üye `ProjectTo` sonucunda varsayılan değerde kalıyor. Loglarda şu uyarı var:

```text
warn: VeloxMapper[0]
      ProjectTo: 'ViewedBy' üyesi özel resolver/converter kullandığı için projeksiyona dahil edilmedi.
```

**Neden:** Üye bir `IValueResolver`, `IValueConverter` veya `MapFrom((src, dest) => ...)` gibi fonksiyon tabanlı bir kural kullanıyor. Bu kurallar sorguya çevrilemediği için VeloxMapper üyeyi projeksiyondan çıkarır (AutoMapper bu durumda hata fırlatır).

**Çözüm:** Kuralı ifade tabanlı `MapFrom(s => ...)` olarak yazın veya üyeyi sorgu sonrası bellekte doldurun. Bkz. [Davranış Farkları](./behavior-differences.md#2-projectto-içinde-resolver-ve-converter-kuralları).

## Eşleme sırasında hata

**Belirti:** `VeloxMappingException`.

```text
Eşleştirme hatası: Order -> OrderAuditDto (üye: ViewedBy). InvalidOperationException: Kullanıcı bulunamadı
```

**Neden:** Eşleme sırasında kullanıcı kodundan (resolver, converter, `MapFrom` ifadesi, `Get` metodu, kurucu) bir istisna fırlatıldı. VeloxMapper bunu tür çifti ve üye adıyla sarmalar. Asıl istisna `InnerException` özelliğindedir.

**Çözüm:** `InnerException` ve yığın izini (stack trace) inceleyin; hata genellikle mesajdaki üyenin kaynağındadır. Sık görülen bir örnek, kaynakta `null` olan bir koleksiyon üzerinde çalışan bir `Get` metodudur (`GetTotal()` içinde `Lines.Sum(...)`).

VeloxMapper'ın kendi istisnaları (`VeloxException` türevleri) sarmalanmaz; doğrudan fırlatılır.

## Resolver örneği oluşturulamıyor

### Parametresiz kurucu yok

```text
'Shop.CurrentUserResolver' örneği oluşturulamadı: parametresiz kurucu yok. Türü DI konteynerine kaydedin (AddVeloxMapper ile taranan assembly'lerdeki resolver/converter'lar otomatik kaydedilir) veya ConstructServicesUsing kullanın.
```

**Neden:** Resolver'ın kurucusu parametre alıyor ama `IMapper` bir DI kapsayıcısı olmadan oluşturulmuş (ör. testte `configuration.CreateMapper()`).

**Çözüm:**

- Uygulamada `IMapper`'ı DI'dan alın ve resolver'ın bulunduğu assembly'yi `AddVeloxMapper(typeof(...))` ile taratın.
- Testlerde resolver'ı oluşturan bir fabrika verin:

```csharp
var mapper = configuration.CreateMapper(type =>
    type == typeof(CurrentUserResolver) ? new CurrentUserResolver(new FakeCurrentUser()) : Activator.CreateInstance(type)!);
```

### Scoped servis kök sağlayıcıdan çözülemiyor

```text
Eşleştirme hatası: Order -> OrderAuditDto (üye: ViewedBy). InvalidOperationException: Cannot resolve 'Shop.CurrentUserResolver' from root provider because it requires scoped service 'Shop.ICurrentUser'.
```

**Neden:** Resolver scoped bir servise (ör. `DbContext`, istek bağlamı) bağlı, ancak `IMapper` kök `IServiceProvider` üzerinden çözülmüş. Bu genellikle `IMapper`'ın bir **singleton** servise enjekte edilmesiyle veya `app.Services.GetRequiredService<IMapper>()` çağrısıyla olur.

**Çözüm:** `IMapper`'ı scoped veya transient servislere enjekte edin. Singleton bir servisin (ör. `BackgroundService`) eşleme yapması gerekiyorsa her iş birimi için bir kapsam açın:

```csharp
public class OrderExportWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
        // ...
    }
}
```

`IMapper` transient olarak kaydedilir ve çözüldüğü kapsamın sağlayıcısını taşır; resolver'lar bu kapsamdan oluşturulur. Bkz. [Dependency Injection](./dependency-injection.md#servis-ömürleri).

## ForMember iç içe yol kabul etmiyor

```text
ForMember yalnızca hedef türün doğrudan üyelerini kabul eder ('d => d.Customer.Email'). İç içe hedef yolları için ForPath kullanın. (Parameter 'destinationMember')
```

**Çözüm:** `ForPath(d => d.Customer.Email, o => o.MapFrom(s => s.CustomerEmail))` kullanın. Bkz. [Flattening ve Unflattening](./flattening.md#forpath-ile).

## ForMember ile verilen üye adı bulunamıyor

```text
'Shop.CustomerDto' türünde 'RowVersion' adında bir hedef üye bulunamadı. (Parameter 'name')
```

**Neden:** `ForMember("RowVersion", ...)` gibi metin tabanlı bir çağrı, hedefte olmayan bir üyeyi gösteriyor. Bu durum genellikle tüm eşlemelere uygulanan `ForAllMaps` içinde görülür.

**Çözüm:** Üyenin varlığını önce denetleyin: `if (typeMap.DestinationType.GetProperty("RowVersion") is not null) ...`.

## Geçiş sonrası derleme hataları

| Hata | Neden | Çözüm |
| --- | --- | --- |
| `CS0246: 'AutoMapper' bulunamadı` | Güncellenmemiş bir `using` (takma ad, `using static`, Razor dosyası) | Satırı `VeloxMapper` olarak değiştirin. |
| `CS0104` / `CS0121` belirsizlik | AutoMapper paketi hâlâ referans ediliyor (doğrudan veya transitif) | `dotnet list package --include-transitive` ile bulup kaldırın. |
| `'Internal'`, `'UseAsDataSource'`, `'EqualityComparison'` tanımlı değil | Desteklenmeyen API | Bkz. [Davranış Farkları](./behavior-differences.md#desteklenmeyen-apiler). |
| `CS8198` / `CS0834` (ifade ağacı hataları) `ConvertUsing` veya `ValueTransformers.Add` içinde | Bu API'ler `Expression<Func<...>>` alır; lambda ifade gövdeli olmalı | Blok gövdeli lambdaları ifadeye çevirin veya `ConvertUsing((src, dest) => ...)` overload'unu kullanın. |

Geçiş sürecinin tamamı için [Geçiş Rehberi](./migration-guide.md)'ne bakın.

## Teşhis araçları

Bir eşlemenin neden beklediğiniz gibi çalışmadığını anlamak için:

- **Debug logları:** Log seviyesini `VeloxMapper` kategorisi için `Debug` yapın. Her tür çifti için eşleme kodu üretildiğinde `Map ifadesi üretiliyor: Order -> OrderDto` gibi bir kayıt düşer.
- **Eşleme planı:** `configuration.GetMappingPlan(typeof(Order), typeof(OrderDto))` her hedef üyenin nereden geldiğini (doğrudan, flattening, yok sayılan, eşlenmemiş) gösterir. Bkz. [Diagnostics](./diagnostics.md).
- **Üretilen ifade:** `configuration.BuildExecutionPlan(typeof(Order), typeof(OrderDto))` derlenen `LambdaExpression` nesnesini döndürür; hata ayıklayıcıda inceleyebilirsiniz.

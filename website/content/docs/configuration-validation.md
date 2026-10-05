---
title: "Yapılandırma Doğrulama"
description: "AssertConfigurationIsValid ile eşlenmemiş üyeleri ve çözülemeyen kurucuları bir birim testinde, üretime çıkmadan yakalayın."
section: validation-performance
order: 10
---

# Yapılandırma Doğrulama

Konvansiyonla çalışan bir mapper'da en sık hata, bir DTO'ya yeni üye eklenip eşleme kuralının unutulmasıdır: kod derlenir, üye sessizce varsayılan değerde kalır. `AssertConfigurationIsValid()` tüm eşlemeleri tarar ve bu tür sorunları tek bir istisnada listeler. Bu çağrıyı bir birim testine koymanız önerilir.

## Test içinde doğrulama

<!-- tabs -->
```csharp title="xUnit"
using VeloxMapper;
using Xunit;

public class MappingConfigurationTests
{
    [Fact]
    public void Mapping_configuration_is_valid()
    {
        var config = new MapperConfiguration(cfg => cfg.AddMaps(typeof(OrderProfile).Assembly));

        config.AssertConfigurationIsValid();
    }
}
```
```csharp title="NUnit"
using NUnit.Framework;
using VeloxMapper;

[TestFixture]
public class MappingConfigurationTests
{
    [Test]
    public void Mapping_configuration_is_valid()
    {
        var config = new MapperConfiguration(cfg => cfg.AddMaps(typeof(OrderProfile).Assembly));

        config.AssertConfigurationIsValid();
    }
}
```
<!-- /tabs -->

Uygulamanız yapılandırmayı DI ile oluşturuyorsa testte de aynı kaydı kullanarak gerçek yapılandırmayı doğrulayabilirsiniz:

```csharp
var services = new ServiceCollection();
services.AddVeloxMapper(typeof(OrderProfile));
using var provider = services.BuildServiceProvider();

provider.GetRequiredService<IConfigurationProvider>().AssertConfigurationIsValid();
```

> [!TIP]
> Doğrulamayı uygulama başlangıcında değil test aşamasında çalıştırın. Doğrulama her eşleme için ifade üretir; büyük yapılandırmalarda başlangıç süresine eklenir. Testte yakalanan bir hata üretime hiç ulaşmaz.

## Neler denetlenir

| Denetim | Örnek hata |
| --- | --- |
| Eşleşen kaynağı olmayan hedef üyeler | `'AddressCity' (String): eşleşen kaynak üye bulunamadı.` |
| Hiçbir kurucusu kaynaktan çözülemeyen hedef türler | `'MyApp.Invoice' için parametreleri kaynak 'MyApp.InvoiceRow' türünden çözülebilen bir kurucu bulunamadı. Kurucular: (Guid id). ...` |
| Kaynak türden hedef türe dönüştürülemeyen üyeler | `'Total': Money türü Decimal türüne dönüştürülemiyor. CreateMap<Money, Decimal>().ConvertUsing(...) ekleyin.` |
| `MemberList.Source` ile hiçbir hedef üyeye eşlenmeyen kaynak üyeler | `Kaynak üye 'PasswordHash' (String) hiçbir hedef üyeye eşlenmiyor.` |

Doğrulama, çalışma zamanı motoruyla aynı çözümleme kurallarını kullanır: flattening, önek/sonekler, isimlendirme kuralları, `IncludeMembers`, `ForPath`, kurucu parametreleri ve global ignore'lar hesaba katılır.

## Hata çıktısı

Tüm hatalar profil, tür çifti ve üye bazında gruplanarak tek bir `VeloxValidationException` içinde raporlanır:

```text
VeloxMapper yapılandırma doğrulaması başarısız. Aşağıdaki eşleştirmeleri gözden geçirin:

  1. [Global] Customer -> CustomerExportDto (Source üye listesi)
       - Kaynak üye 'Address' (Address) hiçbir hedef üyeye eşlenmiyor.
       - Kaynak üye 'Orders' (List<Order>) hiçbir hedef üyeye eşlenmiyor.
  2. [MyApp.Mapping.OrderProfile] Order -> OrderDto (Destination üye listesi)
       - 'CustomerEmail' (String): eşleşen kaynak üye bulunamadı.
       - 'ShippingLabel' (String): eşleşen kaynak üye bulunamadı.

Çözüm: eksik üyeler için ForMember(d => d.Uye, o => o.MapFrom(...)) veya o.Ignore() kullanın; kaynak doğrulaması için ForSourceMember(...).DoNotValidate(), doğrulamayı kapatmak için ValidateMemberList(MemberList.None).
```

Köşeli parantez içindeki ad, eşlemenin tanımlandığı profildir (`ProfileName`; varsayılan olarak profil sınıfının tam adı). Profil dışında tanımlanan eşlemeler `[Global]` olarak gösterilir.

## Hataları düzeltmek

| Durum | Çözüm |
| --- | --- |
| Üye başka bir kaynaktan gelmeli | `ForMember(d => d.X, o => o.MapFrom(s => ...))` |
| Üye bilinçli olarak eşlenmiyor | `ForMember(d => d.X, o => o.Ignore())` veya `.Ignore(d => d.X)` |
| Üye konvansiyonla eşleniyor ama doğrulama yanlış alarm veriyor | `o.DoNotValidate()` |
| Tür dönüşümü eksik | `CreateMap<TKaynak, THedef>().ConvertUsing(...)` |
| Kurucu çözülemiyor | `ForCtorParam`, `ConstructUsing` veya `[VeloxConstructor]` |
| Belirli bir önekle başlayan tüm üyeler (`Audit*`) | `cfg.AddGlobalIgnore("Audit")` |

## MemberList: hangi taraf doğrulanır

`CreateMap` ikinci parametre olarak bir `MemberList` alır:

| Değer | Denetlenen |
| --- | --- |
| `MemberList.Destination` (varsayılan) | Her hedef üyenin bir kaynağı olmalı. |
| `MemberList.Source` | Her kaynak üye bir hedef üyeye eşlenmeli. Komut/istek nesnelerinden varlığa eşlemelerde, gönderilen alanın kaybolmadığından emin olmak için kullanılır. |
| `MemberList.None` | Üye doğrulaması yapılmaz (kurucu doğrulaması yapılır). |

```csharp
CreateMap<CreateCustomerCommand, Customer>(MemberList.Source);

// Eşleme tanımlandıktan sonra da değiştirilebilir
CreateMap<Customer, CustomerExportDto>().ValidateMemberList(MemberList.None);
```

### Kaynak üyelerini doğrulamak

`MemberList.Source` ile bir kaynak üyeyi bilinçli olarak dışarıda bırakmak için `ForSourceMember` kullanın:

```csharp
CreateMap<CreateCustomerCommand, Customer>(MemberList.Source)
    .ForSourceMember(s => s.CaptchaToken, o => o.DoNotValidate())
    .ForSourceMember("ClientTimestamp", o => o.DoNotValidate());
```

Getter'ı public olmayan kaynak property'leri toplu olarak dışarıda bırakmak için `IgnoreAllSourcePropertiesWithAnInaccessibleSetter()` kullanılabilir. Parametresiz kaynak metotları (`GetTotal()`) kaynak doğrulamasına dahil edilmez.

## DoNotValidate ve Ignore

| | `Ignore()` | `DoNotValidate()` |
| --- | --- | --- |
| Üye eşlenir mi? | Hayır | Evet (konvansiyonla eşlenebiliyorsa) |
| Doğrulamada hata verir mi? | Hayır | Hayır |

## Kurucu doğrulamasını kapatmak

Hedef nesne her zaman `ConstructUsing` veya DI ile oluşturuluyorsa ya da kurucu çözümü yalnızca çalışma zamanında mümkünse kurucu denetimini kapatın:

```csharp
CreateMap<InvoiceRow, Invoice>().DisableCtorValidation();
```

`DisableCtorValidation()` yalnızca kurucu denetimini kapatır; üye doğrulaması devam eder. `ConstructUsing`, `ConstructUsingServiceLocator` tanımlı veya hedef türü soyut olan eşlemelerde kurucu zaten denetlenmez.

## Profil bazında doğrulama

Büyük uygulamalarda her profili ayrı bir testte doğrulamak, hatanın hangi modülden geldiğini netleştirir:

```csharp
[Theory]
[InlineData(typeof(OrderProfile))]
[InlineData(typeof(CustomerProfile))]
public void Profile_is_valid(Type profileType)
{
    var config = new MapperConfiguration(cfg => cfg.AddProfile(profileType));
    config.AssertConfigurationIsValid();
}

// veya tek yapılandırma içinde
config.AssertConfigurationIsValid<OrderProfile>();
config.AssertConfigurationIsValid("Raporlama"); // CreateProfile("Raporlama", ...) ile tanımlanan profil
```

## Neler doğrulanmaz

| Durum | Neden |
| --- | --- |
| `ConvertUsing` ile tanımlı eşlemeler | Eşleme tamamen dönüştürücüye devredilmiştir. |
| Kaynak türü sözlük olan eşlemeler | Anahtarlar çalışma zamanında bilinir. |
| Open generic tanımlar | Üyeler ancak kapalı türlerle bilinir. |
| `ReverseMap()` ile üretilen ters eşlemeler | Varsayılan olarak `MemberList.None`; gerekirse `.ReverseMap().ValidateMemberList(MemberList.Destination)`. |
| Hedef türü basit tür olan eşlemeler (`CreateMap<int, string>()`) | Üye listesi yoktur. |
| `CreateMap` ile tanımlanmamış, konvansiyonla örtük eşlenen tür çiftleri | Kayıt yoktur; yalnızca açık eşlemelerin üyeleri denetlenir. |

Son madde AutoMapper'dan önemli bir farktır: AutoMapper, `CreateMap` tanımı olmayan iç içe bir tür çifti için "Missing type map" hatası verir; VeloxMapper bu çifti konvansiyonla eşler ve doğrulama bunu hata saymaz. İç içe türlerin de doğrulanmasını istiyorsanız onlar için açık `CreateMap` tanımları yazın. Bkz. [Davranış Farkları](./behavior-differences.md).

## Yapılandırma oluşturulurken alınan hatalar

Bazı hatalar `AssertConfigurationIsValid` çağrılmadan, `MapperConfiguration` oluşturulurken fırlatılır:

| Hata | İstisna |
| --- | --- |
| Aynı tür çifti iki kez `CreateMap` ile tanımlandı | `VeloxConfigurationException` |
| `ForMember` iç içe bir yol aldı (`d => d.Customer.Name`) | `ArgumentException` (`ForPath` kullanın) |
| Resolver/converter türü beklenen arayüzü uygulamıyor | `ArgumentException` |

İstisna türleri için [İstisnalar](./api/exceptions.md) sayfasına bakın.

## AutoMapper uyumluluğu

`AssertConfigurationIsValid()`, `AssertConfigurationIsValid<TProfile>()`, `AssertConfigurationIsValid(string profileName)`, `MemberList`, `ValidateMemberList`, `ForSourceMember(...).DoNotValidate()` ve `DisableCtorValidation()` AutoMapper ile aynıdır. Farklar:

- İstisna türü `AutoMapperConfigurationException` yerine `VeloxMapper.Exceptions.VeloxValidationException`'dır.
- Hata mesajları Türkçedir ve tüm hataları tek mesajda listeler.
- Örtük (CreateMap'siz) iç içe tür çiftleri hata sayılmaz.

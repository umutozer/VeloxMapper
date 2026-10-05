---
title: "Genel Bakış"
description: "VeloxMapper'ın ne olduğunu, kimler için tasarlandığını ve AutoMapper ile uyumluluğun somut olarak ne anlama geldiğini öğrenin."
section: getting-started
order: 10
---

# Genel Bakış

VeloxMapper, .NET için MIT lisanslı bir nesne eşleme (object mapping) kütüphanesidir. AutoMapper ile aynı tür ve metot adlarını kullanır; mevcut AutoMapper kodu paket ve `using` satırları değiştirilerek VeloxMapper ile derlenir.

## VeloxMapper nedir

Bir nesneyi başka bir türe dönüştürmek (ör. EF Core varlığını API'nin döndürdüğü DTO'ya çevirmek) çoğu uygulamada tekrarlanan, hataya açık bir iştir. VeloxMapper bu dönüşümü konvansiyonlara dayanarak yapar: aynı adlı üyeler otomatik eşlenir, `CustomerName` gibi düzleştirilmiş adlar `Customer.Name` yolundan okunur, koleksiyonlar ve iç içe nesneler kendiliğinden eşlenir. Konvansiyonun yetmediği yerde `ForMember`, `MapFrom`, `ConvertUsing` gibi tanıdık API'lerle kural yazarsınız.

```csharp title="OrderProfile.cs"
using VeloxMapper;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerName,
                opt => opt.MapFrom(s => s.Customer.FirstName + " " + s.Customer.LastName));
    }
}
```

```csharp
OrderDto dto = mapper.Map<OrderDto>(order);
```

## Kimler için

- **AutoMapper kullanan ekipler.** AutoMapper 2025'te ticari lisans modeline geçti; 15 ve sonraki sürümler bir lisans anahtarı ister. VeloxMapper aynı API'yi MIT lisansıyla sunar. Lisans maliyeti veya lisans yönetimi nedeniyle alternatif arayan ekipler kodlarını yeniden yazmadan geçiş yapabilir.
- **Yeni projeler.** Ekibiniz AutoMapper API'sini zaten biliyorsa VeloxMapper'da öğrenme maliyeti yoktur. Bunun yanı sıra derleme zamanı Source Generator, eşleme planı teşhisleri ve mevcut nesneye kısmi güncelleme (PATCH) seçenekleri gibi ek özellikler sunar.

## "Uyumlu" ne demek

Uyumluluk, kodunuzun **kaynak düzeyinde** aynı kalması demektir:

| Kalan | Değişen |
| --- | --- |
| `IMapper`, `Mapper`, `MapperConfiguration`, `IConfigurationProvider`, `Profile` | NuGet paketi: `AutoMapper` → `VeloxMapper` |
| `CreateMap`, `ForMember`, `MapFrom`, `ReverseMap`, `ConvertUsing`, `Include` ... | `using AutoMapper;` → `using VeloxMapper;` |
| `IValueResolver`, `IMemberValueResolver`, `IValueConverter`, `ITypeConverter`, `IMappingAction` | `using AutoMapper.QueryableExtensions;` → `using VeloxMapper.QueryableExtensions;` |
| `ResolutionContext`, `opt.Items`, `BeforeMap` / `AfterMap` | `AddAutoMapper(...)` → `AddVeloxMapper(...)` (eski ad da derlenir) |
| `ProjectTo`, `AssertConfigurationIsValid`, `[AutoMap]`, `[Ignore]` | İstisna türlerinin adları (ör. `AutoMapperMappingException` → `VeloxMappingException`) |

Bu değişikliklerin tamamını [otomatik geçiş betiği](./migration-script.md) tek komutla uygular.

AutoMapper 13.0.1 ile yazılmış bir örnek uygulama (DI, scoped resolver, value converter, EF Core `ProjectTo`, `ReverseMap`, PATCH senaryosu, `Items`) betik çalıştırıldıktan sonra **başka hiçbir değişiklik yapılmadan** VeloxMapper 6.0.0 ile derlendi ve çıktıları birebir aynıydı.

Uyumluluk her davranışın aynı olduğu anlamına gelmez. Bilinen farklar (ör. tanımlanmamış tür çiftlerinin konvansiyonla eşlenmesi, `ProjectTo` içinde resolver kullanan üyelerin atlanması) [Davranış Farkları](./behavior-differences.md) sayfasında tek tek listelenmiştir. Geçiş yapmadan önce bu sayfayı okuyun.

## Mimari

VeloxMapper iki katmanlı bir motor kullanır:

1. **Çalışma zamanı ifade derleme (varsayılan).** Bir tür çifti ilk kez eşlendiğinde VeloxMapper yapılandırmadan bir Expression Tree üretir, derler ve sonucu `MapperConfiguration` üzerinde önbelleğe alır. Aynı yapılandırmadan oluşturulan tüm `IMapper` örnekleri bu derlenmiş delegeleri paylaşır. İlk çağrı maliyetini uygulama başlangıcına almak için `CompileMappings()` kullanabilirsiniz.
2. **Derleme zamanı Source Generator (isteğe bağlı).** `[VeloxMap]` özniteliğiyle işaretlediğiniz tür çiftleri için derleme sırasında yansıma (reflection) kullanmayan eşleme kodu üretilir. Bu yol NativeAOT ve trimming senaryoları için tasarlanmıştır.

Her iki katman da tek pakette gelir; ek paket kurmanız gerekmez. `ProjectTo`, aynı yapılandırmadan `IQueryable` için bir projeksiyon ifadesi üretir ve EF Core bu ifadeyi SQL'e çevirir.

## Nereden başlamalı

| Hedefiniz | Başlangıç noktası |
| --- | --- |
| VeloxMapper'ı yeni bir projede denemek | [Hızlı Başlangıç](./quickstart.md): kurulum, ilk profil, DI, `Map` ve `ProjectTo` |
| AutoMapper kullanan bir projeyi taşımak | [Geçiş Rehberi](./migration-guide.md): adım adım süreç ve kontrol listesi |
| Belirli bir API'nin imzasına bakmak | [IMapper](./api/imapper.md), [MapperConfiguration](./api/mapper-configuration.md), [IMappingExpression](./api/mapping-expression.md) |
| AutoMapper ile farkları görmek | [Davranış Farkları](./behavior-differences.md) ve [API Eşleme Tablosu](./api-mapping.md) |

## Lisans

VeloxMapper [MIT lisansı](https://github.com/umutozer/VeloxMapper) ile dağıtılır. Ticari projelerde ücretsiz kullanılabilir; lisans anahtarı, kayıt veya telemetri yoktur. Kaynak kodu [GitHub](https://github.com/umutozer/VeloxMapper) üzerindedir, paket [NuGet](https://www.nuget.org/packages/VeloxMapper) üzerinden yayımlanır.

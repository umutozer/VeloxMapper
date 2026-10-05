# VeloxMapper Değişiklik Günlüğü

Tüm önemli değişiklikler bu dosyada tutulur. Sürümleme [SemVer](https://semver.org/lang/tr/) kurallarına uyar.
Ayrıntılı sürüm notları: https://veloxmapper-website.netlify.app/docs/changelog

---

## [6.0.0] - 2026-10-05

Bu sürümün hedefi, AutoMapper kullanan projelerin **kod değiştirmeden** VeloxMapper'a geçebilmesidir: tür, metot ve
overload adları AutoMapper ile eşitlendi; davranışlar AutoMapper parite testleriyle doğrulandı.

### Eklenenler
- AutoMapper ile aynı adlı tür ve arayüzler: `IMapper`, `Profile`, `ResolutionContext`, `IMappingOperationOptions`,
  `IValueResolver`, `IMemberValueResolver`, `IValueConverter`, `ITypeConverter`, `IMappingAction`, `IProfileExpression`,
  `IMapperConfigurationExpression`, `MapperConfigurationExpression`, `TypeMap`, `PropertyMap`, non-generic `IMappingExpression`
  ve `IMemberConfigurationExpression`.
- `IMapper` üzerinde tüm AutoMapper overload'ları: `Map(object, Type, Type)`, `Map(object, object, Type, Type)`, işlem seçenekli
  (`opts => opts.Items[...]`, `BeforeMap`, `AfterMap`, `State`, `ConstructServicesUsing`) overload'lar, `ProjectTo` overload'ları.
- `VeloxMapper.QueryableExtensions.Extensions.ProjectTo<T>(IConfigurationProvider, ...)` (parametreler ve `ExplicitExpansion` dahil).
- DI: `AddVeloxMapper` için AutoMapper'ın tüm overload'ları (assembly, marker type, `(sp, cfg)` delegesi) ve `AddAutoMapper` takma adı.
  Birden çok çağrı tek yapılandırmada birleşir; taranan assembly'lerdeki resolver/converter/action türleri otomatik kaydedilir.
- `MapFrom` ile farklı türde kaynak (`s => s.Customer` → `CustomerDto`, koleksiyonlar), resolver/converter örnek overload'ları,
  ad tabanlı (`"Sub.Code"`) kaynaklar, `PreCondition(ctx)`, `AddTransform`, `DestinationMember`.
- `ReverseMap()` ters ifadeyi döndürür ve zincirlenebilir; `MapFrom` üye zincirleri ve flattening otomatik tersine çevrilir.
- Profil düzeyinde konvansiyonlar (isimlendirme kuralları, ön/son ekler, `AllowNullCollections`, `ShouldMapProperty`...),
  `ReplaceMemberName`, `IncludeSourceExtensionMethods`, `DisableConstructorMapping`, `ShouldUseConstructor`, `ForAllMaps((typeMap, map) => ...)`,
  `ForAllPropertyMaps`, `CreateProfile`.
- Kaynak `GetX()` metotlarının `X` üyesine eşlenmesi, public field eşleme, yerleşik tür dönüşümleri (ToString, Parse, System.Convert,
  kullanıcı tanımlı operatörler), isimle enum eşleme, `IReadOnlyDictionary`/`ObservableCollection`/setter'sız koleksiyon desteği.
- `[AutoMap]` ile `[Ignore]`, `[SourceMember]`, `[NullSubstitute]`, `[ValueResolver]`, `[ValueConverter]`, `[UseExistingValue]`,
  `[MappingOrder]` üye öznitelikleri.
- `AssertConfigurationIsValid<TProfile>()`, `AssertConfigurationIsValid(string)`, `MemberList.Source` doğrulaması, kurucu doğrulaması,
  `CreateMapper(Func<Type, object>)`, `BuildExecutionPlan`, `MapperConfiguration(cfg, ILoggerFactory)`, `cfg.LicenseKey` (yok sayılır).
- `tools/migrate-from-automapper.ps1` geçiş betiği.

### Değişenler (kırıcı)
- `Map(source, destination)` hedefi döndürür; iç nesneler ve koleksiyonlar mevcut örneklerine eşlenir.
- `Action<VeloxResolutionContext>` alan overload'lar yerine `IMappingOperationOptions` (lambda'lar aynen derlenir).
- `ForAllMembers` parametresi `IMemberConfigurationExpression<TSource, TDestination, object>`; tek parametreli `ConvertUsing` artık
  `Expression<Func<TSource, TDestination>>` alır; `ValueTransformers.Add<T>` ifade alır.
- `DoNotValidate` üyeyi yok saymaz, yalnızca doğrulamadan muaf tutar; `DisableCtorValidation` yalnızca kurucu doğrulamasını kapatır;
  `Ignore` kuralları `ReverseMap` ile tersine çevrilmez (AutoMapper davranışı).
- `MapperConfiguration`, `VeloxMapperOptions`, `AutoMapAttribute` → `VeloxMapper` namespace'i; DI uzantıları →
  `Microsoft.Extensions.DependencyInjection` (`using VeloxMapper.DependencyInjection;` satırını kaldırın); `ProjectTo` →
  `VeloxMapper.QueryableExtensions`.
- DI'da `IMapper` transient olarak kaydedilir (derlenmiş eşleştirmeler `MapperConfiguration` üzerinde paylaşılır).
- `ShouldMapField` varsayılanı public field'lar; `AddGlobalIgnore` ön ek eşleşmesi yapar; kurucu seçimi en çok parametreli çözülebilir kurucu.
- Bağımlılık sürümleri hedef framework ile hizalandı (net8 → 8.0.x, net9 → 9.0.x, net10 → 10.0.x).

### Düzeltilenler
- DI'da singleton mapper'ın root provider ile scoped bağımlılıkları çözmesi.
- `ProjectTo` içinde string birleştirme ve metot çağrılarının reddedilmesi; koleksiyon projeksiyonlarının `List<T>`'ye dönüşmemesi.
- Adı `Proxy` ile biten sıradan sınıfların taban türe çözülmesi.
- `IncludeBase` ve open generic kapatmada `ConstructUsing`, converter türü, `IncludeMembers` ve doğrulama ayarlarının kaybolması.
- Döngüsel nesne grafiklerinde StackOverflow (referans koruması otomatik etkinleşir).
- `ForPath` içinde `MapFrom((src, dest) => ...)` fonksiyonlarının yok sayılması; `ForMember` ile field hedefleri.

---

---

## [5.2.0] - 2026-07-07

Bu sürümde proje yapısı sadeleştirilmiş, asli görevini yapan kütüphane kodları ve birim testleri haricindeki tüm yardımcı/demo/benchmark projeleri kaldırılmıştır.

### Kaldırılanlar & Sadeleştirme
- **VeloxMapper.Benchmarks**: BenchmarkDotNet testleri ve karşılaştırma projesi kaldırıldı.
- **VeloxMapper.WebSample**: ASP.NET Core Web Örnek projesi kaldırıldı.
- **VeloxMapperDemo**: Konsol uygulama demosu kaldırıldı.
- **nupkg / nupkg_test**: Eski NuGet paket sürümleri temizlendi, sadece son kararlı sürüm korundu.
- **slnx Güncellemesi**: Solution dosyası sadece Core, Ana kütüphane, Source Generator ve Unit Test projelerini içerecek şekilde sadeleştirildi.

---

## [5.0.0] - 2026-05-29

Bu büyük sürümle birlikte AutoMapper geçişini tamamen zahmetsiz hale getiren gelişmiş kurallar, polimorfizm destekleri ve performans iyileştirmeleri eklenmiştir. Ayrıca geliştirici deneyimini artırmak adına NuGet paket karmaşası giderilmiş, `VeloxMapper` ve `VeloxMapper.Core` paketleri tek bir çatı altında birleştirilmiştir.

### Eklenenler (Faz 4 & Faz 5)
- **`As<T>()` Desteği**: Haritalama sonucunu farklı bir türetilmiş tipe yönlendirme yeteneği eklendi. AutoMapper'ın `.As<T>()` API'siyle birebir uyumludur.
- **`SetMappingOrder()` Desteği**: Özelliklerin (properties) hangi sırayla eşleneceğini belirleme imkanı sağlandı. Sıralı atama gerektiren karmaşık iş kuralları için idealdir.
- **`IncludeAllDerived()` Desteği**: Bir temel sınıf eşleşmesinde, tanımlanmış tüm alt sınıf eşleşmelerini otomatik olarak polimorfik haritalamaya dahil eden metot eklendi.
- **Açık Generic (Open Generics) Desteği**: `CreateMap(typeof(Source<>), typeof(Dest<>))` gibi açık generic tür eşleştirmeleri çalışma zamanında dinamik olarak çözülecek şekilde optimize edildi.
- **`ConvertUsing(Func<>)` Lambda Overload**: Basit tür dönüşümlerini tek satırda gerçekleştirmek için lambda delegate desteği sağlandı.
- **`UseDestinationValue()` Desteği**: Eşleştirme sırasında hedef nesnedeki mevcut referansların (özellikle nested class ve generic koleksiyonlarda) korunmasını ve üzerine patch yapılmasını sağlayan özellik eklendi.
- **Source Generator Miras Desteği**: Source Generator'ın base class'lardaki public property'leri de rekürsif olarak tarayarak kod üretmesi sağlandı.

### İyileştirmeler & Hata Düzeltmeleri (Bug Fixes)
- **NuGet Paket Birleşimi**: Geliştirici kafa karışıklığını önlemek adına `VeloxMapper.Core` ve `VeloxMapper` paketleri tek bir pakette (`VeloxMapper`) toplandı.
- **Versiyon Tutarsızlığı**: `VeloxVersion.Current` sabitinin hardcoded `4.0.1` kalması sorunu giderilerek güncel sürüm (`4.1.2` / `5.0.0`) ile senkronize edildi.
- **ProjectTo Performans İyileştirmesi**: EF Core `ProjectTo` çağrılarındaki her seferinde reflection yapan Queryable metod aramaları static readonly field ile cache'lendi.
- **RequiresContext Lazy Caching**: Haritalamanın ResolutionContext gerektirip gerektirmediğini sorgulayan getter özelliğine lazy-cache mekanizması uygulanarak performans artışı sağlandı.
- **ReferenceComparer Tekilleştirme**: Kod tabanındaki `ReferenceComparer` sınıfı duplikasyonu giderilerek tek bir ortak konuma taşındı.

---

## [4.1.2] - Önceki Sürümler

- Temel `ForPath()`, `Include()`, `MaxDepth()`, `PreserveReferences()` özellikleri.
- Temel dependency injection (DI) entegrasyonu (`AddVeloxMapper`).
- `IValueResolver`, `IMemberValueResolver` ve `IValueConverter` uyumlulukları.

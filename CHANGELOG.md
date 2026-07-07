# VeloxMapper Değişiklik Günlüğü (CHANGELOG)

Tüm sürüm değişiklikleri bu dosyada kayıt altında tutulur. VeloxMapper projesinin temel amacı, .NET projelerinde en az efor ve zahmet ile geliştiricilerin AutoMapper'dan VeloxMapper'a geçmesini sağlamak ve kusursuz bir eşleştirme (mapping) deneyimi sunmaktır.

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

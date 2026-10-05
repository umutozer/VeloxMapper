---
title: "SSS"
description: "VeloxMapper'ın lisansı, AutoMapper ile birlikte kullanımı, performansı, NativeAOT desteği, desteklenen platformlar, iş parçacığı güvenliği ve servis ömürleri hakkında sık sorulan sorular."
section: resources
order: 20
---

# SSS

VeloxMapper hakkında sık sorulan soruların kısa yanıtları. Ayrıntılı bilgi için her yanıttaki bağlantıları izleyin.

## Lisans

### VeloxMapper ücretli mi?

Hayır. VeloxMapper [MIT lisansı](https://github.com/umutozer/VeloxMapper) ile dağıtılır; ticari ve kapalı kaynaklı projeler dahil her yerde ücretsiz kullanılabilir. Lisans anahtarı, kayıt, kullanım sınırı veya telemetri yoktur.

### AutoMapper'daki LicenseKey satırını silmem gerekir mi?

Hayır. AutoMapper 15 ile eklenen `cfg.LicenseKey = "...";` satırı VeloxMapper'da derlenir ve değeri yok sayılır. Kodunuzu sadeleştirmek için satırı silebilirsiniz.

### MIT lisansı bana hangi yükümlülükleri getirir?

Kütüphaneyi dağıttığınız yazılımda telif hakkı bildirimini ve lisans metnini korumanız yeterlidir. Hukuki değerlendirme için kendi hukuk ekibinize danışın.

## Geçiş

### AutoMapper ve VeloxMapper aynı projede birlikte kullanılabilir mi?

Önerilmez. İki paket de `IMapper`, `Profile`, `MapperConfiguration` gibi aynı tür adlarını ve `Microsoft.Extensions.DependencyInjection` namespace'inde `AddAutoMapper` uzantı metodunu tanımlar. Aynı projede iki `using` birlikte bulunduğunda tür adları, iki paket birlikte referans edildiğinde ise `AddAutoMapper` çağrıları belirsiz hale gelir (`CS0104`, `CS0121`). Çözümü tek seferde taşıyın; bkz. [Geçiş Rehberi](./migration-guide.md).

### Kodumda ne kadar değişiklik gerekir?

Çoğu projede yalnızca paket referansları, `using` satırları ve isteğe bağlı olarak `AddAutoMapper` çağrıları değişir. AutoMapper 13.0.1 ile yazılmış bir örnek uygulama (DI, scoped resolver, value converter, EF Core `ProjectTo`, `ReverseMap`, PATCH, `Items`) [geçiş betiği](./migration-script.md) çalıştırıldıktan sonra başka değişiklik yapılmadan derlendi ve aynı çıktıları üretti. Kodunuzu etkileyebilecek farklar [Davranış Farkları](./behavior-differences.md) sayfasındadır.

### AutoMapper'ın hangi sürümlerinden geçiş yapılabilir?

VeloxMapper'ın API'si AutoMapper'ın güncel genel API'sini (profiller, `IMapper`, DI uzantıları, `ProjectTo`) hedefler. AutoMapper 9 ve sonrasında kaldırılan statik `Mapper.Initialize` / `Mapper.Map` API'si VeloxMapper'da yoktur; bu API'yi kullanan kodu önce örnek tabanlı `IMapper` kullanımına çevirin.

### Bir AutoMapper API'si VeloxMapper'da yoksa ne yapmalıyım?

[API Eşleme Tablosu](./api-mapping.md#bilinen-api-farkları) ve [Davranış Farkları](./behavior-differences.md#desteklenmeyen-apiler) sayfalarındaki alternatiflere bakın. Listede olmayan bir eksik bulursanız [GitHub](https://github.com/umutozer/VeloxMapper) üzerinden bildirin.

## Performans

### VeloxMapper AutoMapper'dan hızlı mı?

Yayımlanmış bir karşılaştırmalı ölçüm (benchmark) yoktur; bu dokümantasyon da sayısal bir performans iddiasında bulunmaz. Performans; model yapısına, koleksiyon boyutlarına ve kullanılan özelliklere (resolver, koşul, kalıtım) göre değişir. Kendi senaryonuzu ölçün:

1. Gerçek DTO'larınızla bir [BenchmarkDotNet](https://benchmarkdotnet.org) projesi oluşturun.
2. Geçiş öncesi AutoMapper ile, geçiş sonrası VeloxMapper ile aynı benchmark'ı çalıştırın.
3. Hem süreyi hem de bellek ayırmayı (`[MemoryDiagnoser]`) karşılaştırın.
4. `MapperConfiguration` nesnesini benchmark'ın kurulum (setup) adımında oluşturun ve `CompileMappings()` çağırın; aksi halde ilk eşlemenin derleme maliyetini ölçmüş olursunuz.

`ProjectTo` sorgularında kütüphanenin süresi genellikle veritabanı süresinin yanında küçüktür; burada üretilen SQL'i `ToQueryString()` ile karşılaştırmak daha anlamlıdır.

### İlk eşleme neden daha yavaş?

Bir tür çifti ilk kez eşlendiğinde VeloxMapper eşleme kodunu üretir ve derler. Sonuç `MapperConfiguration` üzerinde önbelleğe alınır; sonraki çağrılar derlenmiş kodu kullanır. Bu maliyeti uygulama başlangıcına almak için `configuration.CompileMappings()` çağırın. Bkz. [Performans](./performance.md).

## Platform

### Hangi .NET sürümleri destekleniyor?

.NET 8, .NET 9 ve .NET 10. .NET Framework ve .NET Standard desteklenmez. Bkz. [Kurulum](./installation.md#desteklenen-platformlar).

### NativeAOT ve trimming destekleniyor mu?

İki yol vardır:

- **Source Generator yolu:** `[VeloxMap]` ile işaretlenen tür çiftleri için eşleme kodu derleme zamanında üretilir ve yansıma (reflection) kullanmaz. NativeAOT ve trimming senaryoları için bu yol tasarlanmıştır.
- **Çalışma zamanı yolu:** `IMapper.Map`, `ProjectTo` ve profil tabanlı yapılandırma, yansıma ve çalışma zamanında ifade ağacı derleme (`Expression.Compile`) kullanır. Bu yol dinamik kod üretimine ihtiyaç duyar; kütüphane NativeAOT/trimming uyumlu olarak işaretlenmemiştir ve bu senaryolarda çalışması garanti edilmez.

AOT hedefleyen bir uygulamada kritik eşlemeler için Source Generator yolunu kullanın ve uygulamanızı AOT yayımıyla test edin.

### EF Core'un hangi sürümleri ile çalışır?

`ProjectTo` standart LINQ ifade ağaçları üretir ve belirli bir EF Core sürümüne bağımlı değildir; VeloxMapper paketinin EF Core bağımlılığı yoktur. Test paketi EF Core InMemory sağlayıcısıyla doğrulanmıştır. Üretilen ifadenin SQL'e çevrilebilmesi kullandığınız veritabanı sağlayıcısına bağlıdır.

## Çalışma zamanı

### MapperConfiguration ve IMapper iş parçacığı güvenli mi?

Evet. `MapperConfiguration` oluşturulduktan sonra değiştirilemez ve derlenmiş eşleme önbelleği eşzamanlı erişime uygundur. `IMapper` örnekleri birden fazla iş parçacığından aynı anda kullanılabilir. Eşleme sırasında oluşturulan bağlam (`ResolutionContext`) her çağrıya özeldir.

Kendi resolver ve converter sınıflarınızın iş parçacığı güvenliği sizin sorumluluğunuzdadır; DI ile oluşturulanlar transient'tır.

### Servis ömürleri nedir?

| Servis | Ömür |
| --- | --- |
| `MapperConfiguration`, `IConfigurationProvider` | Singleton |
| `IMapper`, `IVeloxMapper` | Transient |
| Taranan assembly'lerdeki resolver, converter ve mapping action türleri | Transient |

`IMapper` transient olduğu için her türlü servise enjekte edilebilir. Resolver'lar scoped servislere bağlıysa `IMapper`'ı scoped bir servise enjekte edin veya bir kapsam açın. Bkz. [Dependency Injection](./dependency-injection.md#servis-ömürleri).

### MapperConfiguration'ı her istekte oluşturabilir miyim?

Teknik olarak evet, ancak yapmayın. Derlenmiş eşleme kodu `MapperConfiguration` üzerinde önbelleğe alınır; her yeni örnek tüm eşlemeleri yeniden derler. Uygulama başına bir örnek oluşturun (DI bunu sizin için yapar).

### Hata mesajları neden Türkçe?

VeloxMapper'ın istisna ve teşhis mesajları Türkçe yazılmıştır ve tür, üye ve profil adlarını içerir. İstisna **türleri** (`VeloxValidationException` vb.) dilden bağımsızdır; kodunuzda mesaj metnine değil türe göre işlem yapın.

## Destek

### Hata bildirimi veya özellik isteğini nereye iletebilirim?

[GitHub deposu](https://github.com/umutozer/VeloxMapper) üzerinden bir issue açın. Mümkünse hatayı yeniden üreten küçük bir örnek, VeloxMapper sürümünü (`VeloxVersion.Current`) ve hedef .NET sürümünü ekleyin.

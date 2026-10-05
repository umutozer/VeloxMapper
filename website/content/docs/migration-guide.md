---
title: "Geçiş Rehberi"
description: "AutoMapper kullanan bir .NET projesini adım adım, ölçülebilir ve geri alınabilir şekilde VeloxMapper'a taşıyın."
section: migration
order: 10
badge: "Yeni"
---

# Geçiş Rehberi

Bu rehber, AutoMapper kullanan bir çözümü VeloxMapper'a taşımak için önerilen süreci anlatır. Çoğu projede kod değişikliği paket referansları ve `using` satırlarıyla sınırlıdır; asıl çaba, davranışın aynı kaldığını doğrulamaktır.

## Ne değişir, ne aynı kalır

VeloxMapper, AutoMapper'ın genel API'sini aynı adlarla sunar. Profilleriniz, `CreateMap` / `ForMember` zincirleriniz, resolver ve converter sınıflarınız, `IMapper` kullanan servisleriniz ve testleriniz olduğu gibi kalır.

| Aynı kalır | Değişir |
| --- | --- |
| `Profile`, `CreateMap`, `ForMember`, `ForPath`, `ForCtorParam`, `ReverseMap`, `Include`, `IncludeBase` | NuGet paketi |
| `IMapper.Map` overload'ları, `ProjectTo`, `opts.Items`, `BeforeMap`, `AfterMap` | `using AutoMapper*` satırları |
| `IValueResolver`, `IMemberValueResolver`, `IValueConverter`, `ITypeConverter`, `IMappingAction` | `AddAutoMapper` → `AddVeloxMapper` (isteğe bağlı) |
| `MapperConfiguration`, `AssertConfigurationIsValid`, `CompileMappings` | `catch` bloklarındaki istisna türleri |
| `[AutoMap]`, `[Ignore]`, `[SourceMember]` ve diğer öznitelikler | Kaldırılması gereken eklenti paketleri |

Tam liste için [API Eşleme Tablosu](./api-mapping.md) sayfasına bakın.

### Değişen satırlar

<!-- tabs -->
```csharp title="AutoMapper"
using AutoMapper;
using AutoMapper.QueryableExtensions;
using AutoMapper.Configuration.Annotations;

builder.Services.AddAutoMapper(typeof(Program));

try
{
    var dto = mapper.Map<OrderDto>(order);
}
catch (AutoMapperMappingException ex)
{
    logger.LogError(ex, "Eşleme başarısız");
}
```
```csharp title="VeloxMapper"
using VeloxMapper;
using VeloxMapper.QueryableExtensions;
using VeloxMapper.Configuration.Annotations;

builder.Services.AddVeloxMapper(typeof(Program));

try
{
    var dto = mapper.Map<OrderDto>(order);
}
catch (VeloxMapper.Exceptions.VeloxMappingException ex)
{
    logger.LogError(ex, "Eşleme başarısız");
}
```
<!-- /tabs -->

Proje dosyasında:

<!-- tabs -->
```xml title="AutoMapper"
<PackageReference Include="AutoMapper" Version="13.0.1" />
<PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="12.0.1" />
```
```xml title="VeloxMapper"
<PackageReference Include="VeloxMapper" Version="6.0.0" />
```
<!-- /tabs -->

## Başlamadan önce

- **Hedef çerçeve:** VeloxMapper .NET 8, 9 ve 10'u destekler. .NET Framework veya .NET 7 ve öncesini hedefleyen projeler önce yükseltilmelidir.
- **Eklenti paketleri:** `AutoMapper.Collection`, `AutoMapper.Extensions.ExpressionMapping`, `AutoMapper.EF6`, `AutoMapper.Data` veya `AutoMapper.AspNetCore.OData` kullanıyorsanız bu paketlerin VeloxMapper karşılığı yoktur. Kullanımlarını [Davranış Farkları](./behavior-differences.md#desteklenmeyen-apiler) sayfasındaki önerilere göre planlayın.
- **Test kapsamı:** Eşleme sonuçlarını doğrulayan testleriniz ne kadar çoksa geçiş o kadar güvenlidir. Kritik DTO'lar için test yoksa geçişten önce birkaç tane yazın (bkz. [Adım 7](#7-testleri-çalıştırın)).

## Adım adım geçiş

### 1. Ayrı bir dal açın ve çalışma alanını temizleyin

```bash
git switch -c chore/migrate-to-veloxmapper
git status   # bekleyen değişiklik olmamalı
```

Betik dosyaları yerinde değiştirir. Temiz bir çalışma alanı, sonucu `git diff` ile incelemenizi ve gerekirse `git restore .` ile geri almanızı sağlar.

### 2. Betiği önce deneme modunda çalıştırın

[Otomatik geçiş betiğini](./migration-script.md) indirin ve `-DryRun` ile çalıştırın. Bu mod hiçbir dosyayı değiştirmez; yalnızca değişecek dosyaları ve elle gözden geçirilmesi gereken kullanımları listeler.

```powershell
Invoke-WebRequest https://veloxmapper-website.netlify.app/migrate-from-automapper.ps1 -OutFile migrate-from-automapper.ps1
pwsh ./migrate-from-automapper.ps1 -Path . -DryRun
```

Çıktıda `Elle gözden geçirilmesi gerekenler` başlığı altında bir uyarı varsa (ör. `UseAsDataSource`, `EqualityComparison`, `cfg.Internal()`), devam etmeden önce bu kullanımlar için bir plan yapın.

### 3. Betiği çalıştırın

```powershell
pwsh ./migrate-from-automapper.ps1 -Path .
```

Betik şunları yapar:

- `.csproj`, `Directory.Packages.props`, `Directory.Build.props` ve `packages.config` dosyalarında `AutoMapper` referansını `VeloxMapper` ile değiştirir; DI ve EnumMapping eklenti paketlerini kaldırır.
- `.cs` dosyalarındaki `using AutoMapper...` satırlarını, `AddAutoMapper(` çağrılarını, istisna adlarını ve tam nitelikli `AutoMapper.X` adlarını günceller.
- Dosya kodlamasını (BOM dahil) ve satır sonlarını korur.

Elle yapmayı tercih ederseniz aynı değişiklikleri [API Eşleme Tablosu](./api-mapping.md)'na göre uygulayın.

### 4. Derleyin

```bash
dotnet restore
dotnet build
```

Derleme hataları genellikle şunlardan kaynaklanır:

| Hata | Neden | Çözüm |
| --- | --- | --- |
| `CS0246: 'AutoMapper' türü veya ad alanı bulunamadı` | Betiğin yakalamadığı bir `using` (ör. `using static`, takma ad `using AM = AutoMapper;`) | Satırı elle `VeloxMapper` olarak değiştirin. |
| `CS0104: belirsiz başvuru` veya `CS0121: çağrı belirsiz` (`IMapper`, `AddAutoMapper`) | AutoMapper paketi hâlâ bir projeye doğrudan veya transitif olarak referans veriliyor | `dotnet list package --include-transitive` ile bulun ve kaldırın. |
| `CS1061: 'Internal' tanımı yok` | `cfg.Internal()` AutoMapper'ın iç API'sidir | `cfg.ForAllMaps` / `cfg.ForAllPropertyMaps` ile yeniden yazın. |
| `UseAsDataSource` veya `EqualityComparison` bulunamadı | Desteklenmeyen eklenti API'leri | [Davranış Farkları](./behavior-differences.md#desteklenmeyen-apiler) sayfasındaki alternatifleri uygulayın. |

### 5. Yapılandırmayı doğrulayın

Henüz yoksa, tüm profilleri yükleyip doğrulayan bir test ekleyin:

```csharp title="MappingConfigurationTests.cs"
using VeloxMapper;
using Xunit;

public class MappingConfigurationTests
{
    [Fact]
    public void Mapping_configuration_is_valid()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddMaps(typeof(OrderProfile)));

        configuration.AssertConfigurationIsValid();
    }
}
```

Doğrulama başarısızsa `VeloxValidationException` mesajı her sorunu profil, tür çifti ve üye adıyla listeler. AutoMapper'da geçen bir yapılandırma VeloxMapper'da genellikle aynı sonucu verir; fark görürseniz [Sorun Giderme](./troubleshooting.md#yapılandırma-doğrulaması-başarısız) sayfasına bakın.

### 6. Davranış farklarını gözden geçirin

[Davranış Farkları](./behavior-differences.md) sayfasındaki listeyi kod tabanınızla karşılaştırın. Özellikle şu kullanımları arayın:

- `ProjectTo` ile birlikte resolver, value converter veya `MapFrom((src, dest) => ...)` gibi fonksiyon tabanlı kurallar: VeloxMapper bu üyeleri sorgudan çıkarır ve uyarı loglar.
- `catch (AutoMapperMappingException)` veya hata mesajına bağlı mantık: istisna türleri ve mesaj metinleri farklıdır.
- "Missing type map" hatasına güvenen testler: VeloxMapper tanımlanmamış tür çiftlerini konvansiyonla eşler.

### 7. Testleri çalıştırın

```bash
dotnet test
```

Eşleme sonuçlarını doğrulayan testler geçiyorsa davranış korunmuştur. Testi olmayan kritik eşlemeler için, geçişten önce AutoMapper ile alınmış çıktıyı referans kabul eden bir "anlık görüntü" (snapshot) testi yazabilirsiniz:

```csharp title="OrderMappingTests.cs"
[Fact]
public void Order_maps_to_expected_dto()
{
    var mapper = new MapperConfiguration(cfg => cfg.AddMaps(typeof(OrderProfile))).CreateMapper();

    var dto = mapper.Map<OrderDto>(TestData.SampleOrder());

    var json = JsonSerializer.Serialize(dto);
    Assert.Equal(File.ReadAllText("Snapshots/order-dto.json"), json);
}
```

Snapshot dosyasını geçişten **önce**, AutoMapper ile çalışan dalda üretin; geçişten sonra aynı test VeloxMapper ile çalışır.

### 8. Değişiklikleri inceleyin ve birleştirin

```bash
git diff --stat
git diff
```

Diff'te yalnızca paket referansları, `using` satırları, `AddAutoMapper` çağrıları ve istisna adları görünmelidir. Beklemediğiniz bir değişiklik varsa inceleyin.

## ProjectTo sorgularını karşılaştırın

`ProjectTo` kullanan sorgularda üretilen SQL'i geçişten önce ve sonra karşılaştırmak, performans ve doğruluk açısından en güvenilir kontroldür. EF Core'un `ToQueryString()` metodu sorguyu çalıştırmadan SQL metnini döndürür. Bu karşılaştırma için ilişkisel bir sağlayıcı (SQL Server, PostgreSQL, SQLite) kullanın; InMemory sağlayıcısı SQL üretmez.

```csharp title="ProjectionSqlTests.cs"
[Fact]
public void Order_summary_projection_sql()
{
    using var db = TestDb.Create();
    var configuration = new MapperConfiguration(cfg => cfg.AddMaps(typeof(OrderProfile)));

    var sql = db.Orders
        .Where(o => o.Status == OrderStatus.Paid)
        .ProjectTo<OrderSummaryDto>(configuration)
        .ToQueryString();

    // Geçiş öncesinde AutoMapper ile kaydettiğiniz SQL ile karşılaştırın
    Assert.Equal(File.ReadAllText("Snapshots/order-summary.sql"), sql);
}
```

Farklar genellikle şunlardan kaynaklanır:

- **Null yayılımı:** VeloxMapper'da `EnableNullPropagationForQueryMapping` varsayılan olarak açıktır; ara navigasyon üyeleri için `CASE WHEN ... IS NULL` ifadeleri üretilebilir. AutoMapper'ın davranışına dönmek için `cfg.EnableNullPropagationForQueryMapping = false;` ayarlayın.
- **Atlanan üyeler:** Resolver veya value converter kullanan üyeler projeksiyondan çıkarılır; ilgili kolon SQL'de yer almaz.

SQL farklı olsa bile sonuç kümesinin aynı olduğunu bir entegrasyon testiyle doğrulayın.

## Kontrol listesi

- [ ] Ayrı bir dal açıldı, çalışma alanı temiz
- [ ] Betik `-DryRun` ile çalıştırıldı, uyarılar incelendi
- [ ] Betik çalıştırıldı, `AutoMapper` paket referansı kalmadı (`dotnet list package --include-transitive`)
- [ ] `dotnet build` hatasız
- [ ] `AssertConfigurationIsValid()` testi var ve geçiyor
- [ ] [Davranış Farkları](./behavior-differences.md) listesi kod tabanıyla karşılaştırıldı
- [ ] `ProjectTo` kullanan kritik sorguların SQL'i karşılaştırıldı
- [ ] `catch` blokları ve hata mesajına bağlı kod güncellendi
- [ ] Tüm testler geçiyor
- [ ] `git diff` incelendi

## Yayına alma önerileri

- **Çözümü tek seferde taşıyın.** Çok projeli çözümlerde tüm projeleri aynı dalda taşıyın. Bir projenin AutoMapper'da, diğerinin VeloxMapper'da kalması iki paketin birlikte referans edilmesine ve tür adı çakışmalarına yol açar.
- **Önce test ortamında çalıştırın.** Hata loglarında `VeloxMapper` kategorisini izleyin; `ProjectTo` uyarıları burada görünür.
- **Ölçün.** Performans kritik yollar için kendi benchmark'ınızı geçiş öncesi ve sonrası çalıştırın (bkz. [SSS](./faq.md#veloxmapper-automapperdan-hızlı-mı)).
- **Geri dönüş planı.** Geçiş tek bir commit veya PR olduğu için geri almak `git revert` kadar basittir.

## Yapay zekâ asistanıyla geçiş

Betiğin kapsamadığı durumlar (desteklenmeyen API'ler, özel istisna yönetimi) için bir yapay zekâ asistanından yardım alabilirsiniz. Hazır yönergeler [Yapay Zekâ ile Geçiş](./migration-prompt.md) sayfasındadır.

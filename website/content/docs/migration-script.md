---
title: "Otomatik Geçiş Betiği"
description: "migrate-from-automapper.ps1 betiğiyle paket referanslarını, using satırlarını, DI çağrılarını ve istisna adlarını tek komutla AutoMapper'dan VeloxMapper'a çevirin."
section: migration
order: 20
---

# Otomatik Geçiş Betiği

`migrate-from-automapper.ps1`, bir çözümdeki AutoMapper referanslarını VeloxMapper karşılıklarıyla değiştiren bir PowerShell betiğidir. Yalnızca metin değişikliği yapar, hiçbir şeyi derlemez veya yüklemez; sonucu `git diff` ile inceleyebilirsiniz.

## Hızlı kullanım

```powershell
# 1. Betiği indirin
Invoke-WebRequest https://veloxmapper-website.netlify.app/migrate-from-automapper.ps1 -OutFile migrate-from-automapper.ps1

# 2. Değişiklikleri önizleyin (dosyalar değişmez)
pwsh ./migrate-from-automapper.ps1 -Path ./src -DryRun

# 3. Uygulayın
pwsh ./migrate-from-automapper.ps1 -Path ./src
```

> [!IMPORTANT]
> Betiği çalıştırmadan önce bekleyen değişikliklerinizi commit edin. Betik dosyaları yerinde değiştirir; temiz bir çalışma alanı sonucu incelemenizi ve gerekirse `git restore .` ile geri almanızı sağlar.

## Parametreler

| Parametre | Tür | Varsayılan | Açıklama |
| --- | --- | --- | --- |
| `-Path` | `string` | `.` (geçerli klasör) | Taranacak kök klasör. Alt klasörler özyinelemeli taranır. |
| `-Version` | `string` | `6.0.0` | Proje dosyalarına yazılacak VeloxMapper paket sürümü. |
| `-DryRun` | `switch` | kapalı | Dosyaları değiştirmeden yalnızca değişecek dosyaları ve uyarıları listeler. |

Taramada `bin`, `obj`, `.git`, `.vs`, `node_modules` ve `packages` klasörleri atlanır.

## Betiğin yaptığı değişiklikler

### Paket referansları

Taranan dosyalar: `*.csproj`, `*.fsproj`, `*.vbproj`, `Directory.Packages.props`, `Directory.Build.props`, `packages.config`.

| Önce | Sonra |
| --- | --- |
| `<PackageReference Include="AutoMapper" Version="13.0.1" />` | `<PackageReference Include="VeloxMapper" Version="6.0.0" />` |
| `<PackageVersion Include="AutoMapper" Version="13.0.1" />` | `<PackageVersion Include="VeloxMapper" Version="6.0.0" />` |
| `<PackageReference Include="AutoMapper" />` (merkezi sürüm yönetimi) | `<PackageReference Include="VeloxMapper" />` |
| `<package id="AutoMapper" version="..." />` | `<package id="VeloxMapper" version="6.0.0" />` |
| `AutoMapper.Extensions.Microsoft.DependencyInjection` | Kaldırılır (VeloxMapper'da yerleşik) |
| `AutoMapper.Extensions.EnumMapping` | Kaldırılır (VeloxMapper'da yerleşik) |

### C# kaynak kodu

Yalnızca `*.cs` dosyaları değiştirilir.

| Önce | Sonra |
| --- | --- |
| `using AutoMapper;` / `global using AutoMapper;` | `using VeloxMapper;` / `global using VeloxMapper;` |
| `using AutoMapper.QueryableExtensions;` | `using VeloxMapper.QueryableExtensions;` |
| `using AutoMapper.Configuration.Annotations;` | `using VeloxMapper.Configuration.Annotations;` |
| `using AutoMapper.Configuration;` | `using VeloxMapper.Configuration;` |
| `using AutoMapper.Extensions.EnumMapping;` | Satır silinir |
| `using AutoMapper.Extensions.ExpressionMapping;` | `using VeloxMapper; // TODO: ...` yorumuyla değiştirilir |
| `.AddAutoMapper(` | `.AddVeloxMapper(` |
| `AutoMapperMappingException` | `VeloxMapper.Exceptions.VeloxMappingException` |
| `AutoMapperConfigurationException` | `VeloxMapper.Exceptions.VeloxValidationException` |
| `DuplicateTypeMapConfigurationException` | `VeloxMapper.Exceptions.VeloxConfigurationException` |
| `AutoMapper.QueryableExtensions.` | `VeloxMapper.QueryableExtensions.` |
| `AutoMapper.IMapper`, `AutoMapper.Profile`, `AutoMapper.MapperConfiguration` ve diğer tam nitelikli adlar | `VeloxMapper.IMapper`, `VeloxMapper.Profile`, ... |

Dosya kodlaması (UTF-8 BOM varlığı dahil) ve satır sonları korunur.

### Elle gözden geçirilmesi gereken kullanımlar

Betik aşağıdaki kullanımları **değiştirmez**, yalnızca raporlar:

| Bulunan | Neden | Öneri |
| --- | --- | --- |
| `.Internal()` | AutoMapper'ın iç API'si | `cfg.ForAllMaps` veya `cfg.ForAllPropertyMaps` kullanın. |
| `UseAsDataSource` | `AutoMapper.Extensions.ExpressionMapping` desteklenmez | `ProjectTo` kullanın. |
| `EqualityComparison` | `AutoMapper.Collection` desteklenmez | Koleksiyonlar Clear + Add ile eşlenir; eşleştirmeli güncelleme için [Mevcut Nesneye Eşleme](./map-to-existing.md#koleksiyonları-anahtara-göre-güncelleme) sayfasına bakın. |
| `.AsProxy()` | Arayüz proxy'leri üretilmez | Somut bir hedef tür kullanın. |
| `AutoMapper.Collection`, `AutoMapper.EF6`, `AutoMapper.Data`, `AutoMapper.Extensions.ExpressionMapping`, `AutoMapper.AspNetCore.OData` paketleri | VeloxMapper karşılığı yok | Kullanımı kaldırın veya yeniden yazın; paket referansını elle silin. |

## Örnek çıktı

İki projeli bir çözümde (`Shop.Api` ve `Shop.Application`; ikincisi `AutoMapper.Collection` kullanıyor) önce deneme modu:

```console
> pwsh ./migrate-from-automapper.ps1 -Path . -DryRun

VeloxMapper geçiş betiği — 5 dosya DEĞİŞTİRİLECEK
  ~ src\Shop.Api\Shop.Api.csproj
  ~ src\Shop.Application\Shop.Application.csproj
  ~ src\Shop.Api\Program.cs
  ~ src\Shop.Application\OrderProfile.cs
  ~ src\Shop.Application\OrderQueries.cs

Elle gözden geçirilmesi gerekenler (2):
  ! C:\src\shop\src\Shop.Application\OrderProfile.cs: EqualityComparison (AutoMapper.Collection) desteklenmez; koleksiyonlar Clear+Add ile eşlenir.
  ! C:\src\shop\src\Shop.Application\Shop.Application.csproj: 'Include="AutoMapper.Collection' paketi için VeloxMapper karşılığı yok; kullanımını elle gözden geçirin.

Sonraki adımlar:
  1. dotnet restore && dotnet build
  2. Testlerinizde config.AssertConfigurationIsValid() çağrısını çalıştırın
  3. Davranış farkları: https://veloxmapper-website.netlify.app/docs/behavior-differences
```

`-DryRun` olmadan çalıştırıldığında başlık `5 dosya DEĞİŞTİRİLDİ` olur. Ardından `git diff` şuna benzer bir sonuç gösterir:

```diff
--- a/src/Shop.Api/Shop.Api.csproj
+++ b/src/Shop.Api/Shop.Api.csproj
   <ItemGroup>
-    <PackageReference Include="AutoMapper" Version="13.0.1" />
-    <PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="12.0.1" />
+    <PackageReference Include="VeloxMapper" Version="6.0.0" />
   </ItemGroup>
--- a/src/Shop.Api/Program.cs
+++ b/src/Shop.Api/Program.cs
-using AutoMapper;
+using VeloxMapper;
 
 var builder = WebApplication.CreateBuilder(args);
-builder.Services.AddAutoMapper(typeof(Program));
+builder.Services.AddVeloxMapper(typeof(Program));
--- a/src/Shop.Application/OrderQueries.cs
+++ b/src/Shop.Application/OrderQueries.cs
-using AutoMapper;
-using AutoMapper.QueryableExtensions;
+using VeloxMapper;
+using VeloxMapper.QueryableExtensions;
@@
-        catch (AutoMapperMappingException ex)
+        catch (VeloxMapper.Exceptions.VeloxMappingException ex)
```

`AutoMapper.Collection` referansı yerinde kalır; uyarıyı ele aldıktan sonra elle silin.

## Platformlara göre çalıştırma

### Windows (PowerShell 7 veya Windows PowerShell 5.1)

Betik hem PowerShell 7 (`pwsh`) hem de Windows'ta yerleşik Windows PowerShell 5.1 (`powershell`) ile çalışır.

```powershell
Invoke-WebRequest https://veloxmapper-website.netlify.app/migrate-from-automapper.ps1 -OutFile migrate-from-automapper.ps1

# PowerShell 7
pwsh ./migrate-from-automapper.ps1 -Path . -DryRun

# Windows PowerShell 5.1
powershell -ExecutionPolicy Bypass -File .\migrate-from-automapper.ps1 -Path . -DryRun
```

İnternetten indirilen betikler yürütme ilkesi (execution policy) nedeniyle engellenebilir. Bu durumda yukarıdaki gibi `-ExecutionPolicy Bypass` kullanın veya dosyanın engelini kaldırın:

```powershell
Unblock-File .\migrate-from-automapper.ps1
```

Windows PowerShell 5.1 konsolunda Türkçe karakterler bozuk görünürse çıktı kodlamasını UTF-8 yapın. Bu yalnızca ekran çıktısını etkiler; dosyalar her durumda doğru kodlamayla yazılır.

```powershell
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
```

### Linux ve macOS

PowerShell 7 (`pwsh`) gerekir. Kurulu değilse .NET SDK ile global araç olarak kurabilirsiniz:

```bash
dotnet tool install --global PowerShell
```

Ardından:

```bash
curl -fsSLO https://veloxmapper-website.netlify.app/migrate-from-automapper.ps1
pwsh ./migrate-from-automapper.ps1 -Path ./src -DryRun
pwsh ./migrate-from-automapper.ps1 -Path ./src
```

Betiğin kaynak kodu depoda `tools/migrate-from-automapper.ps1` yolunda da bulunur.

## CI'da kullanım

Geçişten sonra kod tabanına yeniden AutoMapper referansı eklenmesini önlemek için betiği deponuza (ör. `tools/` klasörüne) ekleyin, CI'da çalıştırıp çalışma alanında değişiklik olup olmadığını denetleyebilirsiniz. Betik bir şey değiştirirse `git diff --exit-code` sıfır dışı kodla çıkar ve adım başarısız olur:

```yaml title=".github/workflows/ci.yml"
- name: AutoMapper referansı kalmadığını doğrula
  shell: pwsh
  run: |
    ./tools/migrate-from-automapper.ps1 -Path .
    git diff --exit-code
```

> [!NOTE]
> Betik, elle gözden geçirilmesi gereken bir madde bulduğunda `2` çıkış koduyla, aksi halde `0` ile tamamlanır; bu nedenle CI adımı uyarılarda da başarısız olur. Geçersiz bir `-Path` verildiğinde betik hata ile sonlanır.

## Sınırlamalar

Betik düzenli ifadelerle metin değiştirir; C# kodunu ayrıştırmaz. Aşağıdaki durumları elle düzeltmeniz gerekir:

- **Takma adlı ve statik using'ler:** `using AM = AutoMapper;` veya `using static AutoMapper...;` değiştirilmez.
- **Razor ve diğer dosya türleri:** `.cshtml` / `.razor` içindeki `@using AutoMapper`, F# ve VB kaynak dosyaları değiştirilmez (bu projelerin paket referansları ise güncellenir).
- **Statik çağrılar:** `ServiceCollectionExtensions.AddAutoMapper(services, ...)` gibi uzantı yöntemi olmayan çağrılar değişmez. `AddAutoMapper` takma adı sayesinde bu kod yine de derlenir.
- **Hata mesajına bağlı kod:** İstisna mesajları farklıdır (bkz. [Davranış Farkları](./behavior-differences.md#değişen-istisna-türleri-ve-mesajlar)); mesaj metnini karşılaştıran kodu betik bulamaz.
- **Davranış farkları:** Betik yalnızca derlenebilirliği sağlar. Çalışma zamanı farkları için [Geçiş Rehberi](./migration-guide.md)'ndeki doğrulama adımlarını uygulayın.

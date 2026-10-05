---
title: "Yapay Zekâ ile Geçiş"
description: "Claude, ChatGPT veya GitHub Copilot gibi bir yapay zekâ asistanına AutoMapper'dan VeloxMapper'a geçişi yaptırmak için hazır, kopyalanabilir yönergeyi kullanın."
section: migration
order: 50
---

# Yapay Zekâ ile Geçiş

Geçişin mekanik kısmını [geçiş betiği](./migration-script.md) yapar. Betiğin kapsamadığı durumlarda (desteklenmeyen API'ler, özel istisna yönetimi, testlerin güncellenmesi) kod tabanına erişimi olan bir yapay zekâ asistanından yardım alabilirsiniz. Bu sayfadaki yönerge, asistanın doğru kaynağı kullanmasını ve doğrulama adımlarını atlamamasını sağlar.

## Makine tarafından okunabilir dokümantasyon

Bu site, yapay zekâ araçlarının okuyabileceği iki düz metin dosyası yayımlar:

| Dosya | İçerik |
| --- | --- |
| [`/llms.txt`](https://veloxmapper-website.netlify.app/llms.txt) | [llmstxt.org](https://llmstxt.org) biçiminde tüm sayfaların başlık, açıklama ve bağlantı listesi |
| [`/llms-full.txt`](https://veloxmapper-website.netlify.app/llms-full.txt) | Tüm dokümantasyonun tek dosyada birleştirilmiş Markdown metni |

Asistanınız web'e erişebiliyorsa `llms-full.txt` adresini verin. Erişemiyorsa dosyayı indirip sohbete ekleyin veya depoya geçici olarak koyun. Her sayfanın Markdown kopyası `/docs-md/<sayfa>.md` adresinde de bulunur (ör. [`/docs-md/behavior-differences.md`](https://veloxmapper-website.netlify.app/docs-md/behavior-differences.md)).

## Hazır yönerge

Aşağıdaki metni olduğu gibi kopyalayın. Kod tabanınızı okuyup düzenleyebilen bir asistanda (Claude Code, GitHub Copilot ajan modu, Cursor ve benzerleri) en iyi sonucu verir.

```text
Bu .NET çözümünü AutoMapper'dan VeloxMapper 6.0.0'a taşı.

REFERANS
- VeloxMapper dokümantasyonu: https://veloxmapper-website.netlify.app/llms-full.txt
  Bu dosyayı oku ve yalnızca orada belgelenmiş API'leri kullan. Belgede olmayan bir
  metot veya tür uydurma; emin olmadığın durumlarda bana sor.
- VeloxMapper, AutoMapper ile aynı tür ve metot adlarını kullanır (IMapper, Mapper,
  MapperConfiguration, Profile, CreateMap, ForMember, MapFrom, ReverseMap, ProjectTo,
  ResolutionContext, IValueResolver, IValueConverter, ITypeConverter, IMappingAction,
  AssertConfigurationIsValid). Bunları yeniden yazma.

YAPILACAK DEĞİŞİKLİKLER
1. Paketler:
   - "AutoMapper" paket referansını "VeloxMapper" Version="6.0.0" ile değiştir.
   - "AutoMapper.Extensions.Microsoft.DependencyInjection" ve
     "AutoMapper.Extensions.EnumMapping" referanslarını kaldır (VeloxMapper'da yerleşik).
   - Directory.Packages.props, Directory.Build.props ve packages.config dosyalarını da kontrol et.
2. Namespace'ler:
   - using AutoMapper;                          -> using VeloxMapper;
   - using AutoMapper.QueryableExtensions;      -> using VeloxMapper.QueryableExtensions;
   - using AutoMapper.Configuration.Annotations; -> using VeloxMapper.Configuration.Annotations;
   - using AutoMapper.Extensions.EnumMapping;   -> satırı sil
   - .cshtml/.razor dosyalarındaki @using AutoMapper satırlarını ve
     "using X = AutoMapper..." takma adlarını da güncelle.
3. Dependency injection:
   - services.AddAutoMapper(...) -> services.AddVeloxMapper(...) (aynı argümanlar).
4. İstisnalar:
   - AutoMapperMappingException             -> VeloxMapper.Exceptions.VeloxMappingException
   - AutoMapperConfigurationException       -> VeloxMapper.Exceptions.VeloxValidationException
   - DuplicateTypeMapConfigurationException -> VeloxMapper.Exceptions.VeloxConfigurationException
   - İstisna mesajı metnine bağlı kod veya test varsa bana listele; mesajlar farklıdır.
5. Tam nitelikli adlar: AutoMapper.IMapper gibi kullanımları VeloxMapper.IMapper yap.

DESTEKLENMEYEN API'LER (bulursan değiştirmeden önce bana planını anlat)
- cfg.Internal()             -> cfg.ForAllMaps / cfg.ForAllPropertyMaps ile yeniden yaz.
- UseAsDataSource            -> ProjectTo ile yeniden yaz.
- EqualityComparison         -> AutoMapper.Collection desteklenmez; koleksiyonu
                                 anahtara göre güncelleyen açık kod yaz.
- AsProxy / arayüz hedefleri -> somut hedef tür kullan.
- AutoMapper.Collection, AutoMapper.EF6, AutoMapper.Data,
  AutoMapper.Extensions.ExpressionMapping, AutoMapper.AspNetCore.OData paketleri.

DAVRANIŞ FARKLARI (kod değişikliği gerektirebilir, dokümandaki
"Davranış Farkları" sayfasına bak)
- ProjectTo, resolver/value converter/MapFrom((src, dest) => ...) kullanan üyeleri
  sorgudan çıkarır. ProjectTo ile kullanılan eşlemelerde bu tür kuralları bul ve listele.
- EnableNullPropagationForQueryMapping varsayılan olarak true'dur.
- Kendine referans veren modellerde ProjectTo için MaxDepth gerekir.
- Tanımlanmamış tür çiftleri hata vermek yerine konvansiyonla eşlenir.

DOĞRULAMA (her adımı çalıştır ve sonucu raporla)
1. dotnet build: hatasız olmalı. Hata varsa düzelt.
2. Tüm profilleri yükleyen ve AssertConfigurationIsValid() çağıran bir test yoksa ekle:
     var configuration = new MapperConfiguration(cfg => cfg.AddMaps(typeof(<BirProfil>)));
     configuration.AssertConfigurationIsValid();
   Test başarısızsa VeloxValidationException mesajındaki her maddeyi düzelt.
3. dotnet test: tüm testler geçmeli.
4. "dotnet list package --include-transitive" çıktısında AutoMapper kalmadığını doğrula.

ÇIKTI
- Değiştirdiğin dosyaların listesi.
- Elle karar verilmesi gereken noktaların listesi (desteklenmeyen API'ler,
  ProjectTo'dan çıkarılacak üyeler, mesaj metnine bağlı kod).
- Build ve test sonuçları.
Mantığı değiştiren hiçbir düzenlemeyi bana sormadan yapma.
```

## Önerilen kullanım

1. **Önce betiği çalıştırın.** [Geçiş betiği](./migration-script.md) mekanik değişiklikleri deterministik olarak yapar. Ardından asistanı yalnızca kalan derleme hataları ve uyarılar için kullanmak hem daha hızlı hem daha güvenilirdir.
2. **Ayrı bir dalda çalışın.** Asistanın yaptığı her değişikliği `git diff` ile inceleyin.
3. **Doğrulamayı atlamayın.** Yönergedeki `AssertConfigurationIsValid()` ve `dotnet test` adımları, asistanın gözden kaçırdığı hataları yakalar.
4. **Uydurma API'lere dikkat edin.** Asistan bu sayfalarda belgelenmemiş bir VeloxMapper metodu önerirse [API Eşleme Tablosu](./api-mapping.md) ile kontrol edin. Derlenmeyen kod genellikle var olmayan bir API'ye işaret eder.

## Daha kısa bir yönerge

Asistanınız dokümantasyonu doğrudan okuyabiliyorsa şu kısa yönerge de yeterlidir:

```text
https://veloxmapper-website.netlify.app/llms-full.txt adresindeki VeloxMapper
dokümantasyonunu oku. Ardından bu çözümü "Geçiş Rehberi" sayfasındaki adımlara göre
AutoMapper'dan VeloxMapper 6.0.0'a taşı. Yalnızca belgelenmiş API'leri kullan,
"Davranış Farkları" sayfasındaki maddeleri kod tabanında ara, sonunda dotnet build,
AssertConfigurationIsValid() testi ve dotnet test çalıştırıp sonuçları raporla.
```

# VeloxMapper Dokümantasyonu (Docusaurus)

Bu klasör, VeloxMapper için **Docusaurus** uyumlu, production-grade dokümantasyon kaynağıdır. Tüm içerik **yalnızca gerçek koda** dayanır (`src/` altındaki public API + mevcut website dokümanları). Kodda olmayan hiçbir API belgelenmemiştir.

## Klasör Yapısı

```
docs/
├── intro.md                     # Landing — VeloxMapper nedir, ne çözer
├── getting-started.md           # Kurulum + ilk çalışan örnek
├── core-concepts.md             # Mapping mantığı, config yaklaşımı, hibrit motor
├── api/
│   ├── mapper.md                # IVeloxMapper, IConfigurationProvider, Mapper
│   ├── configuration.md         # MapperConfiguration, VeloxMapperOptions, VeloxProfile, DI
│   ├── mapping-expression.md    # CreateMap zinciri (IMappingExpression)
│   ├── member-options.md        # ForMember seçenekleri (IMemberConfigurationExpression)
│   ├── interfaces.md            # Resolver / Converter / Action arayüzleri
│   ├── attributes.md            # [VeloxMap], [VeloxConstructor], [AutoMap]
│   └── diagnostics.md           # MappingPlanReport, gözlemlenebilirlik
├── extension-methods.md         # AddVeloxMapper, ProjectTo
├── advanced.md                  # Koleksiyon, flattening, koşullu, projeksiyon, polimorfizm
├── scenarios.md                 # DTO↔Entity, API response shaping, complex mapping
├── troubleshooting.md           # Gerçek hata senaryoları + debug
├── best-practices.md            # Clean Architecture, maintainability
├── migration.md                 # AutoMapper'dan adaptasyon rehberi
├── sidebars.js                  # Docusaurus sidebar konfigürasyonu
└── analysis-gaps.md             # Mevcut docs analizi + eksikler raporu
```

## Dosya İsimlendirme Standardı

- **kebab-case** `.md` dosyaları (`getting-started.md`, `member-options.md`).
- Her dosyanın başında Docusaurus **frontmatter** (`id`, `title`, `sidebar_label`, `description`).
- API referansı `api/` alt klasöründe, sahibi olan tür/arayüz adına göre gruplanır.
- Başlıklar SEO-dostu ve gezilebilir (`##` seviyesi API adıyla eşleşir).

## Doğruluk İlkesi

Her kod örneğinde **gerçek namespace ve sınıf isimleri** kullanılır: `VeloxMapper`, `VeloxMapper.Configuration`, `VeloxMapper.Abstractions`, `VeloxMapper.Attributes`, `VeloxMapper.DependencyInjection`, `VeloxMapper.Extensions`, `VeloxMapper.Diagnostics`. Bir API hakkında koddan doğrulanamayan bir bilgi varsa açıkça "yeterli bilgi yok" olarak işaretlenir.

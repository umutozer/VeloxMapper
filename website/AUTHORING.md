# Dokümantasyon Yazım Rehberi

VeloxMapper dokümantasyonu deponun kökündeki **`docs/`** klasöründe, düz Markdown dosyaları olarak yaşar.
Website bu dosyaları derleme sırasında statik HTML'e çevirir. Bu belge, sayfa yazarken kullanabileceğiniz
sözdizimini ve içeriğin siteye nasıl ulaştığını anlatır.

## İçerik akışı

```
docs/                     ← kanonik içerik (burayı düzenleyin)
  _meta.json              ← kenar çubuğu bölümleri ve sırası
  introduction.md         ← /docs/introduction
  api/imapper.md          ← /docs/api/imapper
  _legacy/                ← "_" ile başlayan klasörler siteye ALINMAZ

website/
  scripts/sync-docs.mjs   ← predev / prebuild adımında docs/ → content/docs/ kopyalar
  content/docs/           ← senkronize kopya (commit'lenir; website tek başına derlenebilsin diye)
```

- `npm run dev` ve `npm run build` öncesinde `scripts/sync-docs.mjs` otomatik çalışır (`predev` / `prebuild`).
  Elle çalıştırmak için: `npm run sync-docs`.
- `../docs` bulunamazsa betik hiçbir şey yapmaz ve commit'lenmiş `content/docs` kopyası kullanılır.
- Senkronizasyon `content/docs`'u her seferinde temizleyip yeniden oluşturur; **`content/docs` altını elle
  düzenlemeyin**, değişiklikler kaybolur.
- Kopyalanan dosyalar: `*.md` ve `_meta.json`. `_` veya `.` ile başlayan klasörler/dosyalar atlanır.

## Bölümler: `docs/_meta.json`

Kenar çubuğundaki bölümler ve sıraları bu dosyada tanımlanır:

```json
{
  "sections": [
    { "id": "getting-started", "title": "Başlarken" },
    { "id": "migration", "title": "AutoMapper'dan Geçiş" }
  ]
}
```

Bir sayfa, frontmatter'daki `section` alanıyla bu bölümlerden birine bağlanır. Bilinmeyen bir `section` değeri
derlemeyi **hata ile durdurur** (fail-fast).

## Sayfa: frontmatter

Her sayfa YAML frontmatter ile başlar:

```markdown
---
title: "Hızlı Başlangıç"
description: "İlk profilinizi tanımlayıp ilk eşlemenizi çalıştırın."
section: getting-started
order: 30
badge: "Yeni"
---

# Hızlı Başlangıç

Giriş paragrafı…
```

| Alan          | Zorunlu | Açıklama                                                                                    |
| ------------- | ------- | ------------------------------------------------------------------------------------------- |
| `title`       | Evet    | Sayfa başlığı. H1, kenar çubuğu, `<title>` (`<title> · VeloxMapper Docs`) ve aramada kullanılır. |
| `description` | Önerilir | Başlığın altındaki giriş metni, meta description, Open Graph ve `llms.txt` açıklaması.       |
| `section`     | Evet    | `_meta.json` içindeki bölüm `id`'si. Yoksa sayfa uyarıyla atlanır.                          |
| `order`       | Hayır   | Bölüm içi sıralama (küçükten büyüğe). Varsayılan 1000. Aralıklı numara (10, 20, 30…) önerilir. |
| `badge`       | Hayır   | Kenar çubuğunda başlığın yanında gösterilen kısa etiket (ör. `Yeni`, `Beta`).               |

**Slug / adres:** dosyanın `docs/`'a göre yolu, `.md` olmadan. `docs/quickstart.md` → `/docs/quickstart`,
`docs/api/imapper.md` → `/docs/api/imapper`. Yalnızca **bir seviye** alt klasör desteklenir.

**H1:** Gövdenin ilk satırı bir `# Başlık` ise sayfada gösterilmez (başlık frontmatter'dan basılır), ama ham
Markdown kopyalarında (`/docs-md/*.md`, `llms-full.txt`, "Bu sayfayı kopyala") korunur. H1 eklemeniz önerilir.

## Başlıklar ve içindekiler

- `##` (H2) ve `###` (H3) başlıklar sağdaki **"Bu sayfada"** listesine otomatik eklenir.
- Her başlık otomatik bir kimlik (id) alır: `## Kurulum Adımları` → `#kurulum-adımları`.
- Arama dizini sayfaları H2/H3 başlıklarına göre bölümler; sonuçlar ilgili başlığa doğrudan bağlanır.

## Bağlantılar

```markdown
[Hızlı başlangıç](./quickstart.md)           → /docs/quickstart
[IMapper](api/imapper.md#map)                 → /docs/api/imapper#map
[Geçiş rehberi](/docs/migration-guide)        → olduğu gibi
[GitHub](https://github.com/umutozer/VeloxMapper)  → yeni sekmede açılır
```

Göreli `.md` bağlantıları, dosyanın konumuna göre çözülüp site adresine çevrilir. GitHub'da da çalıştıkları için
bu biçim önerilir.

## Kod blokları

Dil belirtin; derleme anında Shiki ile renklendirilir (açık/koyu tema otomatik):

````markdown
```csharp
var dto = mapper.Map<OrderDto>(order);
```
````

Desteklenen diller: `csharp` (`cs`, `c#`), `bash` (`sh`, `shell`, `console`), `powershell` (`ps1`, `pwsh`),
`json`, `xml` (`csproj`), `html`, `diff`, `ts`/`typescript`, `tsx`, `js`, `yaml`. Bilinmeyen diller düz metin
olarak gösterilir.

**Başlık (dosya adı)** ve **satır vurgulama**:

````markdown
```csharp title="Program.cs" {3,5-6}
using VeloxMapper;

builder.Services.AddVeloxMapper(typeof(Program));
```
````

- `title="…"` kod bloğunun üst çubuğunda gösterilir (yoksa dil adı gösterilir).
- `{3,5-6}` belirtilen satırları vurgular.
- Her kod bloğunda otomatik bir **kopyala** düğmesi bulunur.

## Kod sekmeleri

Ardışık kod bloklarını `<!-- tabs -->` ve `<!-- /tabs -->` yorumları arasına alın. Sekme adları `title="…"`
değerinden gelir (yoksa dil adı):

````markdown
<!-- tabs -->
```csharp title="AutoMapper"
using AutoMapper;
services.AddAutoMapper(typeof(Program));
```
```csharp title="VeloxMapper"
using VeloxMapper;
services.AddVeloxMapper(typeof(Program));
```
<!-- /tabs -->
````

- Aynı ada sahip sekmeler sayfadaki **tüm sekme gruplarında senkronize** olur ve okuyucunun tercihi tarayıcıda
  hatırlanır (ör. okuyucu bir kez "VeloxMapper"ı seçerse tüm karşılaştırmalar o sekmede açılır). Bu nedenle
  karşılaştırmalarda her zaman aynı adları kullanın: **`AutoMapper`** ve **`VeloxMapper`**.
- Yorum satırları kendi satırlarında olmalı; aralarında kod bloğu dışında içerik kullanmayın (kullanılırsa sekme
  grubunun hemen altına taşınır).

## Uyarı kutuları (callout)

GitHub'ın uyarı sözdizimi desteklenir; GitHub'da da aynı şekilde görünür:

```markdown
> [!NOTE]
> Bilgilendirici not.

> [!TIP]
> Yararlı bir ipucu.

> [!IMPORTANT]
> Bilinmesi gereken önemli bilgi.

> [!WARNING]
> Dikkat edilmezse sorun çıkarabilecek durum.

> [!CAUTION]
> Veri kaybı veya hatalı davranış riski.
```

Başlıklar Türkçe gösterilir: Not, İpucu, Önemli, Uyarı, Dikkat. Özel başlık için işaretin yanına yazın:
`> [!WARNING] Davranış farkı`.

## Diğer özellikler

- **GFM tabloları** — dar ekranlarda tablo kendi içinde yatay kaydırılır.
- **Görev listeleri** — `- [x] Tamamlandı`, `- [ ] Bekliyor`.
- **Ham HTML** — `<details>`, `<kbd>`, `<br>` gibi basit HTML etiketleri kullanılabilir. JSX/MDX desteklenmez.
- **Görseller** — `![Açıklama](https://…)`; yerel görseller için `website/public/` altına koyup `/dosya.png`
  ile bağlayın.

## Otomatik üretilen çıktılar

Derleme her sayfa için şunları üretir:

| Çıktı                          | Açıklama                                                             |
| ------------------------------ | -------------------------------------------------------------------- |
| `/docs/<slug>`                 | HTML sayfa (`/docs` → `introduction` sayfası)                         |
| `/docs-md/<slug>.md`           | Ham Markdown kopyası ("Markdown olarak görüntüle")                    |
| `/llms.txt`                    | [llmstxt.org](https://llmstxt.org) biçiminde sayfa dizini              |
| `/llms-full.txt`               | Tüm dokümantasyonun tek dosyada birleşimi                             |
| `/search-index.json`           | İstemci tarafı arama dizini (⌘K / Ctrl+K)                             |
| `/sitemap.xml`, `/robots.txt`  | Arama motorları için                                                  |

## Sürüm numarası

Sitede gösterilen sürüm `website/src/lib/version.ts` içindeki `APP_VERSION` sabitinden gelir; NuGet paket
sürümüyle senkron tutun.

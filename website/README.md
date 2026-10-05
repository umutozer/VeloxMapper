# VeloxMapper Website

VeloxMapper tanıtım sitesi ve dokümantasyonu. Next.js (App Router) ile statik olarak dışa aktarılır
(`output: "export"`) ve Netlify'da `out/` klasöründen yayımlanır.

## Komutlar

```bash
npm install
npm run dev        # docs senkronizasyonu + geliştirme sunucusu
npm run build      # docs senkronizasyonu + statik çıktı (out/)
npm run lint
npm run sync-docs  # yalnızca ../docs → content/docs kopyası
```

## Yapı

- `../docs/` — kanonik dokümantasyon içeriği (Markdown). Yazım kuralları: [AUTHORING.md](./AUTHORING.md)
- `content/docs/` — `scripts/sync-docs.mjs` tarafından üretilen kopya (elle düzenlemeyin)
- `src/app/(marketing)/` — ana sayfa
- `src/app/docs/` — dokümantasyon sayfaları (`/docs/[...slug]`)
- `src/app/docs-md/`, `llms.txt/`, `llms-full.txt/`, `search-index.json/` — derleme zamanı üretilen dosyalar
- `src/lib/docs/` — içerik yükleme, Markdown hattı (unified + Shiki), arama dizini, llms.txt
- `src/lib/version.ts` — sitede gösterilen sürüm (`APP_VERSION`)

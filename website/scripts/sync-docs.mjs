#!/usr/bin/env node
/**
 * Dokümantasyon senkronizasyonu.
 *
 * Kanonik içerik deponun kökündeki `../docs` klasöründe yaşar. Bu betik
 * `predev` / `prebuild` adımında çalışır ve oradaki `*.md` dosyalarını ve
 * `_meta.json`'ı `website/content/docs/` altına kopyalar.
 *
 * - `../docs` yoksa (ör. website tek başına derleniyorsa) hiçbir şey yapmaz;
 *   `content/docs` altındaki commit'lenmiş kopya kullanılır.
 * - `_` ile başlayan klasörler (ör. `_legacy`) ve dosyalar (`_meta.json` hariç)
 *   atlanır.
 * - Hedef klasör her senkronizasyonda temizlenir; böylece silinen sayfalar
 *   sitede kalmaz.
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const websiteRoot = path.resolve(here, "..");
const sourceDir = path.resolve(websiteRoot, "..", "docs");
const targetDir = path.resolve(websiteRoot, "content", "docs");

function isIncluded(name, isDir) {
  if (name.startsWith(".")) return false;
  if (isDir) return !name.startsWith("_");
  if (name === "_meta.json") return true;
  if (name.startsWith("_")) return false;
  return name.toLowerCase().endsWith(".md");
}

function copyTree(from, to) {
  let count = 0;
  for (const entry of fs.readdirSync(from, { withFileTypes: true })) {
    if (!isIncluded(entry.name, entry.isDirectory())) continue;
    const src = path.join(from, entry.name);
    const dest = path.join(to, entry.name);
    if (entry.isDirectory()) {
      count += copyTree(src, dest);
    } else if (entry.isFile()) {
      fs.mkdirSync(path.dirname(dest), { recursive: true });
      // Satır sonlarını normalize et: Windows/Unix arasında gereksiz diff oluşmasın.
      const text = fs.readFileSync(src, "utf8").replace(/\r\n/g, "\n");
      fs.writeFileSync(dest, text, "utf8");
      count++;
    }
  }
  return count;
}

if (!fs.existsSync(sourceDir) || !fs.statSync(sourceDir).isDirectory()) {
  console.log(`[sync-docs] ${sourceDir} bulunamadı — commit'lenmiş content/docs kopyası kullanılacak.`);
  process.exit(0);
}

if (!fs.existsSync(path.join(sourceDir, "_meta.json"))) {
  console.error(`[sync-docs] HATA: ${path.join(sourceDir, "_meta.json")} bulunamadı.`);
  process.exit(1);
}

fs.rmSync(targetDir, { recursive: true, force: true });
fs.mkdirSync(targetDir, { recursive: true });
const copied = copyTree(sourceDir, targetDir);
console.log(`[sync-docs] ${copied} dosya ${path.relative(websiteRoot, targetDir)} klasörüne kopyalandı.`);

// Geçiş betiğini siteden indirilebilir yap: /migrate-from-automapper.ps1
const scriptSource = path.join(websiteRoot, "..", "tools", "migrate-from-automapper.ps1");
if (fs.existsSync(scriptSource)) {
  fs.copyFileSync(scriptSource, path.join(websiteRoot, "public", "migrate-from-automapper.ps1"));
  console.log("[sync-docs] tools/migrate-from-automapper.ps1 → public/ kopyalandı.");
}

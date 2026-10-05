import "server-only";
import fs from "node:fs";
import path from "node:path";
import matter from "gray-matter";
import type { NavSection } from "./types";

export type { NavItem, NavSection } from "./types";

/**
 * Dokümantasyon kaynağı: `content/docs` altındaki Markdown dosyalarını okur,
 * frontmatter'ı doğrular ve `_meta.json` sırasına göre gezinme ağacını kurar.
 * `content/docs`, `scripts/sync-docs.mjs` tarafından deponun kökündeki
 * `docs/` klasöründen senkronize edilir.
 */

export const DOCS_DIR = path.join(process.cwd(), "content", "docs");

export interface DocSectionMeta {
  id: string;
  title: string;
}

export interface Doc {
  /** URL yolu, ör. `quickstart` veya `api/imapper`. */
  slug: string;
  /** `docs/` köküne göre dosya yolu, ör. `api/imapper.md`. */
  filePath: string;
  title: string;
  description: string;
  section: string;
  sectionTitle: string;
  order: number;
  badge?: string;
  /** Frontmatter'sız Markdown gövdesi. */
  body: string;
}


interface DocsStore {
  sections: DocSectionMeta[];
  docs: Doc[]; // gezinme sırasına göre
  bySlug: Map<string, Doc>;
}

function walk(dir: string, prefix = ""): string[] {
  const out: string[] = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name.startsWith("_") || entry.name.startsWith(".")) continue;
    const rel = prefix ? `${prefix}/${entry.name}` : entry.name;
    if (entry.isDirectory()) {
      // Yalnızca bir seviye alt klasör desteklenir (ör. api/imapper).
      if (prefix) continue;
      out.push(...walk(path.join(dir, entry.name), rel));
    } else if (entry.isFile() && entry.name.toLowerCase().endsWith(".md")) {
      out.push(rel);
    }
  }
  return out;
}

function loadStore(): DocsStore {
  const metaPath = path.join(DOCS_DIR, "_meta.json");
  if (!fs.existsSync(metaPath)) {
    throw new Error(`[docs] ${metaPath} bulunamadı. 'npm run sync-docs' çalıştırın.`);
  }
  const meta = JSON.parse(fs.readFileSync(metaPath, "utf8")) as { sections?: DocSectionMeta[] };
  const sections = meta.sections ?? [];
  if (sections.length === 0) throw new Error("[docs] _meta.json içinde 'sections' boş.");
  const sectionIndex = new Map(sections.map((s, i) => [s.id, i]));

  const docs: Doc[] = [];
  for (const rel of walk(DOCS_DIR)) {
    const raw = fs.readFileSync(path.join(DOCS_DIR, rel), "utf8").replace(/\r\n/g, "\n");
    const { data, content } = matter(raw);
    const slug = rel.replace(/\.md$/i, "");

    if (!data.section) {
      console.warn(`[docs] '${rel}' atlandı: frontmatter içinde 'section' yok.`);
      continue;
    }
    const sIdx = sectionIndex.get(String(data.section));
    if (sIdx === undefined) {
      throw new Error(
        `[docs] '${rel}': bilinmeyen section '${data.section}'. Geçerli değerler: ${sections.map((s) => s.id).join(", ")}`
      );
    }
    if (!data.title) throw new Error(`[docs] '${rel}': frontmatter içinde 'title' zorunludur.`);

    docs.push({
      slug,
      filePath: rel,
      title: String(data.title),
      description: data.description ? String(data.description) : "",
      section: String(data.section),
      sectionTitle: sections[sIdx].title,
      order: typeof data.order === "number" ? data.order : Number(data.order ?? 1000) || 1000,
      badge: data.badge ? String(data.badge) : undefined,
      body: content.replace(/^\n+/, ""),
    });
  }

  docs.sort((a, b) => {
    const s = sectionIndex.get(a.section)! - sectionIndex.get(b.section)!;
    if (s !== 0) return s;
    if (a.order !== b.order) return a.order - b.order;
    return a.slug.localeCompare(b.slug);
  });

  return { sections, docs, bySlug: new Map(docs.map((d) => [d.slug, d])) };
}

let cached: DocsStore | null = null;

function store(): DocsStore {
  // Geliştirme ortamında içerik değişikliklerinin anında görünmesi için önbelleği atla.
  if (process.env.NODE_ENV !== "production") return loadStore();
  if (!cached) cached = loadStore();
  return cached;
}

export function getAllDocs(): Doc[] {
  return store().docs;
}

export function getDoc(slug: string): Doc | undefined {
  return store().bySlug.get(slug);
}

export function getNavigation(): NavSection[] {
  const { sections, docs } = store();
  return sections
    .map((s) => ({
      id: s.id,
      title: s.title,
      items: docs
        .filter((d) => d.section === s.id)
        .map((d) => ({ slug: d.slug, title: d.title, badge: d.badge })),
    }))
    .filter((s) => s.items.length > 0);
}

export function getPrevNext(slug: string): { prev?: Doc; next?: Doc } {
  const docs = store().docs;
  const i = docs.findIndex((d) => d.slug === slug);
  if (i < 0) return {};
  return { prev: docs[i - 1], next: docs[i + 1] };
}

export const DEFAULT_DOC_SLUG = "introduction";

/** "Kopyala" ve `/docs-md/*.md` için LLM dostu Markdown: H1 garanti edilir. */
export function toRawMarkdown(doc: Doc): string {
  const body = doc.body.trimEnd() + "\n";
  if (/^#\s+/.test(body)) return body;
  const lead = doc.description ? `> ${doc.description}\n\n` : "";
  return `# ${doc.title}\n\n${lead}${body}`;
}

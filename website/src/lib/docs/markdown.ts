import "server-only";
import path from "node:path";
import { unified, type Plugin } from "unified";
import remarkParse from "remark-parse";
import remarkGfm from "remark-gfm";
import remarkRehype from "remark-rehype";
import rehypeRaw from "rehype-raw";
import GithubSlugger from "github-slugger";
import rehypeAutolinkHeadings from "rehype-autolink-headings";
import { visit, SKIP } from "unist-util-visit";
import { toString as hastToString } from "hast-util-to-string";
import type { Root as MdastRoot, Blockquote, Code, Parent as MdastParent, RootContent } from "mdast";
import type { Root as HastRoot, Element, ElementContent } from "hast";
import type { TocItem } from "./types";
import { highlightToHast, langLabel, normalizeLang, parseLineRanges } from "./highlight";

/**
 * Markdown → hast hattı.
 *
 * Desteklenen yazım özellikleri (ayrıntılar: website/AUTHORING.md):
 *  - GFM (tablolar, görev listeleri, üstü çizili, otomatik bağlantılar)
 *  - GitHub tarzı uyarılar: `> [!NOTE]`, `[!TIP]`, `[!IMPORTANT]`, `[!WARNING]`, `[!CAUTION]`
 *  - Başlıklı kod blokları: ```csharp title="Program.cs" {3-4}
 *  - Kod sekmeleri: `<!-- tabs -->` ... `<!-- /tabs -->` arasındaki ardışık kod blokları
 *  - Göreli `.md` bağlantıları `/docs/<slug>` adreslerine çevrilir
 */


export interface ProcessedDoc {
  tree: HastRoot;
  toc: TocItem[];
}

// ---------------------------------------------------------------- remark

/** Gövdenin başındaki H1'i kaldırır; başlık sayfa düzeninde frontmatter'dan basılır. */
const remarkStripLeadingH1: Plugin<[], MdastRoot> = () => (tree) => {
  const first = tree.children[0];
  if (first && first.type === "heading" && first.depth === 1) tree.children.shift();
};

const ALERT_LABELS: Record<string, string> = {
  note: "Not",
  tip: "İpucu",
  important: "Önemli",
  warning: "Uyarı",
  caution: "Dikkat",
};

/** `> [!NOTE]` biçimindeki GitHub uyarılarını `<doc-callout>` öğesine dönüştürür. */
const remarkAlerts: Plugin<[], MdastRoot> = () => (tree) => {
  visit(tree, "blockquote", (node: Blockquote) => {
    const para = node.children[0];
    if (!para || para.type !== "paragraph") return;
    const firstText = para.children[0];
    if (!firstText || firstText.type !== "text") return;
    const m = firstText.value.match(/^\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]([^\n]*)\n?/i);
    if (!m) return;
    const type = m[1].toLowerCase();
    const customTitle = m[2].trim();
    firstText.value = firstText.value.slice(m[0].length);
    if (!firstText.value) para.children.shift();
    // `[!NOTE]` sonrası satır kırılımı (break) kaldıysa temizle.
    if (para.children[0]?.type === "break") para.children.shift();
    if (para.children.length === 0) node.children.shift();

    node.data = {
      ...node.data,
      hName: "doc-callout",
      hProperties: { dataType: type, dataTitle: customTitle || ALERT_LABELS[type] },
    };
  });
};

function parseMeta(meta: string | null | undefined) {
  const out: { title?: string; highlight?: string } = {};
  if (!meta) return out;
  const title = meta.match(/title=(?:"([^"]*)"|'([^']*)'|(\S+))/);
  if (title) out.title = title[1] ?? title[2] ?? title[3];
  const hl = meta.match(/\{([\d,\s-]+)\}/);
  if (hl) out.highlight = hl[1].replace(/\s+/g, "");
  return out;
}

function codeToBlockNode(code: Code, inTabs: boolean): RootContent {
  const lang = normalizeLang(code.lang);
  const meta = parseMeta(code.meta);
  return {
    type: "codeBlock",
    data: {
      hName: "code-block",
      hProperties: {
        dataLang: lang,
        dataLabel: langLabel(lang),
        dataTitle: meta.title ?? "",
        dataHighlight: meta.highlight ?? "",
        dataInTabs: inTabs ? "true" : undefined,
      },
      hChildren: [{ type: "text", value: code.value }],
    },
  } as unknown as RootContent;
}

const TABS_OPEN = /^<!--\s*tabs\s*-->$/i;
const TABS_CLOSE = /^<!--\s*\/tabs\s*-->$/i;

/** Ardışık kod bloklarını `<!-- tabs -->` / `<!-- /tabs -->` arasında sekmelere toplar. */
const remarkCodeBlocks: Plugin<[], MdastRoot> = () => (tree) => {
  const transformParent = (parent: MdastParent) => {
    const out: RootContent[] = [];
    const kids = parent.children as RootContent[];
    for (let i = 0; i < kids.length; i++) {
      const node = kids[i];
      if (node.type === "html" && TABS_OPEN.test(node.value.trim())) {
        const codes: Code[] = [];
        const rest: RootContent[] = [];
        let j = i + 1;
        for (; j < kids.length; j++) {
          const n = kids[j];
          if (n.type === "html" && TABS_CLOSE.test(n.value.trim())) break;
          if (n.type === "code") codes.push(n);
          else rest.push(n);
        }
        const titles = codes.map((c, idx) => parseMeta(c.meta).title || langLabel(normalizeLang(c.lang)) || `Sekme ${idx + 1}`);
        if (codes.length > 0) {
          out.push({
            type: "codeTabs",
            data: {
              hName: "code-tabs",
              hProperties: { dataTabs: JSON.stringify(titles) },
            },
            children: codes.map((c) => codeToBlockNode(c, true)),
          } as unknown as RootContent);
        }
        out.push(...rest);
        i = j; // kapanış yorumunu da atla
        continue;
      }
      if (node.type === "code") {
        out.push(codeToBlockNode(node, false));
        continue;
      }
      if ("children" in node && Array.isArray(node.children)) transformParent(node as MdastParent);
      out.push(node);
    }
    parent.children = out as MdastParent["children"];
  };
  transformParent(tree);
};

// ---------------------------------------------------------------- rehype

/** `<code-block>` öğelerini Shiki ile renklendirir. */
const rehypeHighlight: Plugin<[], HastRoot> = () => async (tree) => {
  const blocks: Element[] = [];
  visit(tree, "element", (node: Element) => {
    if (node.tagName === "code-block") {
      blocks.push(node);
      return SKIP;
    }
  });
  await Promise.all(
    blocks.map(async (node) => {
      const code = hastToString(node);
      const lang = String(node.properties.dataLang ?? "text");
      const pre = await highlightToHast(code, lang, {
        highlightLines: parseLineRanges(String(node.properties.dataHighlight ?? "")),
      });
      node.children = [pre as ElementContent];
    })
  );
};

/** Göreli `.md` bağlantılarını site adreslerine çevirir; dış bağlantıları yeni sekmede açar. */
const rehypeLinks: Plugin<[{ slug: string }], HastRoot> = ({ slug }) => (tree) => {
  const baseDir = path.posix.dirname(slug);
  visit(tree, "element", (node: Element) => {
    if (node.tagName !== "a") return;
    const href = typeof node.properties.href === "string" ? node.properties.href : "";
    if (!href) return;
    if (/^https?:\/\//i.test(href)) {
      node.properties.target = "_blank";
      node.properties.rel = ["noopener", "noreferrer"];
      return;
    }
    const m = href.match(/^([^#?]+)\.md(#.*)?$/i);
    if (!m || /^[a-z]+:/i.test(href)) return;
    let target = m[1];
    if (target.startsWith("/")) {
      target = target.replace(/^\/(docs\/)?/, "");
    } else {
      target = path.posix.normalize(path.posix.join(baseDir === "." ? "" : baseDir, target));
    }
    node.properties.href = `/docs/${target}${m[2] ?? ""}`;
  });
};

function extractToc(tree: HastRoot): TocItem[] {
  const toc: TocItem[] = [];
  visit(tree, "element", (node: Element) => {
    if ((node.tagName === "h2" || node.tagName === "h3") && node.properties.id) {
      toc.push({
        id: String(node.properties.id),
        text: hastToString(node).replace(/#$/, "").trim(),
        depth: node.tagName === "h2" ? 2 : 3,
      });
    }
  });
  return toc;
}


/** Başlıklara kimlik ekler. "İ" harfi "i" olarak normalize edilir (aksi halde "i̇" birleşik nokta karakteri oluşur). */
const rehypeTurkishSlug: Plugin<[], HastRoot> = () => (tree) => {
  const slugger = new GithubSlugger();
  visit(tree, "element", (node: Element) => {
    if (!/^h[1-6]$/.test(node.tagName) || node.properties?.id) return;
    const text = hastToString(node).replace(/İ/g, "I");
    node.properties = { ...node.properties, id: slugger.slug(text) };
  });
};
function baseProcessor(slug: string) {
  return unified()
    .use(remarkParse)
    .use(remarkGfm)
    .use(remarkStripLeadingH1)
    .use(remarkAlerts)
    .use(remarkCodeBlocks)
    .use(remarkRehype, { allowDangerousHtml: true })
    .use(rehypeRaw)
    .use(rehypeTurkishSlug)
    .use(rehypeLinks, { slug });
}

/** Sayfa render'ı için tam hat: renklendirme + başlık bağlantıları + içindekiler. */
export async function processMarkdown(markdown: string, slug: string): Promise<ProcessedDoc> {
  const processor = baseProcessor(slug)
    .use(rehypeHighlight)
    .use(rehypeAutolinkHeadings, {
      behavior: "append",
      test: ["h2", "h3", "h4"],
      properties: { className: ["heading-anchor"], ariaHidden: "true", tabIndex: -1 },
      content: { type: "text", value: "#" },
    });
  const mdast = processor.parse(markdown);
  const tree = (await processor.run(mdast)) as HastRoot;
  return { tree, toc: extractToc(tree) };
}

export interface SearchSection {
  /** Başlık id'si; sayfanın girişi için boş. */
  anchor: string;
  heading: string;
  text: string;
}

/** Arama dizini için hafif hat: sayfayı h2/h3 başlıklarına göre bölümlere ayırır. */
export async function extractSearchSections(markdown: string, slug: string): Promise<SearchSection[]> {
  const processor = baseProcessor(slug);
  const tree = (await processor.run(processor.parse(markdown))) as HastRoot;
  const sections: SearchSection[] = [{ anchor: "", heading: "", text: "" }];
  for (const node of tree.children) {
    if (node.type !== "element") continue;
    if ((node.tagName === "h2" || node.tagName === "h3") && node.properties.id) {
      sections.push({ anchor: String(node.properties.id), heading: hastToString(node).trim(), text: "" });
      continue;
    }
    const text = hastToString(node).replace(/\s+/g, " ").trim();
    if (text) {
      const current = sections[sections.length - 1];
      current.text = current.text ? `${current.text} ${text}` : text;
    }
  }
  return sections.filter((s) => s.heading || s.text);
}


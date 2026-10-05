import "server-only";
import type { Element, Root } from "hast";
import { createHighlighterCore, type HighlighterCore, type ShikiTransformer } from "shiki/core";
import { createJavaScriptRegexEngine } from "shiki/engine/javascript";

/**
 * Derleme zamanı sözdizimi renklendirme (Shiki). Açık ve koyu tema renkleri
 * CSS değişkenleri olarak (`--shiki-light` / `--shiki-dark`) üretilir; tema
 * geçişi tamamen CSS ile yapılır, istemciye JavaScript gönderilmez.
 */

export const SHIKI_THEMES = { light: "github-light-default", dark: "github-dark-default" } as const;

const LANG_ALIASES: Record<string, string> = {
  cs: "csharp",
  "c#": "csharp",
  csharp: "csharp",
  sh: "bash",
  shell: "bash",
  console: "bash",
  terminal: "bash",
  zsh: "bash",
  bash: "bash",
  ps: "powershell",
  ps1: "powershell",
  pwsh: "powershell",
  powershell: "powershell",
  json: "json",
  jsonc: "json",
  xml: "xml",
  csproj: "xml",
  html: "html",
  diff: "diff",
  ts: "typescript",
  typescript: "typescript",
  tsx: "tsx",
  js: "javascript",
  javascript: "javascript",
  mjs: "javascript",
  yaml: "yaml",
  yml: "yaml",
};

const LANG_LABELS: Record<string, string> = {
  csharp: "C#",
  bash: "Terminal",
  powershell: "PowerShell",
  json: "JSON",
  xml: "XML",
  html: "HTML",
  diff: "Diff",
  typescript: "TypeScript",
  tsx: "TSX",
  javascript: "JavaScript",
  yaml: "YAML",
  text: "Metin",
};

export function normalizeLang(lang: string | null | undefined): string {
  if (!lang) return "text";
  return LANG_ALIASES[lang.toLowerCase()] ?? "text";
}

export function langLabel(lang: string): string {
  return LANG_LABELS[lang] ?? lang.toUpperCase();
}

let highlighterPromise: Promise<HighlighterCore> | null = null;

function getHighlighter(): Promise<HighlighterCore> {
  if (!highlighterPromise) {
    highlighterPromise = createHighlighterCore({
      themes: [
        import("shiki/themes/github-light-default.mjs"),
        import("shiki/themes/github-dark-default.mjs"),
      ],
      langs: [
        import("shiki/langs/csharp.mjs"),
        import("shiki/langs/bash.mjs"),
        import("shiki/langs/powershell.mjs"),
        import("shiki/langs/json.mjs"),
        import("shiki/langs/xml.mjs"),
        import("shiki/langs/html.mjs"),
        import("shiki/langs/diff.mjs"),
        import("shiki/langs/typescript.mjs"),
        import("shiki/langs/tsx.mjs"),
        import("shiki/langs/javascript.mjs"),
        import("shiki/langs/yaml.mjs"),
      ],
      engine: createJavaScriptRegexEngine(),
    });
  }
  return highlighterPromise;
}

/** `{1,3-5}` biçimindeki satır aralıklarını çözümler. */
export function parseLineRanges(spec: string | undefined | null): Set<number> {
  const lines = new Set<number>();
  if (!spec) return lines;
  for (const part of spec.split(",")) {
    const m = part.trim().match(/^(\d+)(?:-(\d+))?$/);
    if (!m) continue;
    const start = Number(m[1]);
    const end = m[2] ? Number(m[2]) : start;
    for (let i = start; i <= end; i++) lines.add(i);
  }
  return lines;
}

function lineHighlighter(lines: Set<number>): ShikiTransformer {
  return {
    name: "velox:line-highlight",
    line(node, line) {
      if (lines.has(line)) this.addClassToHast(node, "line--highlighted");
    },
  };
}

export interface HighlightOptions {
  highlightLines?: Set<number>;
}

/** Kodu renklendirip `<pre>` hast öğesi döndürür. */
export async function highlightToHast(code: string, lang: string, options: HighlightOptions = {}): Promise<Element> {
  const highlighter = await getHighlighter();
  const resolved = normalizeLang(lang);
  const root = highlighter.codeToHast(code.replace(/\n$/, ""), {
    lang: resolved,
    themes: SHIKI_THEMES,
    defaultColor: false,
    transformers: options.highlightLines?.size ? [lineHighlighter(options.highlightLines)] : [],
  }) as Root;
  const pre = root.children.find((n): n is Element => n.type === "element" && n.tagName === "pre");
  if (!pre) throw new Error("Shiki <pre> üretmedi.");
  // Arka plan ve renkleri site temasından alıyoruz; Shiki'nin inline stilini sadeleştir.
  delete pre.properties.style;
  pre.properties.tabIndex = 0;
  return pre;
}

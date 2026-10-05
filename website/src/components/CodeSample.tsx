import "server-only";
import type { Root } from "hast";
import CodeBlock from "@/components/docs/CodeBlock";
import { highlightToHast, langLabel, normalizeLang, parseLineRanges } from "@/lib/docs/highlight";
import { renderHast } from "@/lib/docs/render";

interface CodeSampleProps {
  code: string;
  lang: string;
  title?: string;
  /** `{1,3-4}` biçiminde vurgulanacak satırlar. */
  highlight?: string;
  inTabs?: boolean;
}

/** Derleme zamanında Shiki ile renklendirilen kod örneği (sunucu bileşeni). */
export default async function CodeSample({ code, lang, title, highlight, inTabs }: CodeSampleProps) {
  const resolved = normalizeLang(lang);
  const pre = await highlightToHast(code, resolved, { highlightLines: parseLineRanges(highlight) });
  const tree: Root = { type: "root", children: [pre] };
  return (
    <CodeBlock lang={resolved} label={langLabel(resolved)} title={title} inTabs={inTabs}>
      {renderHast(tree)}
    </CodeBlock>
  );
}

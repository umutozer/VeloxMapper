import { getAllDocs, getDoc, toRawMarkdown } from "@/lib/docs/source";

export const dynamic = "force-static";
export const dynamicParams = false;

/** Her sayfanın ham Markdown kopyası: /docs-md/<slug>.md */
export function generateStaticParams() {
  return getAllDocs().map((doc) => {
    const parts = doc.slug.split("/");
    parts[parts.length - 1] = `${parts[parts.length - 1]}.md`;
    return { slug: parts };
  });
}

export async function GET(_req: Request, { params }: { params: Promise<{ slug: string[] }> }) {
  const { slug } = await params;
  const path = slug.join("/").replace(/\.md$/, "");
  const doc = getDoc(path);
  if (!doc) return new Response("Bulunamadı", { status: 404 });
  return new Response(toRawMarkdown(doc), {
    headers: { "Content-Type": "text/markdown; charset=utf-8" },
  });
}

import type { Metadata } from "next";
import { notFound } from "next/navigation";
import DocPage from "@/components/docs/DocPage";
import { DEFAULT_DOC_SLUG, getDoc } from "@/lib/docs/source";
import { docMetadata } from "@/lib/docs/metadata";

/** /docs → giriş sayfası (kanonik adres /docs/introduction). */
export function generateMetadata(): Metadata {
  const doc = getDoc(DEFAULT_DOC_SLUG);
  return doc ? docMetadata(doc) : {};
}

export default function DocsIndex() {
  const doc = getDoc(DEFAULT_DOC_SLUG);
  if (!doc) notFound();
  return <DocPage doc={doc} />;
}

import type { Metadata } from "next";
import { notFound } from "next/navigation";
import DocPage from "@/components/docs/DocPage";
import { getAllDocs, getDoc } from "@/lib/docs/source";
import { docMetadata } from "@/lib/docs/metadata";

export const dynamicParams = false;

interface PageProps {
  params: Promise<{ slug: string[] }>;
}

export function generateStaticParams() {
  return getAllDocs().map((doc) => ({ slug: doc.slug.split("/") }));
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const doc = getDoc(slug.join("/"));
  return doc ? docMetadata(doc) : {};
}

export default async function Page({ params }: PageProps) {
  const { slug } = await params;
  const doc = getDoc(slug.join("/"));
  if (!doc) notFound();
  return <DocPage doc={doc} />;
}

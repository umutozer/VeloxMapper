import type { Metadata } from "next";
import type { Doc } from "./source";

const OG_IMAGE = {
  url: "/opengraph-image",
  width: 1200,
  height: 630,
  alt: "VeloxMapper — AutoMapper uyumlu, MIT lisanslı .NET nesne eşleme kütüphanesi",
};

export function docMetadata(doc: Doc): Metadata {
  const title = `${doc.title} · VeloxMapper Docs`;
  const url = `/docs/${doc.slug}`;
  return {
    title,
    description: doc.description || undefined,
    alternates: {
      canonical: url,
      types: { "text/markdown": `/docs-md/${doc.slug}.md` },
    },
    openGraph: {
      type: "article",
      title,
      description: doc.description || undefined,
      url,
      siteName: "VeloxMapper",
      locale: "tr_TR",
      images: [OG_IMAGE],
    },
    twitter: {
      card: "summary_large_image",
      title,
      description: doc.description || undefined,
      images: [OG_IMAGE.url],
    },
  };
}

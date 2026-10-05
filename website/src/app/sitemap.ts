import type { MetadataRoute } from "next";
import { getAllDocs } from "@/lib/docs/source";
import { SITE_URL } from "@/lib/site";

export const dynamic = "force-static";

export default function sitemap(): MetadataRoute.Sitemap {
  return [
    { url: `${SITE_URL}/`, changeFrequency: "weekly", priority: 1 },
    { url: `${SITE_URL}/docs/introduction`, changeFrequency: "weekly", priority: 0.9 },
    ...getAllDocs()
      .filter((d) => d.slug !== "introduction")
      .map((d) => ({
        url: `${SITE_URL}/docs/${d.slug}`,
        changeFrequency: "weekly" as const,
        priority: d.section === "getting-started" || d.section === "migration" ? 0.8 : 0.6,
      })),
  ];
}

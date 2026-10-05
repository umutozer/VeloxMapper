import "server-only";
import { getAllDocs } from "./source";
import { extractSearchSections } from "./markdown";

/** İstemci tarafı aramada kullanılan kayıt. */
export interface SearchRecord {
  id: string;
  /** Hedef adres (ör. `/docs/quickstart#kurulum`). */
  url: string;
  page: string;
  section: string;
  heading: string;
  text: string;
}

const MAX_TEXT = 1800;

export async function buildSearchIndex(): Promise<SearchRecord[]> {
  const records: SearchRecord[] = [];
  for (const doc of getAllDocs()) {
    const sections = await extractSearchSections(doc.body, doc.slug);
    sections.forEach((s, i) => {
      const intro = !s.heading;
      records.push({
        id: `${doc.slug}#${s.anchor || i}`,
        url: `/docs/${doc.slug}${s.anchor ? `#${s.anchor}` : ""}`,
        page: doc.title,
        section: doc.sectionTitle,
        heading: s.heading,
        text: (intro && doc.description && !s.text.includes(doc.description) ? `${doc.description} ${s.text}` : s.text).slice(0, MAX_TEXT),
      });
    });
    if (!sections.some((s) => !s.heading)) {
      records.push({
        id: `${doc.slug}#`,
        url: `/docs/${doc.slug}`,
        page: doc.title,
        section: doc.sectionTitle,
        heading: "",
        text: doc.description,
      });
    }
  }
  return records;
}

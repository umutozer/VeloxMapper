import "server-only";
import { SITE_DESCRIPTION, SITE_URL, GITHUB_URL, NUGET_URL } from "@/lib/site";
import { APP_VERSION } from "@/lib/version";
import { getAllDocs, getNavigation, getDoc, toRawMarkdown } from "./source";

/** llmstxt.org biçiminde dizin dosyası. */
export function buildLlmsTxt(): string {
  const lines: string[] = [
    "# VeloxMapper",
    "",
    `> ${SITE_DESCRIPTION}`,
    "",
    `VeloxMapper ${APP_VERSION}, AutoMapper ile aynı API isimlerini (Profile, CreateMap, ForMember, MapFrom, IMapper, MapperConfiguration, ProjectTo, AssertConfigurationIsValid) kullanan MIT lisanslı bir .NET nesne eşleme kütüphanesidir. Geçişte çoğunlukla NuGet paketini değiştirmek ve \`using AutoMapper;\` satırlarını \`using VeloxMapper;\` yapmak yeterlidir.`,
    "",
    `- Kaynak kod: ${GITHUB_URL}`,
    `- NuGet: ${NUGET_URL}`,
    `- Tüm dokümantasyon tek dosyada: ${SITE_URL}/llms-full.txt`,
    "",
  ];
  for (const section of getNavigation()) {
    lines.push(`## ${section.title}`, "");
    for (const item of section.items) {
      const doc = getDoc(item.slug)!;
      const desc = doc.description ? `: ${doc.description}` : "";
      lines.push(`- [${doc.title}](${SITE_URL}/docs-md/${doc.slug}.md)${desc}`);
    }
    lines.push("");
  }
  return lines.join("\n");
}

/** Tüm dokümantasyonun tek dosyada birleştirilmiş hali. */
export function buildLlmsFullTxt(): string {
  const parts: string[] = [
    "# VeloxMapper — Tam Dokümantasyon",
    "",
    `> ${SITE_DESCRIPTION}`,
    "",
    `Sürüm: ${APP_VERSION} · Kaynak: ${SITE_URL}/docs`,
    "",
  ];
  for (const doc of getAllDocs()) {
    parts.push("---", "", `<!-- Kaynak: ${SITE_URL}/docs/${doc.slug} -->`, "", toRawMarkdown(doc).trim(), "");
  }
  return parts.join("\n");
}

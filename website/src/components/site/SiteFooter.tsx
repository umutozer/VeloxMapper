import Link from "next/link";
import { LogoMark } from "./Logo";
import { GITHUB_URL, ISSUES_URL, LICENSE_URL, NUGET_URL } from "@/lib/site";
import styles from "./SiteFooter.module.css";

type FooterLink = { label: string; href: string; external?: boolean };

const COLUMNS: { title: string; links: FooterLink[] }[] = [
  {
    title: "Başlarken",
    links: [
      { label: "Genel Bakış", href: "/docs/introduction" },
      { label: "Kurulum", href: "/docs/installation" },
      { label: "Hızlı Başlangıç", href: "/docs/quickstart" },
      { label: "Dependency Injection", href: "/docs/dependency-injection" },
    ],
  },
  {
    title: "Geçiş",
    links: [
      { label: "Geçiş Rehberi", href: "/docs/migration-guide" },
      { label: "Otomatik Geçiş Betiği", href: "/docs/migration-script" },
      { label: "API Eşleme Tablosu", href: "/docs/api-mapping" },
      { label: "Davranış Farkları", href: "/docs/behavior-differences" },
    ],
  },
  {
    title: "Kaynaklar",
    links: [
      { label: "API Referansı", href: "/docs/api/imapper" },
      { label: "SSS", href: "/docs/faq" },
      { label: "Sürüm Notları", href: "/docs/changelog" },
      { label: "llms.txt", href: "/llms.txt", external: true },
    ],
  },
  {
    title: "Proje",
    links: [
      { label: "GitHub", href: GITHUB_URL, external: true },
      { label: "NuGet", href: NUGET_URL, external: true },
      { label: "Sorun Bildir", href: ISSUES_URL, external: true },
      { label: "MIT Lisansı", href: LICENSE_URL, external: true },
    ],
  },
];

export default function SiteFooter() {
  return (
    <footer className={styles.footer}>
      <div className={styles.inner}>
        <div className={styles.top}>
          <div className={styles.brand}>
            <Link href="/" className={styles.brandRow} aria-label="VeloxMapper ana sayfa">
              <LogoMark size={26} />
              <span className={styles.brandName}>VeloxMapper</span>
            </Link>
            <p className={styles.brandDesc}>
              AutoMapper ile aynı API&apos;yi konuşan, MIT lisanslı .NET nesne eşleme kütüphanesi.
            </p>
            <p className={styles.platforms}>.NET 8 · .NET 9 · .NET 10</p>
          </div>

          {COLUMNS.map((col) => (
            <div key={col.title} className={styles.col}>
              <h2 className={styles.colTitle}>{col.title}</h2>
              <ul className={styles.list}>
                {col.links.map((l) => (
                  <li key={l.label}>
                    {l.external ? (
                      <a
                        href={l.href}
                        className={styles.link}
                        {...(l.href.startsWith("http") ? { target: "_blank", rel: "noopener noreferrer" } : {})}
                      >
                        {l.label}
                      </a>
                    ) : (
                      <Link href={l.href} className={styles.link}>
                        {l.label}
                      </Link>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        <div className={styles.bottom}>
          <span>© 2026 Umut Özer</span>
          <span>
            <a href={LICENSE_URL} target="_blank" rel="noopener noreferrer" className={styles.link}>
              MIT Lisansı
            </a>{" "}
            ile dağıtılır · Ticari kullanım serbesttir.
          </span>
        </div>
      </div>
    </footer>
  );
}

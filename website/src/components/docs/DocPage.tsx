import Link from "next/link";
import { ArrowLeft, ArrowRight, ChevronRight, Pencil, MessageSquareWarning } from "lucide-react";
import type { Doc } from "@/lib/docs/source";
import { getPrevNext, toRawMarkdown } from "@/lib/docs/source";
import { processMarkdown } from "@/lib/docs/markdown";
import { renderHast } from "@/lib/docs/render";
import { DOCS_EDIT_BASE, ISSUES_URL } from "@/lib/site";
import Toc from "./Toc";
import PageActions from "./PageActions";
import styles from "./DocPage.module.css";

export default async function DocPage({ doc }: { doc: Doc }) {
  const { tree, toc } = await processMarkdown(doc.body, doc.slug);
  const { prev, next } = getPrevNext(doc.slug);

  return (
    <div className={styles.grid}>
      <article className={styles.article} id="main-content" tabIndex={-1}>
        <nav className={styles.breadcrumb} aria-label="Sayfa konumu">
          <ol>
            <li>
              <Link href="/docs/introduction">Dokümantasyon</Link>
            </li>
            <li aria-hidden="true">
              <ChevronRight size={13} />
            </li>
            <li>{doc.sectionTitle}</li>
            <li aria-hidden="true">
              <ChevronRight size={13} />
            </li>
            <li aria-current="page">{doc.title}</li>
          </ol>
        </nav>

        <header className={styles.header}>
          <h1 className={styles.title}>{doc.title}</h1>
          {doc.description && <p className={styles.lead}>{doc.description}</p>}
          <PageActions markdown={toRawMarkdown(doc)} mdUrl={`/docs-md/${doc.slug}.md`} />
        </header>

        {toc.length > 0 && (
          <details className={styles.inlineToc}>
            <summary>Bu sayfada</summary>
            <ul>
              {toc.map((t) => (
                <li key={t.id} data-depth={t.depth}>
                  <a href={`#${t.id}`}>{t.text}</a>
                </li>
              ))}
            </ul>
          </details>
        )}

        <div className="prose">{renderHast(tree)}</div>

        <footer className={styles.footer}>
          <div className={styles.meta}>
            <a href={`${DOCS_EDIT_BASE}/${doc.filePath}`} target="_blank" rel="noopener noreferrer" className={styles.metaLink}>
              <Pencil size={14} aria-hidden="true" />
              GitHub&apos;da düzenle
            </a>
            <a
              href={`${ISSUES_URL}/new?title=${encodeURIComponent(`Dokümantasyon: ${doc.title}`)}`}
              target="_blank"
              rel="noopener noreferrer"
              className={styles.metaLink}
            >
              <MessageSquareWarning size={14} aria-hidden="true" />
              Sorun bildir
            </a>
          </div>

          {(prev || next) && (
            <nav className={styles.pager} aria-label="Önceki ve sonraki sayfa">
              {prev ? (
                <Link href={`/docs/${prev.slug}`} className={styles.pagerCard} rel="prev">
                  <span className={styles.pagerLabel}>
                    <ArrowLeft size={14} aria-hidden="true" /> Önceki
                  </span>
                  <span className={styles.pagerTitle}>{prev.title}</span>
                  <span className={styles.pagerSection}>{prev.sectionTitle}</span>
                </Link>
              ) : (
                <span />
              )}
              {next && (
                <Link href={`/docs/${next.slug}`} className={`${styles.pagerCard} ${styles.pagerNext}`} rel="next">
                  <span className={styles.pagerLabel}>
                    Sonraki <ArrowRight size={14} aria-hidden="true" />
                  </span>
                  <span className={styles.pagerTitle}>{next.title}</span>
                  <span className={styles.pagerSection}>{next.sectionTitle}</span>
                </Link>
              )}
            </nav>
          )}

          <p className={styles.copyright}>© 2026 Umut Özer · MIT Lisansı</p>
        </footer>
      </article>

      <aside className={styles.rail} aria-label="İçindekiler">
        <div className={styles.railInner}>
          <Toc items={toc} />
        </div>
      </aside>
    </div>
  );
}

import type { ReactNode } from "react";
import { BookText, ExternalLink } from "lucide-react";
import SiteHeader from "@/components/site/SiteHeader";
import DocsNav from "@/components/docs/DocsNav";
import { GitHubIcon } from "@/components/site/BrandIcons";
import { getNavigation } from "@/lib/docs/source";
import { GITHUB_URL } from "@/lib/site";
import "@/styles/prose.css";
import styles from "./layout.module.css";

export default function DocsLayout({ children }: { children: ReactNode }) {
  const nav = getNavigation();
  return (
    <>
      <SiteHeader variant="docs" nav={nav} />
      <div className={styles.shell}>
        <aside className={styles.sidebar} aria-label="Dokümantasyon gezinmesi">
          <div className={styles.sidebarInner}>
            <DocsNav nav={nav} />
            <div className={styles.sidebarFooter}>
              <a href="/llms.txt" className={styles.sidebarLink} target="_blank" rel="noopener">
                <BookText size={14} aria-hidden="true" />
                llms.txt
                <ExternalLink size={12} aria-hidden="true" className={styles.ext} />
              </a>
              <a href={GITHUB_URL} className={styles.sidebarLink} target="_blank" rel="noopener noreferrer">
                <GitHubIcon size={14} />
                GitHub
                <ExternalLink size={12} aria-hidden="true" className={styles.ext} />
              </a>
            </div>
          </div>
        </aside>
        <main className={styles.main}>{children}</main>
      </div>
    </>
  );
}

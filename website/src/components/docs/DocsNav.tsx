"use client";

import { useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ChevronDown } from "lucide-react";
import type { NavSection } from "@/lib/docs/types";
import styles from "./DocsNav.module.css";

function normalize(path: string | null): string {
  if (!path) return "";
  const p = path.replace(/\/$/, "");
  return p === "/docs" ? "/docs/introduction" : p;
}

/** Dokümantasyon kenar çubuğu: _meta.json sırasıyla katlanabilir bölümler. */
export default function DocsNav({ nav, onNavigate }: { nav: NavSection[]; onNavigate?: () => void }) {
  const pathname = normalize(usePathname());
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});

  return (
    <nav className={styles.nav} aria-label="Dokümantasyon">
      {nav.map((section) => {
        const containsActive = section.items.some((i) => `/docs/${i.slug}` === pathname);
        const isCollapsed = !containsActive && collapsed[section.id];
        const listId = `nav-${section.id}`;
        return (
          <div key={section.id} className={styles.section}>
            <button
              type="button"
              className={styles.sectionTitle}
              aria-expanded={!isCollapsed}
              aria-controls={listId}
              onClick={() => setCollapsed((c) => ({ ...c, [section.id]: !isCollapsed }))}
            >
              <span>{section.title}</span>
              <ChevronDown size={14} aria-hidden="true" className={styles.chevron} data-collapsed={isCollapsed || undefined} />
            </button>
            <ul id={listId} className={styles.list} hidden={isCollapsed}>
              {section.items.map((item) => {
                const href = `/docs/${item.slug}`;
                const active = href === pathname;
                return (
                  <li key={item.slug}>
                    <Link
                      href={href}
                      className={styles.link}
                      aria-current={active ? "page" : undefined}
                      onClick={onNavigate}
                    >
                      <span className={styles.linkText}>{item.title}</span>
                      {item.badge && <span className={styles.badge}>{item.badge}</span>}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </div>
        );
      })}
    </nav>
  );
}

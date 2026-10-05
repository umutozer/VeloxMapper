"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ExternalLink, Menu, Search, X } from "lucide-react";
import Logo from "./Logo";
import ThemeToggle from "./ThemeToggle";
import SearchDialog from "./SearchDialog";
import { GitHubIcon, NuGetIcon } from "./BrandIcons";
import DocsNav from "@/components/docs/DocsNav";
import type { NavSection } from "@/lib/docs/types";
import { GITHUB_URL, NUGET_URL } from "@/lib/site";
import styles from "./SiteHeader.module.css";

const LINKS = [
  { label: "Dokümantasyon", href: "/docs/introduction", match: (p: string) => p.startsWith("/docs") && !p.startsWith("/docs/api/") && !p.startsWith("/docs/migration") },
  { label: "Geçiş Rehberi", href: "/docs/migration-guide", match: (p: string) => p.startsWith("/docs/migration") },
  { label: "API Referansı", href: "/docs/api/imapper", match: (p: string) => p.startsWith("/docs/api/") },
];

function subscribeNoop() {
  return () => {};
}

function isApple() {
  return /Mac|iPhone|iPad|iPod/i.test(navigator.platform || navigator.userAgent);
}

interface SiteHeaderProps {
  variant?: "marketing" | "docs";
  nav?: NavSection[];
}

export default function SiteHeader({ variant = "marketing", nav }: SiteHeaderProps) {
  const pathname = usePathname() ?? "/";
  const [searchOpen, setSearchOpen] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const drawerRef = useRef<HTMLDialogElement>(null);
  const apple = useSyncExternalStore(subscribeNoop, isApple, () => false);

  // ⌘K / Ctrl+K ve "/" kısayolları
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setSearchOpen((o) => !o);
        return;
      }
      if (e.key === "/" && !e.metaKey && !e.ctrlKey && !e.altKey) {
        const t = e.target as HTMLElement | null;
        if (t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName))) return;
        e.preventDefault();
        setSearchOpen(true);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  useEffect(() => {
    const d = drawerRef.current;
    if (!d) return;
    if (menuOpen && !d.open) d.showModal();
    else if (!menuOpen && d.open) d.close();
  }, [menuOpen]);

  const closeMenu = () => setMenuOpen(false);

  return (
    <>
      <header className={styles.header} data-variant={variant}>
        <div className={styles.inner}>
          <button
            type="button"
            className={`${styles.iconBtn} ${styles.menuBtn}`}
            onClick={() => setMenuOpen(true)}
            aria-label="Menüyü aç"
            aria-expanded={menuOpen}
          >
            <Menu size={19} aria-hidden="true" />
          </button>

          <Logo suffix={variant === "docs" ? "Docs" : undefined} />

          <nav className={styles.links} aria-label="Ana menü">
            {LINKS.map((l) => (
              <Link key={l.href} href={l.href} className={styles.link} aria-current={l.match(pathname) ? "page" : undefined}>
                {l.label}
              </Link>
            ))}
          </nav>

          <div className={styles.actions}>
            <button type="button" className={styles.searchBtn} onClick={() => setSearchOpen(true)} aria-label="Dokümantasyonda ara">
              <Search size={15} aria-hidden="true" />
              <span className={styles.searchLabel}>Ara…</span>
              <span className={styles.searchKbd} aria-hidden="true">
                <kbd>{apple ? "⌘" : "Ctrl"}</kbd>
                <kbd>K</kbd>
              </span>
            </button>
            <a className={`${styles.textLink} ${styles.hideMd}`} href={NUGET_URL} target="_blank" rel="noopener noreferrer">
              <NuGetIcon size={15} />
              <span>NuGet</span>
            </a>
            <a className={`${styles.iconBtn} ${styles.hideSm}`} href={GITHUB_URL} target="_blank" rel="noopener noreferrer" aria-label="GitHub deposu">
              <GitHubIcon size={17} />
            </a>
            <ThemeToggle className={styles.iconBtn} />
          </div>
        </div>
      </header>

      <SearchDialog open={searchOpen} onClose={() => setSearchOpen(false)} />

      <dialog
        ref={drawerRef}
        className={styles.drawer}
        aria-label="Menü"
        onClose={closeMenu}
        onClick={(e) => {
          if (e.target === drawerRef.current) closeMenu();
        }}
      >
        <div className={styles.drawerPanel}>
          <div className={styles.drawerHead}>
            <Logo showVersion />
            <button type="button" className={styles.iconBtn} onClick={closeMenu} aria-label="Menüyü kapat">
              <X size={19} aria-hidden="true" />
            </button>
          </div>
          <div className={styles.drawerBody}>
            <nav className={styles.drawerLinks} aria-label="Ana menü (mobil)">
              {LINKS.map((l) => (
                <Link key={l.href} href={l.href} className={styles.drawerLink} aria-current={l.match(pathname) ? "page" : undefined} onClick={closeMenu}>
                  {l.label}
                </Link>
              ))}
              <a className={styles.drawerLink} href={GITHUB_URL} target="_blank" rel="noopener noreferrer">
                GitHub <ExternalLink size={13} aria-hidden="true" />
              </a>
              <a className={styles.drawerLink} href={NUGET_URL} target="_blank" rel="noopener noreferrer">
                NuGet <ExternalLink size={13} aria-hidden="true" />
              </a>
            </nav>
            {nav && nav.length > 0 && (
              <div className={styles.drawerNav}>
                <DocsNav nav={nav} onNavigate={closeMenu} />
              </div>
            )}
          </div>
        </div>
      </dialog>
    </>
  );
}

"use client";

import { useEffect, useState } from "react";
import type { TocItem } from "@/lib/docs/types";
import styles from "./Toc.module.css";

const OFFSET = 110; // sabit başlık yüksekliği + pay

/** "Bu sayfada" içindekiler listesi; kaydırma konumuna göre etkin başlığı vurgular. */
export default function Toc({ items }: { items: TocItem[] }) {
  const [activeId, setActiveId] = useState<string | null>(null);

  useEffect(() => {
    if (items.length === 0) return;
    let frame = 0;
    const update = () => {
      frame = 0;
      let current: string | null = items[0].id;
      for (const item of items) {
        const el = document.getElementById(item.id);
        if (!el) continue;
        if (el.getBoundingClientRect().top - OFFSET <= 0) current = item.id;
        else break;
      }
      // Sayfanın sonuna gelindiyse son başlığı etkinleştir.
      if (window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 4) {
        current = items[items.length - 1].id;
      }
      setActiveId(current);
    };
    const onScroll = () => {
      if (!frame) frame = requestAnimationFrame(update);
    };
    frame = requestAnimationFrame(update);
    window.addEventListener("scroll", onScroll, { passive: true });
    window.addEventListener("resize", onScroll);
    return () => {
      if (frame) cancelAnimationFrame(frame);
      window.removeEventListener("scroll", onScroll);
      window.removeEventListener("resize", onScroll);
    };
  }, [items]);

  if (items.length === 0) return null;

  return (
    <nav className={styles.toc} aria-labelledby="toc-title">
      <p id="toc-title" className={styles.title}>
        Bu sayfada
      </p>
      <ul className={styles.list}>
        {items.map((item) => (
          <li key={item.id} data-depth={item.depth}>
            <a
              href={`#${item.id}`}
              className={styles.link}
              aria-current={activeId === item.id ? "location" : undefined}
            >
              {item.text}
            </a>
          </li>
        ))}
      </ul>
    </nav>
  );
}

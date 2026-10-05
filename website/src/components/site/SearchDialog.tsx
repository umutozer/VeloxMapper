"use client";

import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from "react";
import { useRouter } from "next/navigation";
import type MiniSearch from "minisearch";
import { CornerDownLeft, FileText, Hash, Search, X } from "lucide-react";
import styles from "./SearchDialog.module.css";

interface SearchRecord {
  id: string;
  url: string;
  page: string;
  section: string;
  heading: string;
  text: string;
}

interface Result extends SearchRecord {
  score: number;
  terms: string[];
}

const QUICK_LINKS = [
  { url: "/docs/introduction", page: "Genel Bakış", section: "Başlarken" },
  { url: "/docs/quickstart", page: "Hızlı Başlangıç", section: "Başlarken" },
  { url: "/docs/migration-guide", page: "Geçiş Rehberi", section: "AutoMapper'dan Geçiş" },
  { url: "/docs/api-mapping", page: "API Eşleme Tablosu", section: "AutoMapper'dan Geçiş" },
  { url: "/docs/api/imapper", page: "IMapper", section: "API Referansı" },
];

/** Türkçe karakterleri ve büyük/küçük harfi yok sayan terim normalizasyonu. */
export function normalizeTerm(term: string): string {
  return term
    .toLocaleLowerCase("tr")
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/ı/g, "i");
}

let indexPromise: Promise<MiniSearch<SearchRecord>> | null = null;

function loadIndex(): Promise<MiniSearch<SearchRecord>> {
  if (!indexPromise) {
    indexPromise = (async () => {
      const [{ default: MiniSearchCtor }, res] = await Promise.all([import("minisearch"), fetch("/search-index.json")]);
      if (!res.ok) throw new Error(`Arama dizini yüklenemedi (${res.status})`);
      const docs = (await res.json()) as SearchRecord[];
      const ms = new MiniSearchCtor<SearchRecord>({
        fields: ["page", "heading", "text"],
        storeFields: ["url", "page", "section", "heading", "text"],
        processTerm: (t) => normalizeTerm(t) || null,
        searchOptions: {
          boost: { page: 3, heading: 2 },
          prefix: true,
          fuzzy: (term) => (term.length > 4 ? 0.2 : false),
        },
      });
      ms.addAll(docs);
      return ms;
    })();
    indexPromise.catch(() => {
      indexPromise = null;
    });
  }
  return indexPromise;
}

/** Metni karakter karakter normalize eder (uzunluk korunur), eşleşme konumlarını bulmak için. */
function foldChars(text: string): string {
  let out = "";
  for (let i = 0; i < text.length; i++) out += normalizeTerm(text[i])[0] ?? text[i];
  return out;
}

/** Eşleşen ilk terimin çevresinden kısa bir özet çıkarır ve terimleri (Türkçe karakter duyarsız) vurgular. */
function Snippet({ text, query }: { text: string; query: string }) {
  if (!text) return null;
  const words = query
    .split(/s+/)
    .map((w) => normalizeTerm(w.trim()))
    .filter((w) => w.length > 1);
  const folded = foldChars(text);
  let pos = -1;
  for (const w of words) {
    const i = folded.indexOf(w);
    if (i >= 0 && (pos < 0 || i < pos)) pos = i;
  }
  const start = Math.max(0, pos - 50);
  const end = Math.min(text.length, start + 160);
  const excerpt = text.slice(start, end);
  const foldedExcerpt = folded.slice(start, end);
  // Vurgulanacak aralıkları bul
  const marks: [number, number][] = [];
  for (const w of words) {
    let i = foldedExcerpt.indexOf(w);
    while (i >= 0) {
      marks.push([i, i + w.length]);
      i = foldedExcerpt.indexOf(w, i + w.length);
    }
  }
  marks.sort((a, b) => a[0] - b[0]);
  const parts: { text: string; hit: boolean }[] = [];
  let cursor = 0;
  for (const [a, b] of marks) {
    if (a < cursor) continue;
    if (a > cursor) parts.push({ text: excerpt.slice(cursor, a), hit: false });
    parts.push({ text: excerpt.slice(a, b), hit: true });
    cursor = b;
  }
  if (cursor < excerpt.length) parts.push({ text: excerpt.slice(cursor), hit: false });
  return (
    <span>
      {start > 0 && "…"}
      {parts.map((p, i) => (p.hit ? <mark key={i}>{p.text}</mark> : <span key={i}>{p.text}</span>))}
      {end < text.length && "…"}
    </span>
  );
}

export default function SearchDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const router = useRouter();
  const dialogRef = useRef<HTMLDialogElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<Result[]>([]);
  const [active, setActive] = useState(0);
  const [status, setStatus] = useState<"idle" | "loading" | "ready" | "error">("idle");

  // Dialog'u aç/kapat (native <dialog>: odak hapsi ve Esc tarayıcıdan gelir).
  useEffect(() => {
    const d = dialogRef.current;
    if (!d) return;
    if (open && !d.open) {
      d.showModal();
      requestAnimationFrame(() => inputRef.current?.focus());
      loadIndex().then(
        () => setStatus("ready"),
        () => setStatus("error")
      );
    } else if (!open && d.open) {
      d.close();
    }
  }, [open]);

  const runSearch = useCallback(async (q: string) => {
    const trimmed = q.trim();
    if (!trimmed) {
      setResults([]);
      return;
    }
    setStatus((s) => (s === "ready" ? s : "loading"));
    try {
      const ms = await loadIndex();
      let hits = ms.search(trimmed, { combineWith: "AND" });
      if (hits.length === 0) hits = ms.search(trimmed, { combineWith: "OR" });
      setResults(
        hits.slice(0, 12).map((h) => ({
          id: String(h.id),
          url: h.url,
          page: h.page,
          section: h.section,
          heading: h.heading,
          text: h.text,
          score: h.score,
          terms: h.terms,
        }))
      );
      setActive(0);
      setStatus("ready");
    } catch {
      setStatus("error");
    }
  }, []);

  const items = useMemo(
    () =>
      query.trim()
        ? results
        : QUICK_LINKS.map((l) => ({ ...l, id: l.url, heading: "", text: "", score: 0, terms: [] as string[] })),
    [query, results]
  );

  const go = (url: string) => {
    onClose();
    setQuery("");
    setResults([]);
    router.push(url);
  };

  const onKeyDown = (e: ReactKeyboardEvent) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActive((a) => Math.min(a + 1, items.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActive((a) => Math.max(a - 1, 0));
    } else if (e.key === "Enter") {
      const item = items[active];
      if (item) {
        e.preventDefault();
        go(item.url);
      }
    }
  };

  useEffect(() => {
    listRef.current?.querySelector<HTMLElement>(`[data-index="${active}"]`)?.scrollIntoView({ block: "nearest" });
  }, [active]);

  const activeId = items[active] ? `search-opt-${active}` : undefined;

  return (
    <dialog
      ref={dialogRef}
      className={styles.dialog}
      aria-label="Dokümantasyonda ara"
      onClose={onClose}
      onClick={(e) => {
        if (e.target === dialogRef.current) onClose();
      }}
    >
      <div className={styles.panel} onKeyDown={onKeyDown}>
        <div className={styles.inputRow}>
          <Search size={18} aria-hidden="true" className={styles.inputIcon} />
          <input
            ref={inputRef}
            className={styles.input}
            type="search"
            placeholder="Dokümantasyonda ara…"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              runSearch(e.target.value);
            }}
            role="combobox"
            aria-expanded={items.length > 0}
            aria-controls="search-results"
            aria-activedescendant={activeId}
            aria-autocomplete="list"
            autoComplete="off"
            spellCheck={false}
          />
          <button type="button" className={styles.close} onClick={onClose} aria-label="Aramayı kapat">
            <X size={16} aria-hidden="true" />
          </button>
        </div>

        <div className={styles.body}>
          {!query.trim() && <p className={styles.groupLabel}>Öne çıkan sayfalar</p>}
          {query.trim() && status === "error" && (
            <p className={styles.empty}>Arama dizini yüklenemedi. Sayfayı yenileyip tekrar deneyin.</p>
          )}
          {query.trim() && status !== "error" && results.length === 0 && (
            <p className={styles.empty}>
              {status === "loading" ? "Aranıyor…" : <>“{query}” için sonuç bulunamadı.</>}
            </p>
          )}
          {items.length > 0 && (
            <ul ref={listRef} id="search-results" role="listbox" className={styles.list} aria-label="Arama sonuçları">
              {items.map((r, i) => (
                <li
                  key={r.id}
                  id={`search-opt-${i}`}
                  role="option"
                  aria-selected={i === active}
                  data-index={i}
                  className={styles.item}
                  onMouseMove={() => setActive(i)}
                  onClick={() => go(r.url)}
                >
                  <span className={styles.itemIcon} aria-hidden="true">
                    {r.heading ? <Hash size={15} /> : <FileText size={15} />}
                  </span>
                  <span className={styles.itemMain}>
                    <span className={styles.itemTitle}>
                      {r.page}
                      {r.heading && <span className={styles.itemHeading}> › {r.heading}</span>}
                    </span>
                    {query.trim() && r.text ? (
                      <span className={styles.itemSnippet}>
                        <Snippet text={r.text} query={query} />
                      </span>
                    ) : (
                      <span className={styles.itemSection}>{r.section}</span>
                    )}
                  </span>
                  <CornerDownLeft size={14} aria-hidden="true" className={styles.itemEnter} />
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className={styles.footer} aria-hidden="true">
          <span>
            <kbd>↑</kbd>
            <kbd>↓</kbd> gezin
          </span>
          <span>
            <kbd>↵</kbd> aç
          </span>
          <span>
            <kbd>Esc</kbd> kapat
          </span>
        </div>
      </div>
    </dialog>
  );
}

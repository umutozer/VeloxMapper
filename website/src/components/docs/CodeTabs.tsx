"use client";

import { Children, isValidElement, useId, useState, useSyncExternalStore, type KeyboardEvent, type ReactNode } from "react";

const STORAGE_KEY = "velox:code-tab";
const EVENT = "velox:code-tab-change";

function subscribe(callback: () => void) {
  window.addEventListener(EVENT, callback);
  window.addEventListener("storage", callback);
  return () => {
    window.removeEventListener(EVENT, callback);
    window.removeEventListener("storage", callback);
  };
}

function readPreference(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function writePreference(value: string) {
  try {
    localStorage.setItem(STORAGE_KEY, value);
  } catch {
    /* depolama yoksa yalnızca bu oturumda geçerli */
  }
  window.dispatchEvent(new Event(EVENT));
}

/**
 * Kod sekmeleri. Aynı etikete sahip sekmeler (ör. "VeloxMapper") sayfadaki tüm
 * sekme gruplarında senkronize edilir ve tercih tarayıcıda hatırlanır.
 */
export default function CodeTabs({ tabs, children }: { tabs: string[]; children?: ReactNode }) {
  const baseId = useId();
  const panels = Children.toArray(children).filter(isValidElement);
  const preferred = useSyncExternalStore(subscribe, readPreference, () => null);
  const [localIndex, setLocalIndex] = useState(0);
  const preferredIndex = preferred ? tabs.indexOf(preferred) : -1;
  const active = preferredIndex >= 0 ? preferredIndex : Math.min(localIndex, panels.length - 1);

  const select = (i: number) => {
    setLocalIndex(i);
    if (tabs[i]) writePreference(tabs[i]);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLButtonElement>) => {
    const count = panels.length;
    let next = -1;
    if (e.key === "ArrowRight") next = (active + 1) % count;
    else if (e.key === "ArrowLeft") next = (active - 1 + count) % count;
    else if (e.key === "Home") next = 0;
    else if (e.key === "End") next = count - 1;
    if (next < 0) return;
    e.preventDefault();
    select(next);
    document.getElementById(`${baseId}-tab-${next}`)?.focus();
  };

  return (
    <div className="code-tabs">
      <div className="code-tabs__list" role="tablist" aria-label="Kod örnekleri">
        {panels.map((_, i) => (
          <button
            key={i}
            id={`${baseId}-tab-${i}`}
            type="button"
            role="tab"
            className="code-tabs__tab"
            aria-selected={i === active}
            aria-controls={`${baseId}-panel-${i}`}
            tabIndex={i === active ? 0 : -1}
            onClick={() => select(i)}
            onKeyDown={onKeyDown}
          >
            {tabs[i] ?? `Sekme ${i + 1}`}
          </button>
        ))}
      </div>
      {panels.map((panel, i) => (
        <div
          key={i}
          id={`${baseId}-panel-${i}`}
          role="tabpanel"
          aria-labelledby={`${baseId}-tab-${i}`}
          hidden={i !== active}
          className="code-tabs__panel"
        >
          {panel}
        </div>
      ))}
    </div>
  );
}

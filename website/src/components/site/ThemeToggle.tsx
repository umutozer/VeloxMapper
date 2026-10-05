"use client";

import { useEffect, useSyncExternalStore } from "react";
import { Monitor, Moon, Sun } from "lucide-react";

type Pref = "light" | "dark" | "system";
const STORAGE_KEY = "velox-theme";
const EVENT = "velox:theme-change";
const ORDER: Pref[] = ["system", "light", "dark"];
const LABELS: Record<Pref, string> = { system: "Sistem", light: "Açık", dark: "Koyu" };

function readPref(): Pref {
  const p = document.documentElement.getAttribute("data-theme-pref");
  return p === "light" || p === "dark" ? p : "system";
}

function subscribe(cb: () => void) {
  window.addEventListener(EVENT, cb);
  return () => window.removeEventListener(EVENT, cb);
}

function apply(pref: Pref) {
  const dark = pref === "dark" || (pref === "system" && window.matchMedia("(prefers-color-scheme: dark)").matches);
  const root = document.documentElement;
  root.setAttribute("data-theme", dark ? "dark" : "light");
  root.setAttribute("data-theme-pref", pref);
}

export default function ThemeToggle({ className }: { className?: string }) {
  const pref = useSyncExternalStore<Pref>(subscribe, readPref, () => "system");

  // Sistem teması değişirse (ör. işletim sistemi gece moduna geçerse) takip et.
  useEffect(() => {
    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = () => {
      if (readPref() === "system") apply("system");
    };
    mq.addEventListener("change", onChange);
    return () => mq.removeEventListener("change", onChange);
  }, []);

  const cycle = () => {
    const next = ORDER[(ORDER.indexOf(pref) + 1) % ORDER.length];
    try {
      if (next === "system") localStorage.removeItem(STORAGE_KEY);
      else localStorage.setItem(STORAGE_KEY, next);
    } catch {
      /* depolama kapalıysa yalnızca bu sayfada uygula */
    }
    apply(next);
    window.dispatchEvent(new Event(EVENT));
  };

  const Icon = pref === "light" ? Sun : pref === "dark" ? Moon : Monitor;
  const label = `Tema: ${LABELS[pref]} (değiştirmek için tıklayın)`;

  return (
    <button type="button" className={className} onClick={cycle} aria-label={label} title={label}>
      <Icon size={17} aria-hidden="true" />
    </button>
  );
}

"use client";

import { useRef, type ReactNode } from "react";
import CopyButton from "@/components/CopyButton";

interface CodeBlockProps {
  lang: string;
  label: string;
  title?: string;
  inTabs?: boolean;
  children?: ReactNode;
}

/** Renklendirilmiş kod bloğu: başlık çubuğu + kopyalama düğmesi. */
export default function CodeBlock({ lang, label, title, inTabs, children }: CodeBlockProps) {
  const ref = useRef<HTMLDivElement>(null);
  const getText = () => ref.current?.querySelector("pre")?.textContent ?? "";

  return (
    <div className="code-block" data-lang={lang} data-in-tabs={inTabs || undefined}>
      {!inTabs && (
        <div className="code-block__header">
          <span className="code-block__title">{title || label}</span>
          {title && label && <span className="code-block__lang">{label}</span>}
          <CopyButton text={getText} label="Kodu kopyala" className="copy-btn code-block__copy" />
        </div>
      )}
      <div className="code-block__body" ref={ref}>
        {children}
        {inTabs && (
          <CopyButton text={getText} label="Kodu kopyala" className="copy-btn code-block__copy code-block__copy--floating" />
        )}
      </div>
    </div>
  );
}

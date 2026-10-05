"use client";

import { FileText } from "lucide-react";
import CopyButton from "@/components/CopyButton";
import styles from "./PageActions.module.css";

/** "Bu sayfayı kopyala" + "Markdown olarak görüntüle" eylemleri. */
export default function PageActions({ markdown, mdUrl }: { markdown: string; mdUrl: string }) {
  return (
    <div className={styles.actions}>
      <CopyButton
        text={markdown}
        label="Bu sayfayı kopyala"
        copiedLabel="Kopyalandı"
        iconOnly={false}
        className={styles.action}
      />
      <a className={styles.action} href={mdUrl} target="_blank" rel="noopener">
        <FileText size={15} aria-hidden="true" />
        <span>Markdown olarak görüntüle</span>
      </a>
    </div>
  );
}

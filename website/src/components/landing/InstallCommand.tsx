"use client";

import { useState } from "react";
import { Check, Copy } from "lucide-react";
import { copyText } from "@/components/CopyButton";
import { INSTALL_COMMAND } from "@/lib/site";
import styles from "./InstallCommand.module.css";

export default function InstallCommand() {
  const [copied, setCopied] = useState(false);
  const onClick = async () => {
    if (await copyText(INSTALL_COMMAND)) {
      setCopied(true);
      setTimeout(() => setCopied(false), 1800);
    }
  };
  return (
    <button type="button" className={styles.box} onClick={onClick} aria-label={`Kurulum komutunu kopyala: ${INSTALL_COMMAND}`}>
      <span className={styles.prompt} aria-hidden="true">
        $
      </span>
      <code className={styles.cmd}>{INSTALL_COMMAND}</code>
      <span className={styles.icon} data-copied={copied || undefined} aria-hidden="true">
        {copied ? <Check size={15} /> : <Copy size={15} />}
      </span>
      <span className="sr-only" aria-live="polite">
        {copied ? "Kopyalandı" : ""}
      </span>
    </button>
  );
}

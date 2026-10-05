import Link from "next/link";
import { APP_VERSION } from "@/lib/version";
import styles from "./Logo.module.css";

export function LogoMark({ size = 28 }: { size?: number }) {
  return (
    <span className={styles.mark} style={{ width: size, height: size }} aria-hidden="true">
      <svg viewBox="0 0 24 24" width={size * 0.58} height={size * 0.58} fill="currentColor">
        <path d="M13.2 2.4a.6.6 0 0 1 1 .58L12.3 9.6h6.2a.6.6 0 0 1 .46.98l-8.2 10.98a.6.6 0 0 1-1.06-.53l1.9-6.63H5.5a.6.6 0 0 1-.47-.97Z" />
      </svg>
    </span>
  );
}

export default function Logo({ showVersion = true, suffix }: { showVersion?: boolean; suffix?: string }) {
  return (
    <span className={styles.wrap}>
      <Link href="/" className={styles.logo} aria-label="VeloxMapper ana sayfa">
        <LogoMark />
        <span className={styles.text}>VeloxMapper</span>
      </Link>
      {suffix && <span className={styles.suffix}>{suffix}</span>}
      {showVersion && (
        <span className={styles.version} title={`Güncel sürüm ${APP_VERSION}`}>
          v{APP_VERSION}
        </span>
      )}
    </span>
  );
}

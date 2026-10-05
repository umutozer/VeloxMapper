import type { Metadata } from "next";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import SiteHeader from "@/components/site/SiteHeader";
import SiteFooter from "@/components/site/SiteFooter";

export const metadata: Metadata = {
  title: "Sayfa bulunamadı · VeloxMapper",
};

export default function NotFound() {
  return (
    <>
      <SiteHeader />
      <main
        id="main-content"
        style={{
          maxWidth: 640,
          margin: "0 auto",
          padding: "120px 24px 140px",
          display: "flex",
          flexDirection: "column",
          gap: 18,
          alignItems: "flex-start",
        }}
      >
        <p style={{ fontFamily: "var(--font-mono)", color: "var(--accent-text)", fontWeight: 600 }}>404</p>
        <h1 style={{ fontFamily: "var(--font-display)", fontSize: "2.2rem", fontWeight: 800, letterSpacing: "-0.03em", color: "var(--fg-strong)" }}>
          Aradığınız sayfa bulunamadı.
        </h1>
        <p style={{ color: "var(--fg-muted)", fontSize: "1.05rem" }}>
          Sayfa taşınmış ya da kaldırılmış olabilir. Dokümantasyonda arama yapmak için <kbd>Ctrl</kbd> <kbd>K</kbd>{" "}
          kısayolunu kullanabilirsiniz.
        </p>
        <div style={{ display: "flex", gap: 12, flexWrap: "wrap", marginTop: 8 }}>
          <Link href="/docs/introduction" className="btn btn--primary">
            Dokümantasyon <ArrowRight size={16} aria-hidden="true" />
          </Link>
          <Link href="/" className="btn btn--secondary">
            Ana sayfa
          </Link>
        </div>
      </main>
      <SiteFooter />
    </>
  );
}

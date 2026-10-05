import { ImageResponse } from "next/og";

export const dynamic = "force-static";
export const alt = "VeloxMapper — AutoMapper uyumlu, MIT lisanslı .NET nesne eşleme kütüphanesi";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default function OpengraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between",
          padding: "72px 80px",
          background: "linear-gradient(135deg, #0b1220 0%, #111a2e 55%, #16264a 100%)",
          color: "#ffffff",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 20 }}>
          <div
            style={{
              width: 64,
              height: 64,
              borderRadius: 14,
              background: "#1d2b49",
              border: "1px solid rgba(255,255,255,0.14)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
            }}
          >
            <svg width="38" height="38" viewBox="0 0 24 24" fill="#7fb0ff">
              <path d="M13.2 2.4a.6.6 0 0 1 1 .58L12.3 9.6h6.2a.6.6 0 0 1 .46.98l-8.2 10.98a.6.6 0 0 1-1.06-.53l1.9-6.63H5.5a.6.6 0 0 1-.47-.97Z" />
            </svg>
          </div>
          <div style={{ fontSize: 40, fontWeight: 800, letterSpacing: -1 }}>VeloxMapper</div>
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
          <div style={{ fontSize: 76, fontWeight: 800, lineHeight: 1.05, letterSpacing: -2 }}>
            {"AutoMapper'dan tek adımda geçiş."}
          </div>
          <div style={{ fontSize: 32, color: "#a9bbdc", lineHeight: 1.4 }}>
            Aynı API · MIT lisansı · .NET 8 / 9 / 10
          </div>
        </div>
        <div style={{ display: "flex", fontSize: 26, color: "#7fb0ff", fontFamily: "monospace" }}>
          dotnet add package VeloxMapper
        </div>
      </div>
    ),
    size
  );
}

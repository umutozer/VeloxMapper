import type { Metadata, Viewport } from "next";
import { Inter, Montserrat, JetBrains_Mono } from "next/font/google";
import { SITE_DESCRIPTION, SITE_NAME, SITE_URL } from "@/lib/site";
import "./globals.css";

const inter = Inter({
  subsets: ["latin", "latin-ext"],
  variable: "--font-sans",
  display: "swap",
});

const montserrat = Montserrat({
  subsets: ["latin", "latin-ext"],
  variable: "--font-display",
  weight: ["600", "700", "800"],
  display: "swap",
});

const jetbrainsMono = JetBrains_Mono({
  subsets: ["latin", "latin-ext"],
  variable: "--font-mono",
  display: "swap",
});

export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: {
    default: "VeloxMapper — AutoMapper uyumlu, MIT lisanslı .NET nesne eşleme",
    template: "%s",
  },
  description: SITE_DESCRIPTION,
  applicationName: SITE_NAME,
  keywords: [
    "VeloxMapper",
    "AutoMapper alternatifi",
    "AutoMapper geçiş",
    ".NET object mapper",
    "nesne eşleme",
    "C#",
    "source generator",
    "NativeAOT",
    "MIT",
  ],
  authors: [{ name: "Umut Özer" }],
  openGraph: {
    type: "website",
    siteName: SITE_NAME,
    locale: "tr_TR",
    url: SITE_URL,
    title: "VeloxMapper — AutoMapper uyumlu, MIT lisanslı .NET nesne eşleme",
    description: SITE_DESCRIPTION,
  },
  twitter: {
    card: "summary_large_image",
    title: "VeloxMapper — AutoMapper uyumlu, MIT lisanslı .NET nesne eşleme",
    description: SITE_DESCRIPTION,
  },
  alternates: {
    types: { "text/plain": [{ url: "/llms.txt", title: "llms.txt" }] },
  },
};

export const viewport: Viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#ffffff" },
    { media: "(prefers-color-scheme: dark)", color: "#0b0e14" },
  ],
};

/**
 * İlk boyamadan önce tema uygulanır (flash yok). Tercih: localStorage
 * "velox-theme" = light | dark | system (varsayılan: system).
 */
const themeScript = `(function(){try{var p=localStorage.getItem("velox-theme");if(p!=="light"&&p!=="dark")p="system";var d=p==="dark"||(p==="system"&&window.matchMedia("(prefers-color-scheme: dark)").matches);var r=document.documentElement;r.setAttribute("data-theme",d?"dark":"light");r.setAttribute("data-theme-pref",p);}catch(e){}})();`;

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html
      lang="tr"
      className={`${inter.variable} ${montserrat.variable} ${jetbrainsMono.variable}`}
      suppressHydrationWarning
    >
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body>
        <a className="skip-link" href="#main-content">
          İçeriğe geç
        </a>
        {children}
      </body>
    </html>
  );
}

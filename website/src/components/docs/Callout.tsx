import type { ReactNode } from "react";
import { Info, Lightbulb, MessageSquareWarning, OctagonAlert, TriangleAlert } from "lucide-react";

const ICONS = {
  note: Info,
  tip: Lightbulb,
  important: MessageSquareWarning,
  warning: TriangleAlert,
  caution: OctagonAlert,
} as const;

/** GitHub tarzı uyarı kutusu (`> [!NOTE]` vb.). */
export default function Callout({ type, title, children }: { type: string; title: string; children?: ReactNode }) {
  const kind = (type in ICONS ? type : "note") as keyof typeof ICONS;
  const Icon = ICONS[kind];
  return (
    <div className={`callout callout--${kind}`} role="note">
      <p className="callout__title">
        <Icon size={16} aria-hidden="true" />
        <span>{title}</span>
      </p>
      <div className="callout__body">{children}</div>
    </div>
  );
}

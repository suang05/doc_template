import { ReactNode } from "react";
import type { BadgeIntent } from "@/types/api";
import { cn } from "@/utils/cn";

const intentStyle: Record<BadgeIntent, string> = {
  docx:    "bg-[var(--blue2-t)] text-[var(--blue2)]",
  xlsx:    "bg-[var(--emerald-t)] text-[var(--emerald)]",
  pdf:     "bg-[var(--rose2-t)] text-[var(--rose2)]",
  html:    "bg-[var(--amber-t)] text-[var(--amber)]",
  json:    "bg-[var(--violet-t)] text-[var(--violet)]",
  grey:    "bg-[var(--sep)] text-[var(--t2)]",
  blue:    "bg-[var(--blue-t)] text-[var(--blue)]",
  violet:  "bg-[var(--violet-t)] text-[var(--violet)]",
};

interface BadgeProps {
  intent: BadgeIntent;
  icon?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function Badge({ intent, icon, children, className }: BadgeProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-[3px] px-[7px] py-[3px] rounded-pill text-[10.5px] font-bold leading-none",
        intentStyle[intent],
        className
      )}
    >
      {icon}
      {children}
    </span>
  );
}

// ── Format badge ──────────────────────────────────────────────────────────────

const FORMAT_INTENT: Record<string, BadgeIntent> = {
  docx: "docx", xlsx: "xlsx", pdf: "pdf", html: "html", json: "json",
};

const DocxIcon = () => (
  <svg width="9" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
    <path d="M14 2v6h6" /><path d="M9 13h6M9 17h4" />
  </svg>
);

const XlsxIcon = () => (
  <svg width="9" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <rect x="3" y="3" width="18" height="18" rx="2" />
    <path d="M3 9h18M3 15h18M9 3v18" />
  </svg>
);

const PdfIcon = () => (
  <svg width="9" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
    <path d="M14 2v6h6" /><path d="M9 13h2a2 2 0 0 0 0-4H9v8" />
  </svg>
);

const HtmlIcon = () => (
  <svg width="9" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="16 18 22 12 16 6" />
    <polyline points="8 6 2 12 8 18" />
  </svg>
);

const JsonIcon = () => (
  <svg width="9" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <path d="M9 8H5a2 2 0 0 0-2 2v4a2 2 0 0 0 2 2h4" />
    <path d="M15 8h4a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2h-4" />
    <line x1="12" y1="8" x2="12" y2="16" />
  </svg>
);

const FORMAT_ICONS: Record<string, ReactNode> = {
  docx: <DocxIcon />,
  xlsx: <XlsxIcon />,
  pdf:  <PdfIcon />,
  html: <HtmlIcon />,
  json: <JsonIcon />,
};

export function FormatBadge({ format, showIcon = false }: { format: string; showIcon?: boolean }) {
  const key = format.toLowerCase();
  const intent = FORMAT_INTENT[key] ?? "grey";
  const icon = showIcon ? FORMAT_ICONS[key] : undefined;
  return <Badge intent={intent} icon={icon}>{format.toUpperCase()}</Badge>;
}

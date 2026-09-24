import { ReactNode } from "react";
import type { PillIntent, GenerationStatus } from "@/types/api";
import { cn } from "@/utils/cn";

const intentStyle: Record<PillIntent, string> = {
  success:    "bg-[var(--emerald)] text-white",
  failed:     "bg-[var(--rose)] text-white",
  processing: "bg-[var(--amber)] text-white",
  DEV:        "bg-[var(--blue)] text-white",
  ADMIN:      "bg-[var(--rose)] text-white",
  count:      "bg-[var(--blue)] text-white",
};

interface PillProps {
  intent: PillIntent;
  children: ReactNode;
  className?: string;
}

export function Pill({ intent, children, className }: PillProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center px-2 py-0.5 rounded-pill text-[11px] font-bold leading-none",
        intentStyle[intent],
        className
      )}
    >
      {children}
    </span>
  );
}

const STATUS_LABEL: Record<string, string> = {
  success:    "สำเร็จ",
  failed:     "ล้มเหลว",
  processing: "กำลังดำเนินการ",
};

export function StatusPill({ status }: { status: GenerationStatus }) {
  const key = status.toLowerCase() as "success" | "failed";
  return <Pill intent={key}>{STATUS_LABEL[key] ?? status}</Pill>;
}

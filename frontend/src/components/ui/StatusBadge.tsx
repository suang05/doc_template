import { CheckCircle2, XCircle, Clock } from "lucide-react";
import { cn } from "@/utils/cn";

type StatusVariant = "success" | "failed" | "pending" | string;

interface StatusBadgeProps {
  status: StatusVariant;
  /** Override display label — defaults to Thai translation of status */
  label?: string;
  className?: string;
}

const STATUS_MAP: Record<string, {
  icon: React.ReactNode;
  cls: string;
  defaultLabel: string;
}> = {
  success:   { icon: <CheckCircle2 className="w-3 h-3" />, cls: "bg-[var(--emerald-t)] text-[var(--emerald)] border-[var(--emerald)]", defaultLabel: "สำเร็จ" },
  completed: { icon: <CheckCircle2 className="w-3 h-3" />, cls: "bg-[var(--emerald-t)] text-[var(--emerald)] border-[var(--emerald)]", defaultLabel: "สำเร็จ" },
  failed:    { icon: <XCircle      className="w-3 h-3" />, cls: "bg-[var(--rose-t)]    text-[var(--rose)]    border-[var(--rose)]",    defaultLabel: "ล้มเหลว" },
  error:     { icon: <XCircle      className="w-3 h-3" />, cls: "bg-[var(--rose-t)]    text-[var(--rose)]    border-[var(--rose)]",    defaultLabel: "ล้มเหลว" },
  pending:   { icon: <Clock        className="w-3 h-3" />, cls: "bg-[var(--amber-t)]   text-[var(--amber)]   border-[var(--amber)]",   defaultLabel: "รอดำเนินการ" },
};

/** Colored badge for SUCCESS / FAILED / PENDING statuses. Case-insensitive. */
export function StatusBadge({ status, label, className }: StatusBadgeProps) {
  const key = status.toLowerCase();
  const s = STATUS_MAP[key] ?? STATUS_MAP.pending;
  return (
    <span className={cn(
      "inline-flex items-center gap-1 px-2 py-0.5 rounded font-semibold text-t-xs border",
      s.cls,
      className
    )}>
      {s.icon}
      {label ?? s.defaultLabel}
    </span>
  );
}

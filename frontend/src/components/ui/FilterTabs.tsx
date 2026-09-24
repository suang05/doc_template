"use client";
import { cn } from "@/utils/cn";

export interface FilterTabItem {
  key: string;
  label: string;
  /** Optional count badge — omit to hide */
  count?: number;
}

interface FilterTabsProps {
  items: FilterTabItem[];
  active: string;
  onChange: (key: string) => void;
  className?: string;
}

/** Pill-style filter tab group. Use for in-card status/category filters. */
export function FilterTabs({ items, active, onChange, className }: FilterTabsProps) {
  return (
    <div className={cn("flex items-center bg-[var(--sep)] rounded-[var(--rp)] p-0.5 gap-0.5", className)}>
      {items.map((item) => (
        <button
          key={item.key}
          onClick={() => onChange(item.key)}
          className={cn(
            "flex items-center gap-1 px-2.5 py-0.5 rounded-[var(--rp)] text-t-xs font-medium transition-colors whitespace-nowrap",
            active === item.key
              ? "bg-[var(--surf)] text-[var(--navy)] font-semibold shadow-sm"
              : "text-[var(--t3)] hover:text-[var(--t1)]"
          )}
        >
          {item.label}
          {item.count != null && (
            <span className={cn(
              "min-w-[16px] h-4 px-1 rounded text-[10px] font-bold tabular-nums text-center leading-4",
              active === item.key
                ? "bg-[var(--navy)] text-white"
                : "bg-[var(--border)] text-[var(--t3)]"
            )}>
              {item.count}
            </span>
          )}
        </button>
      ))}
    </div>
  );
}

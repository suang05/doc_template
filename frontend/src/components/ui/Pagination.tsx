"use client";
import { cn } from "@/utils/cn";

interface PaginationProps {
  page: number;
  /** Total number of pages */
  total: number;
  onChange: (page: number) => void;
  className?: string;
}

function PageBtn({
  label, active, disabled, onClick,
}: {
  label: React.ReactNode; active?: boolean; disabled?: boolean; onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={cn(
        "min-w-[28px] h-7 px-1.5 rounded text-t-xs font-medium border transition-colors",
        active
          ? "bg-[var(--navy)] text-white border-[var(--navy)]"
          : "border-[var(--border)] text-[var(--t2)] hover:bg-[var(--sep)] disabled:opacity-40 disabled:cursor-not-allowed"
      )}
    >
      {label}
    </button>
  );
}

/** Page navigation with first/prev/numbers/next/last. */
export function Pagination({ page, total, onChange, className }: PaginationProps) {
  const pages = (() => {
    const nums: (number | "…")[] = [];
    const delta = 2;
    for (let i = 1; i <= total; i++) {
      if (i === 1 || i === total || (i >= page - delta && i <= page + delta)) {
        nums.push(i);
      } else if (nums[nums.length - 1] !== "…") {
        nums.push("…");
      }
    }
    return nums;
  })();

  if (total <= 1) return null;

  return (
    <div className={cn("flex items-center gap-1", className)}>
      <PageBtn label="«" disabled={page === 1} onClick={() => onChange(1)} />
      <PageBtn label="‹" disabled={page === 1} onClick={() => onChange(page - 1)} />
      {pages.map((n, i) =>
        n === "…"
          ? <span key={`e${i}`} className="text-t-xs text-[var(--t3)] px-1">…</span>
          : <PageBtn key={n} label={n} active={n === page} onClick={() => onChange(n as number)} />
      )}
      <PageBtn label="›" disabled={page === total} onClick={() => onChange(page + 1)} />
      <PageBtn label="»" disabled={page === total} onClick={() => onChange(total)} />
    </div>
  );
}

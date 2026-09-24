"use client";
import { ReactNode } from "react";
import { Search } from "lucide-react";
import { cn } from "@/utils/cn";
import { FilterTabs, FilterTabItem } from "./FilterTabs";

interface ToolbarProps {
  /** Section title — omit to hide */
  title?: string;
  /** Record count shown next to title — omit to hide */
  count?: number | string;
  /** Controlled search value — presence enables search input */
  search?: string;
  onSearch?: (value: string) => void;
  /** Placeholder for search input */
  searchPlaceholder?: string;
  /** Filter tab items — presence enables filter tabs */
  filters?: FilterTabItem[];
  activeFilter?: string;
  onFilter?: (key: string) => void;
  /** Right-side slot: buttons, dropdowns, etc. */
  actions?: ReactNode;
  className?: string;
}

/**
 * Standard toolbar: title + count | search | filter tabs | actions
 * Each slot is optional — omit the prop to hide that section.
 *
 * @example
 * <Toolbar
 *   title="Audit Logs" count={total}
 *   search={q} onSearch={setQ}
 *   filters={STATUS_TABS} activeFilter={status} onFilter={setStatus}
 *   actions={<Button size="sm">Export</Button>}
 * />
 */
export function Toolbar({
  title, count, search, onSearch, searchPlaceholder = "ค้นหา...",
  filters, activeFilter = "", onFilter,
  actions, className,
}: ToolbarProps) {
  return (
    <div className={cn(
      "flex items-center gap-2 px-4 py-2.5 border-b border-[var(--border)] flex-wrap",
      className
    )}>
      {/* title + count */}
      {title && (
        <span className="text-t-sm font-semibold text-[var(--t1)]">{title}</span>
      )}
      {count != null && (
        <span className="text-t-xs text-[var(--t3)]">{count.toLocaleString()} รายการ</span>
      )}

      <div className="flex-1" />

      {/* search */}
      {search != null && (
        <div className="relative">
          <Search className="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-[var(--t3)] pointer-events-none" />
          <input
            value={search}
            onChange={(e) => onSearch?.(e.target.value)}
            placeholder={searchPlaceholder}
            className="pl-7 pr-3 py-1 text-t-xs bg-[var(--sep)] border border-[var(--border)] rounded-[var(--rp)] w-44 focus:outline-none focus:border-[var(--blue)] transition"
          />
        </div>
      )}

      {/* filter tabs */}
      {filters && activeFilter != null && onFilter && (
        <FilterTabs items={filters} active={activeFilter} onChange={onFilter} />
      )}

      {/* right actions */}
      {actions && (
        <div className="flex items-center gap-1.5">{actions}</div>
      )}
    </div>
  );
}

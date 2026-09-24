import { ReactNode } from "react";
import type { TableColumn } from "@/types/api";
import { cn } from "@/utils/cn";
import { EmptyState } from "./EmptyState";

interface TableProps<T extends object> {
  columns: TableColumn<T>[];
  rows: T[];
  loading?: boolean;
  /** Number of skeleton rows to show while loading — default 6 */
  skeletonRows?: number;
  emptyTitle?: string;
  emptyDescription?: string;
  /** Footer slot: Pagination, totals, etc. — omit to hide */
  footer?: ReactNode;
  /** Row hover actions slot — omit to disable */
  rowActions?: (row: T) => ReactNode;
  onRowClick?: (row: T) => void;
  className?: string;
}

/**
 * Standard data table with optional skeleton loading, footer slot, and row actions.
 *
 * @example
 * <Table
 *   columns={COLS} rows={logs} loading={loading}
 *   rowActions={(row) => <CopyBtn value={row.id} />}
 *   footer={<Pagination page={page} total={totalPages} onChange={setPage} />}
 * />
 */
export function Table<T extends object>({
  columns, rows, loading, skeletonRows = 6,
  emptyTitle = "ไม่มีข้อมูล", emptyDescription,
  footer, rowActions, onRowClick, className,
}: TableProps<T>) {
  const colCount = columns.length + (rowActions ? 1 : 0);

  return (
    <div className={cn("flex flex-col overflow-hidden", className)}>
      <div className="overflow-x-auto flex-1">
        <table className="w-full border-collapse text-[12.5px]">
          <thead>
            <tr className="bg-[var(--sep)] border-b border-[var(--border)]">
              {columns.map((col) => (
                <th
                  key={col.key}
                  style={{ width: col.width }}
                  className="px-4 py-2 text-left text-t-xs font-semibold text-[var(--t3)] uppercase tracking-[.4px] whitespace-nowrap"
                >
                  {col.header}
                </th>
              ))}
              {rowActions && <th className="px-2 py-2 w-10" />}
            </tr>
          </thead>
          <tbody>
            {loading ? (
              Array.from({ length: skeletonRows }).map((_, i) => (
                <tr key={i} className="border-b border-[var(--sep)]">
                  {columns.map((col) => (
                    <td key={col.key} className="px-4 py-2">
                      <div
                        className="h-3 rounded bg-[var(--sep)] animate-pulse"
                        style={{ width: `${55 + (i * 7 + col.key.length * 3) % 40}%` }}
                      />
                    </td>
                  ))}
                  {rowActions && <td className="px-2 py-2" />}
                </tr>
              ))
            ) : rows.length === 0 ? (
              <tr>
                <td colSpan={colCount} className="px-4 py-10">
                  <EmptyState title={emptyTitle} description={emptyDescription} />
                </td>
              </tr>
            ) : (
              rows.map((row, i) => (
                <tr
                  key={i}
                  onClick={() => onRowClick?.(row)}
                  className={cn(
                    "border-b border-[var(--sep)] last:border-0 group transition-colors",
                    (onRowClick || rowActions) && "hover:bg-[var(--bg)]",
                    onRowClick && "cursor-pointer"
                  )}
                >
                  {columns.map((col) => (
                    <td key={col.key} className="py-1.5 px-4 text-[var(--t2)]">
                      {col.render
                        ? col.render(row)
                        : String((row as Record<string, unknown>)[col.key] ?? "")}
                    </td>
                  ))}
                  {rowActions && (
                    <td className="py-1.5 px-2 text-right">
                      <div className="opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-end gap-1">
                        {rowActions(row)}
                      </div>
                    </td>
                  )}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* footer slot */}
      {footer && (
        <div className="flex items-center justify-between px-4 py-2 border-t border-[var(--sep)]">
          {footer}
        </div>
      )}
    </div>
  );
}

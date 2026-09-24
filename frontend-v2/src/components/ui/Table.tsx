import React from 'react';

export interface Column<T> {
  header: string;
  accessor?: keyof T | ((item: T) => React.ReactNode);
  align?: 'left' | 'center' | 'right';
  className?: string;
}

export interface TableProps<T> {
  columns: Column<T>[];
  data: T[];
  keyExtractor: (item: T) => string;
  emptyMessage?: string;
  loading?: boolean;
}

export function Table<T>({
  columns,
  data,
  keyExtractor,
  emptyMessage = 'ไม่พบข้อมูล',
  loading = false,
}: TableProps<T>) {
  return (
    <div className="w-full overflow-x-auto border border-border rounded-sm bg-surface">
      <table className="w-full text-left text-xs text-textSecondary border-collapse">
        <thead className="bg-surfaceSubtle border-b border-border text-[11px] font-semibold text-textMuted uppercase tracking-wider">
          <tr>
            {columns.map((col, idx) => {
              const alignClass = {
                left: 'text-left',
                center: 'text-center',
                right: 'text-right',
              }[col.align || 'left'];

              return (
                <th key={idx} className={`px-3 py-2.5 ${alignClass} ${col.className || ''}`}>
                  {col.header}
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {loading ? (
            <tr>
              <td colSpan={columns.length} className="px-3 py-8 text-center text-textMuted">
                <div className="flex items-center justify-center gap-2">
                  <span className="w-3.5 h-3.5 border-2 border-primary border-t-transparent rounded-full animate-spin" />
                  <span>กำลังโหลดข้อมูล...</span>
                </div>
              </td>
            </tr>
          ) : data.length === 0 ? (
            <tr>
              <td colSpan={columns.length} className="px-3 py-8 text-center text-textMuted">
                {emptyMessage}
              </td>
            </tr>
          ) : (
            data.map((item) => (
              <tr
                key={keyExtractor(item)}
                className="hover:bg-slate-50/70 transition-colors"
              >
                {columns.map((col, idx) => {
                  const alignClass = {
                    left: 'text-left',
                    center: 'text-center',
                    right: 'text-right',
                  }[col.align || 'left'];

                  let content: React.ReactNode;
                  if (typeof col.accessor === 'function') {
                    content = col.accessor(item);
                  } else if (col.accessor) {
                    content = item[col.accessor] as unknown as React.ReactNode;
                  }

                  return (
                    <td key={idx} className={`px-3 py-2 text-textPrimary ${alignClass} ${col.className || ''}`}>
                      {content}
                    </td>
                  );
                })}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}

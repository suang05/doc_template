import React from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from './Button';

export interface PaginationProps {
  currentPage: number;
  totalItems: number;
  pageSize: number;
  onPageChange: (page: number) => void;
}

export const Pagination: React.FC<PaginationProps> = ({
  currentPage,
  totalItems,
  pageSize,
  onPageChange,
}) => {
  const totalPages = Math.ceil(totalItems / pageSize) || 1;
  const startItem = totalItems === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const endItem = Math.min(currentPage * pageSize, totalItems);

  return (
    <div className="flex items-center justify-between px-3 py-2 bg-surface border border-border rounded-sm text-xs text-textSecondary">
      <div className="text-[11px] text-textMuted">
        แสดง <span className="font-semibold text-textPrimary">{startItem}</span> -{' '}
        <span className="font-semibold text-textPrimary">{endItem}</span> จากทั้งหมด{' '}
        <span className="font-semibold text-textPrimary">{totalItems}</span> รายการ
      </div>

      <div className="flex items-center gap-1.5">
        <span className="text-[11px] text-textMuted mr-2">
          หน้า {currentPage} จาก {totalPages}
        </span>

        <Button
          variant="secondary"
          size="sm"
          disabled={currentPage <= 1}
          onClick={() => onPageChange(currentPage - 1)}
          icon={ChevronLeft}
        >
          ก่อนหน้า
        </Button>

        <Button
          variant="secondary"
          size="sm"
          disabled={currentPage >= totalPages}
          onClick={() => onPageChange(currentPage + 1)}
          icon={ChevronRight}
          iconPosition="right"
        >
          ถัดไป
        </Button>
      </div>
    </div>
  );
};

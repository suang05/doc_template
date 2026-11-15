'use client';

import React from 'react';
import { AlertTriangle, RefreshCw, X } from 'lucide-react';
import type { ConflictState } from '@/hooks/useConflictRecovery';
import { Button } from './Button';

export interface ConflictBannerProps {
  conflict: ConflictState | null;
  onReload: () => void | Promise<void>;
  onDismiss?: () => void;
  isReloading?: boolean;
  reloadLabel?: string;
  className?: string;
}

export const ConflictBanner: React.FC<ConflictBannerProps> = ({
  conflict,
  onReload,
  onDismiss,
  isReloading = false,
  reloadLabel = 'โหลดข้อมูลล่าสุด',
  className = '',
}) => {
  if (!conflict) return null;

  return (
    <div
      role="alert"
      className={`flex flex-wrap items-center justify-between gap-3 rounded-sm border border-amber-300 bg-amber-50 px-3.5 py-2.5 text-amber-950 ${className}`}
    >
      <div className="flex items-start gap-2.5 text-xs">
        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden="true" />
        <div className="space-y-0.5">
          <p className="font-semibold">
            {conflict.status === 428
              ? 'จำเป็นต้องยืนยันเวอร์ชันข้อมูล (428 Precondition Required)'
              : 'พบการแก้ไขทับซ้อน (412 Precondition Failed)'}
          </p>
          <p className="text-[11px] text-amber-900">{conflict.message}</p>
          {conflict.traceId && (
            <p className="font-mono text-[10px] text-amber-700">Trace ID: {conflict.traceId}</p>
          )}
        </div>
      </div>

      <div className="flex items-center gap-2 shrink-0">
        <Button
          type="button"
          size="sm"
          variant="outline"
          loading={isReloading}
          icon={RefreshCw}
          onClick={() => void onReload()}
        >
          {reloadLabel}
        </Button>
        {onDismiss && (
          <button
            type="button"
            onClick={onDismiss}
            aria-label="ปิดการแจ้งเตือนข้อมูลขัดแย้ง"
            className="rounded-sm p-1 text-amber-700 hover:bg-amber-100 hover:text-amber-950 cursor-pointer"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        )}
      </div>
    </div>
  );
};

'use client';

import { useCallback, useState } from 'react';
import { ApiError } from '@/lib/api/client';

export interface ConflictState {
  status: 412 | 428;
  message: string;
  traceId?: string;
  errorCode?: string;
}

export function isConcurrencyConflictError(err: unknown): err is ApiError {
  return err instanceof ApiError && (err.status === 412 || err.status === 428);
}

export function useConflictRecovery() {
  const [conflict, setConflict] = useState<ConflictState | null>(null);

  const captureConflict = useCallback((err: unknown): boolean => {
    if (!isConcurrencyConflictError(err)) {
      return false;
    }

    setConflict({
      status: err.status === 428 ? 428 : 412,
      message:
        err.status === 428
          ? 'ไม่พบเวอร์ชันข้อมูล (If-Match) กรุณาโหลดข้อมูลล่าสุดก่อนบันทึก'
          : err.problemDetails?.detail ||
            'ข้อมูลถูกแก้ไขโดยผู้ใช้งานหรือหน้าต่างอื่นแล้ว กรุณาโหลดข้อมูลล่าสุดก่อนบันทึกซ้ำ',
      traceId: err.traceId,
      errorCode: err.errorCode,
    });
    return true;
  }, []);

  const clearConflict = useCallback(() => {
    setConflict(null);
  }, []);

  return {
    conflict,
    hasConflict: conflict !== null,
    captureConflict,
    clearConflict,
  };
}

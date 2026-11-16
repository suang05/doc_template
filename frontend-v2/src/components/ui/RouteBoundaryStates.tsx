import React from 'react';
import Link from 'next/link';
import { AlertTriangle, Home, RotateCcw } from 'lucide-react';
import { Button, buttonVariants } from './Button';
import { CardBlock } from './CardBlock';

export interface RouteLoadingSkeletonProps {
  variant?: 'page' | 'studio';
  className?: string;
}

export const RouteLoadingSkeleton: React.FC<RouteLoadingSkeletonProps> = ({
  variant = 'page',
  className = '',
}) => {
  if (variant === 'studio') {
    return (
      <div
        role="status"
        aria-live="polite"
        className={`flex flex-col flex-1 min-h-0 h-full gap-3 ${className}`}
      >
        <progress aria-label="กำลังโหลดข้อมูล..." className="sr-only" />
        <span className="sr-only">กำลังโหลด Template Studio...</span>

        <div className="h-11 bg-surface border border-border rounded-sm px-3 flex items-center justify-between shrink-0">
          <div className="flex items-center gap-2.5">
            <div className="h-4 w-36 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
            <div className="h-5 w-16 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
          </div>
          <div className="flex items-center gap-2">
            <div className="h-7 w-20 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
            <div className="h-7 w-24 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-3 flex-1 min-h-[420px]">
          <div className="bg-surface border border-border rounded-sm p-4 space-y-3">
            <div className="h-4 w-28 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
            <div className="h-3 w-full bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
            <div className="h-3 w-5/6 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
            <div className="h-3 w-3/4 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
            <div className="h-3 w-4/5 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
          </div>
          <div className="bg-surface border border-border rounded-sm p-4 space-y-3 flex flex-col">
            <div className="h-4 w-32 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
            <div className="flex-1 min-h-[260px] bg-surfaceSubtle/70 border border-border rounded-sm animate-pulse motion-reduce:animate-none" />
          </div>
        </div>
      </div>
    );
  }

  return (
    <div
      role="status"
      aria-live="polite"
      className={`space-y-4 w-full ${className}`}
    >
      <progress aria-label="กำลังโหลดข้อมูล..." className="sr-only" />
      <span className="sr-only">กำลังโหลดข้อมูล...</span>

      <div className="flex items-center justify-between pb-4 border-b border-border">
        <div className="space-y-2">
          <div className="h-5 w-48 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
          <div className="h-3.5 w-72 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
        </div>
        <div className="h-8 w-28 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
      </div>

      <div className="h-10 bg-surface border border-border rounded-sm px-3 flex items-center gap-3">
        <div className="h-4 w-44 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
        <div className="h-4 w-24 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
        <div className="ml-auto h-4 w-20 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
      </div>

      <div className="bg-surface border border-border rounded-sm divide-y divide-border">
        {[0, 1, 2, 3, 4].map((row) => (
          <div key={row} className="p-3.5 flex items-center justify-between gap-4">
            <div className="space-y-2 flex-1">
              <div className="h-3.5 w-1/3 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
              <div className="h-3 w-1/2 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
            </div>
            <div className="h-6 w-20 bg-surfaceSubtle rounded-sm animate-pulse motion-reduce:animate-none" />
          </div>
        ))}
      </div>
    </div>
  );
};

export interface RouteErrorFallbackProps {
  error?: (Error & { digest?: string }) | null;
  reset?: () => void;
  title?: string;
  description?: string;
  homeHref?: string;
  homeLabel?: string;
}

export const RouteErrorFallback: React.FC<RouteErrorFallbackProps> = ({
  error,
  reset,
  title = 'เกิดข้อผิดพลาดในการแสดงผลหน้าเว็บ',
  description = 'ระบบไม่สามารถโหลดข้อมูลหรือแสดงผลส่วนนี้ได้ในขณะนี้ กรุณาลองใหม่อีกครั้ง',
  homeHref = '/templates',
  homeLabel = 'กลับหน้าหลัก',
}) => {
  const detailMessage = error?.message?.trim() ? error.message.trim() : description;

  return (
    <div role="alert" className="w-full max-w-lg mx-auto py-8">
      <CardBlock className="p-6 shadow-sm">
        <div className="flex items-start gap-3.5">
          <div className="p-2.5 rounded-sm border border-red-200 bg-red-50 text-red-600 shrink-0">
            <AlertTriangle className="w-5 h-5" aria-hidden="true" />
          </div>
          <div className="min-w-0 flex-1">
            <h2 className="text-base font-semibold text-textPrimary">{title}</h2>
            <p className="mt-1 text-xs text-textSecondary leading-relaxed break-words">
              {detailMessage}
            </p>
            {error?.digest && (
              <div className="mt-3 inline-flex items-center gap-1.5 px-2 py-0.5 rounded-sm bg-surfaceSubtle border border-border text-[11px] font-mono text-textMuted">
                <span>Digest:</span>
                <span>{error.digest}</span>
              </div>
            )}
          </div>
        </div>

        <div className="mt-5 pt-4 border-t border-border flex flex-wrap items-center gap-2.5">
          {reset && (
            <Button type="button" variant="primary" size="md" icon={RotateCcw} onClick={reset}>
              ลองใหม่อีกครั้ง
            </Button>
          )}
          <Link href={homeHref} className={buttonVariants({ variant: 'outline', size: 'md' })}>
            <Home className="w-3.5 h-3.5" aria-hidden="true" />
            <span>{homeLabel}</span>
          </Link>
        </div>
      </CardBlock>
    </div>
  );
};

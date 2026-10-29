import React from 'react';
import {
  Eye,
  RefreshCw,
  AlertTriangle,
} from 'lucide-react';
import { Button } from '@/components/ui';

export interface StudioPreviewSectionProps {
  pdfUrl: string | null;
  previewLoading: boolean;
  previewError: string | null;
  previewErrorDetails?: {
    code?: string;
    detail?: string;
    errors?: Record<string, string[]>;
  } | null;
  renderLatencyMs: number | null;
  contentSizeBytes: number | null;
  hasTemplateSlug: boolean;
  onRefreshPreview: () => void;
}

export const StudioPreviewSection: React.FC<StudioPreviewSectionProps> = ({
  pdfUrl,
  previewLoading,
  previewError,
  previewErrorDetails,
  renderLatencyMs,
  contentSizeBytes,
  hasTemplateSlug,
  onRefreshPreview,
}) => {
  return (
    <div className="w-full md:w-1/2 flex flex-col h-full bg-slate-100 min-w-0">
      {/* Header bar */}
      <div className="h-9 bg-surfaceSubtle border-b border-border px-3 flex items-center justify-between shrink-0">
        <div className="flex items-center gap-2">
          <span className="text-[11px] font-semibold text-textSecondary flex items-center gap-1.5">
            <Eye className="w-3.5 h-3.5 text-primary" />
            Live PDF Preview (Gotenberg)
          </span>
          {previewLoading ? (
            <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-[2px] text-[10px] font-mono bg-blue-50 text-blue-700 border border-blue-200">
              <span className="w-1.5 h-1.5 rounded-full bg-blue-500 animate-pulse" />
              เรนเดอร์...
            </span>
          ) : renderLatencyMs !== null ? (
            <span
              className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded-[2px] text-[10px] font-mono border ${
                renderLatencyMs < 600
                  ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                  : renderLatencyMs < 2000
                  ? 'bg-amber-50 text-amber-700 border-amber-200'
                  : 'bg-rose-50 text-rose-700 border-rose-200'
              }`}
              title="Client roundtrip rendering latency"
            >
              ⚡ {renderLatencyMs}ms · {contentSizeBytes ? `${(contentSizeBytes / 1024).toFixed(1)} KB` : 'Stream'}
            </span>
          ) : null}
        </div>

        <div className="flex items-center gap-2">
          {pdfUrl && (
            <a
              href={pdfUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="px-2 py-0.5 bg-primary/10 text-primary hover:bg-primary/20 rounded-[2px] text-[10px] font-semibold transition-colors flex items-center gap-1"
            >
              เปิด PDF ใหม่
            </a>
          )}
          <button
            onClick={onRefreshPreview}
            className="p-1 rounded-[2px] text-textMuted hover:bg-slate-200 transition-colors cursor-pointer"
            title="รีเฟรช Preview"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${previewLoading ? 'animate-spin text-primary' : ''}`} />
          </button>
        </div>
      </div>

      {/* Main Preview Container */}
      <div className="flex-1 relative flex flex-col overflow-hidden items-center justify-center">
        {previewLoading && !pdfUrl ? (
          <div className="flex flex-col items-center justify-center text-xs text-textMuted gap-3">
            <span className="w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin" />
            <span>กำลังจำลองผล PDF (Gotenberg)...</span>
          </div>
        ) : previewError ? (
          <div className="bg-white border border-border p-5 rounded-sm text-left text-xs text-red-600 space-y-2.5 max-w-md shadow-sm mx-4">
            <div className="flex items-center gap-2">
              <AlertTriangle className="w-5 h-5 text-red-500 shrink-0" />
              <span className="font-semibold text-textPrimary">เกิดข้อผิดพลาดในการสร้างพรีวิว</span>
              {previewErrorDetails?.code && (
                <span className="ml-auto font-mono text-[10px] bg-red-50 text-red-700 px-1.5 py-0.5 rounded-[2px] border border-red-200">
                  {previewErrorDetails.code}
                </span>
              )}
            </div>
            <p className="text-[11px] text-textMuted leading-relaxed font-mono bg-slate-50 p-2 rounded-sm border border-slate-200 overflow-x-auto whitespace-pre-wrap">
              {previewErrorDetails?.detail || previewError}
            </p>
            {previewErrorDetails?.errors && Object.keys(previewErrorDetails.errors).length > 0 && (
              <div className="text-[11px] space-y-1 bg-red-50/50 p-2 rounded-sm border border-red-100">
                <span className="font-medium text-red-800">รายละเอียดจุดที่ผิดพลาด:</span>
                <ul className="list-disc list-inside text-red-700 space-y-0.5">
                  {Object.entries(previewErrorDetails.errors).map(([key, errs]) => (
                    <li key={key}>
                      <strong className="font-mono">{key}:</strong> {errs.join(', ')}
                    </li>
                  ))}
                </ul>
              </div>
            )}
            <div className="pt-1 flex justify-end">
              <Button variant="outline" size="sm" icon={RefreshCw} onClick={onRefreshPreview}>
                ลองใหม่อีกครั้ง
              </Button>
            </div>
          </div>
        ) : pdfUrl ? (
          <div className="w-full h-full bg-white shadow-sm flex flex-col">
            <iframe
              src={`${pdfUrl}#toolbar=0`}
              className="w-full h-full border-none"
              title="PDF Live Preview"
            />
          </div>
        ) : (
          <div className="text-xs text-textMuted flex flex-col items-center gap-2">
            {!hasTemplateSlug ? (
              <>
                <Eye className="w-6 h-6 text-border" />
                <p>กรุณาบันทึกแม่แบบก่อนเพื่อเปิดใช้งาน Live Preview</p>
              </>
            ) : (
              <p>กำลังเตรียมแสดงผล...</p>
            )}
          </div>
        )}
      </div>
    </div>
  );
};


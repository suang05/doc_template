import React from 'react';
import { FileText, RefreshCw, ExternalLink, Loader2, AlertCircle } from 'lucide-react';
import { Button } from '@/components/ui';

export interface GeneratorPreviewPanelProps {
  pdfUrl: string | null;
  loading: boolean;
  error: string | null;
  errorDetails?: {
    code?: string;
    detail?: string;
    errors?: Record<string, string[]>;
  } | null;
  renderLatencyMs: number | null;
  contentSizeBytes: number | null;
  onRefresh: () => void;
}

export const GeneratorPreviewPanel: React.FC<GeneratorPreviewPanelProps> = ({
  pdfUrl,
  loading,
  error,
  errorDetails,
  renderLatencyMs,
  contentSizeBytes,
  onRefresh,
}) => {
  return (
    <div className="lg:col-span-7 h-full flex flex-col bg-slate-100 rounded-sm border border-border overflow-hidden relative">
      {/* Preview Toolbar */}
      <div className="h-9 bg-surfaceSubtle border-b border-border px-3 flex items-center justify-between shrink-0 z-10">
        <div className="flex items-center gap-2">
          <span className="text-[11px] font-semibold text-textSecondary flex items-center gap-1.5">
            <FileText className="w-3.5 h-3.5 text-primary" />
            Live PDF Preview
          </span>
          {loading ? (
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
              <ExternalLink className="w-3 h-3" />
              เปิด PDF ใหม่
            </a>
          )}
          <button
            onClick={onRefresh}
            className="p-1 rounded-[2px] text-textMuted hover:bg-slate-200 transition-colors cursor-pointer"
            title="รีเฟรช Preview"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin text-primary' : ''}`} />
          </button>
        </div>
      </div>

      <div className="flex-1 relative flex flex-col overflow-hidden">
        {/* Loading overlay */}
        {loading && (
          <div className="absolute inset-0 flex flex-col items-center justify-center bg-white/80 backdrop-blur-sm z-10">
            <Loader2 className="w-8 h-8 text-primary animate-spin mb-2" />
            <p className="text-xs font-medium text-textPrimary">กำลังสร้าง Preview...</p>
          </div>
        )}

        {/* Error state */}
        {error && !loading && (
          <div className="absolute inset-0 flex flex-col items-center justify-center bg-white z-10 p-6 text-center gap-3">
            <div className="bg-white border border-border p-5 rounded-sm text-left text-xs text-red-600 space-y-2.5 max-w-md shadow-sm">
              <div className="flex items-center gap-2">
                <AlertCircle className="w-5 h-5 text-red-500 shrink-0" />
                <span className="font-semibold text-textPrimary">Preview ล้มเหลว</span>
                {errorDetails?.code && (
                  <span className="ml-auto font-mono text-[10px] bg-red-50 text-red-700 px-1.5 py-0.5 rounded-[2px] border border-red-200">
                    {errorDetails.code}
                  </span>
                )}
              </div>
              <p className="text-[11px] text-textMuted leading-relaxed font-mono bg-slate-50 p-2 rounded-sm border border-slate-200 overflow-x-auto whitespace-pre-wrap">
                {errorDetails?.detail || error}
              </p>
              {errorDetails?.errors && Object.keys(errorDetails.errors).length > 0 && (
                <div className="text-[11px] space-y-1 bg-red-50/50 p-2 rounded-sm border border-red-100">
                  <span className="font-medium text-red-800">รายละเอียดจุดที่ผิดพลาด:</span>
                  <ul className="list-disc list-inside text-red-700 space-y-0.5">
                    {Object.entries(errorDetails.errors).map(([key, errs]) => (
                      <li key={key}>
                        <strong className="font-mono">{key}:</strong> {errs.join(', ')}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
              <div className="pt-1 flex justify-end">
                <Button variant="outline" size="sm" icon={RefreshCw} onClick={onRefresh}>
                  ลองอีกครั้ง
                </Button>
              </div>
            </div>
          </div>
        )}

        {/* Empty state */}
        {!pdfUrl && !loading && !error && (
          <div className="absolute inset-0 flex flex-col items-center justify-center bg-white z-10 p-6 text-center gap-2">
            <FileText className="w-12 h-12 text-slate-200" />
            <p className="text-sm font-medium text-textSecondary">ยังไม่มี Preview</p>
            <p className="text-xs text-textMuted">กรอกข้อมูลด้านซ้าย Preview จะอัปเดตอัตโนมัติ</p>
          </div>
        )}

        {/* PDF iframe */}
        {pdfUrl && (
          <iframe
            src={`${pdfUrl}#toolbar=0`}
            className="w-full h-full border-none"
            title="PDF Preview"
          />
        )}
      </div>
    </div>
  );
};


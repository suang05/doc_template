'use client';

import React, { useRef, useState, useEffect, useCallback } from 'react';
import { Eye, FileText, Maximize2, RotateCcw, Printer, ExternalLink } from 'lucide-react';
import { documentsApi } from '@/lib/api/documents.api';

export interface PdfPreviewPanelProps {
  file: File | null;
  payload: string;
  /** When provided, called instead of the stateless render endpoint — used by the draft pipeline. */
  onPreview?: (payload: string) => Promise<Blob>;
  title?: string;
  badge?: React.ReactNode;
  emptySlot?: React.ReactNode;
  className?: string;
  showToolbar?: boolean;
}

interface ToolButtonProps {
  icon: React.ReactNode;
  label: string;
  onClick: () => void;
  disabled?: boolean;
}

const ToolButton: React.FC<ToolButtonProps> = ({ icon, label, onClick, disabled }) => (
  <button
    onClick={onClick}
    disabled={disabled}
    title={label}
    className="w-6 h-6 flex items-center justify-center rounded-sm text-textMuted hover:text-textPrimary hover:bg-slate-100 transition-colors cursor-pointer disabled:opacity-30 disabled:cursor-not-allowed"
  >
    {icon}
  </button>
);

export const PdfPreviewPanel: React.FC<PdfPreviewPanelProps> = ({
  file,
  payload,
  onPreview,
  title = 'Live PDF Preview',
  badge,
  emptySlot,
  className = '',
  showToolbar = true,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const iframeRef    = useRef<HTMLIFrameElement>(null);
  const timerRef     = useRef<ReturnType<typeof setTimeout> | null>(null);
  const urlRef       = useRef<string | null>(null);

  const [pdfUrl,  setPdfUrl]  = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const revoke = () => {
    if (urlRef.current) {
      URL.revokeObjectURL(urlRef.current);
      urlRef.current = null;
    }
  };

  const schedule = useCallback((f: File | null, p: string) => {
    if (timerRef.current) clearTimeout(timerRef.current);
    if (!f && !onPreview) {
      revoke();
      setPdfUrl(null);
      setLoading(false);
      return;
    }
    setLoading(true);
    timerRef.current = setTimeout(async () => {
      try {
        const blob = onPreview
          ? await onPreview(p)
          : await documentsApi.renderStatelessDocument(f!, p);
        revoke();
        const url = URL.createObjectURL(blob);
        urlRef.current = url;
        setPdfUrl(url);
      } catch {
        // preview failure is non-fatal — keep current display
      } finally {
        setLoading(false);
      }
    }, 800);
  }, [onPreview]);

  useEffect(() => {
    schedule(file, payload);
  }, [file, payload, schedule]);

  useEffect(() => () => {
    if (timerRef.current) clearTimeout(timerRef.current);
    revoke();
  }, []);

  const handleFullscreen = () => containerRef.current?.requestFullscreen().catch(() => {});
  const handleReset      = () => { if (iframeRef.current?.src) iframeRef.current.src = iframeRef.current.src; };
  const handlePrint      = () => iframeRef.current?.contentWindow?.print();
  const handleNewTab     = () => { if (pdfUrl) window.open(pdfUrl, '_blank'); };

  return (
    <div
      ref={containerRef}
      className={`flex flex-col bg-slate-200 rounded-sm border border-border overflow-hidden relative ${className}`}
    >
      {/* Header bar */}
      {showToolbar && <div className="absolute top-0 left-0 right-0 bg-white border-b border-border px-2.5 h-10 flex items-center justify-between z-20 shadow-sm">
        <div className="flex items-center gap-2 min-w-0">
          <Eye className="w-4 h-4 text-textMuted shrink-0" />
          <span className="text-xs font-medium text-textSecondary">{title}</span>
          {badge && <span className="shrink-0">{badge}</span>}
        </div>

        <div className="flex items-center gap-1 shrink-0 ml-2">
          {loading && (
            <span className="text-[11px] text-textMuted flex items-center gap-1 mr-1">
              <span className="w-3 h-3 border border-primary border-t-transparent rounded-full animate-spin inline-block" />
              กำลังสร้าง...
            </span>
          )}
          <div className="w-px h-4 bg-border mx-1" />
          <ToolButton icon={<RotateCcw className="w-3.5 h-3.5" />} label="รีเซ็ต" onClick={handleReset} disabled={!pdfUrl} />
          <ToolButton icon={<Printer className="w-3.5 h-3.5" />}   label="พิมพ์"  onClick={handlePrint}  disabled={!pdfUrl} />
          <ToolButton icon={<ExternalLink className="w-3.5 h-3.5" />} label="เปิดแท็บใหม่" onClick={handleNewTab} disabled={!pdfUrl} />
          <ToolButton icon={<Maximize2 className="w-3.5 h-3.5" />} label="เต็มจอ" onClick={handleFullscreen} />
        </div>
      </div>}

      {/* Content */}
      <div className={`flex-1 w-full h-full ${showToolbar ? 'pt-10' : ''}`}>
        {pdfUrl ? (
          <iframe ref={iframeRef} src={pdfUrl} className="w-full h-full border-none bg-white" title={title} />
        ) : loading ? (
          <div className="h-full flex flex-col items-center justify-center bg-white p-6 text-center">
            <span className="w-8 h-8 border-2 border-primary border-t-transparent rounded-full animate-spin inline-block mb-3" />
            <p className="text-sm font-medium text-textSecondary">กำลังสร้าง Preview...</p>
          </div>
        ) : emptySlot ? (
          <div className="h-full flex flex-col items-center justify-center bg-white p-6 text-center">
            {emptySlot}
          </div>
        ) : (
          <div className="h-full flex flex-col items-center justify-center bg-white p-6 text-center">
            <FileText className="w-12 h-12 text-slate-200 mb-3" />
            <p className="text-sm font-medium text-textSecondary">ไม่มีข้อมูล Preview</p>
          </div>
        )}
      </div>
    </div>
  );
};

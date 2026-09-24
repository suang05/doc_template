'use client';

import { useEffect, useRef, useState } from 'react';
import { api } from '@/lib/api-client';
import type { DocumentGenerationRequest, OutputFormat } from '@/types/api';

interface PreviewPanelProps {
  templateName: string;
  data: Record<string, unknown>;
  outputFormat?: OutputFormat;
  onClose?: () => void;
  /** Called once when PDF has finished loading */
  onRendered?: () => void;
}

type Status = 'idle' | 'loading' | 'ready' | 'error';

export function PreviewPanel({ templateName, data, outputFormat = 'pdf', onClose, onRendered }: PreviewPanelProps) {
  const [status, setStatus] = useState<Status>('idle');
  const [error, setError] = useState<string | null>(null);
  const blobUrlRef = useRef<string | null>(null);
  const [blobUrl, setBlobUrl] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      setStatus('loading');
      setError(null);

      // Revoke previous blob URL before creating a new one
      if (blobUrlRef.current) {
        URL.revokeObjectURL(blobUrlRef.current);
        blobUrlRef.current = null;
      }

      try {
        const req: DocumentGenerationRequest = { templateName, outputFormat, data };
        const blob = await api.previewDocument(req);
        if (cancelled) return;

        const url = URL.createObjectURL(blob);
        blobUrlRef.current = url;
        setBlobUrl(url);
        setStatus('ready');
        onRendered?.();
      } catch (err) {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : 'เกิดข้อผิดพลาด');
        setStatus('error');
      }
    };

    load();

    return () => {
      cancelled = true;
      // Revoke blob URL on unmount
      if (blobUrlRef.current) {
        URL.revokeObjectURL(blobUrlRef.current);
        blobUrlRef.current = null;
      }
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [templateName, outputFormat, JSON.stringify(data)]);

  return (
    <div className="flex flex-col h-full w-full bg-[var(--surf)] rounded-[var(--r)] border border-[var(--border)] overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-2.5 border-b border-[var(--border)] bg-[var(--bg)]">
        <span className="text-sm font-semibold text-[var(--t1)]">
          ตัวอย่างเอกสาร — {templateName}
        </span>
        <div className="flex items-center gap-2">
          {status === 'loading' && (
            <span className="text-xs text-[var(--t2)]">กำลังโหลด…</span>
          )}
          {onClose && (
            <button
              type="button"
              onClick={onClose}
              className="text-[var(--t2)] hover:text-[var(--t1)] text-lg leading-none px-1"
              aria-label="ปิด"
            >
              ×
            </button>
          )}
        </div>
      </div>

      {/* Body */}
      <div className="flex-1 min-h-0 relative">
        {status === 'loading' && (
          <div className="absolute inset-0 flex items-center justify-center text-[var(--t2)] text-sm">
            กำลังสร้างตัวอย่าง…
          </div>
        )}

        {status === 'error' && (
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 p-6 text-center">
            <span className="text-sm font-medium text-[var(--rose)]">ไม่สามารถสร้างตัวอย่างได้</span>
            <span className="text-xs text-[var(--t2)]">{error}</span>
          </div>
        )}

        {status === 'ready' && blobUrl && (
          <iframe
            src={blobUrl}
            title={`ตัวอย่าง ${templateName}`}
            className="w-full h-full border-0"
          />
        )}
      </div>
    </div>
  );
}

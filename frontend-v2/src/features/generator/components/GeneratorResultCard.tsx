import React from 'react';
import { CheckCircle, Copy, ExternalLink, Download, AlertCircle } from 'lucide-react';
import { CardBlock, Badge, Button } from '@/components/ui';
import { toBffProxyUrl } from '@/lib/api/client';
import type { GenerateDocumentResultDto } from '@/lib/api/types';

export interface GeneratorResultCardProps {
  result: GenerateDocumentResultDto | null;
  error: string | null;
  onCopy: (url: string) => void;
}

export const GeneratorResultCard: React.FC<GeneratorResultCardProps> = ({
  result,
  error,
  onCopy,
}) => {
  const proxyDownloadUrl = result ? toBffProxyUrl(result.url) : '#';

  return (
    <>
      {result && (
        <CardBlock
          category="financial"
          className="p-4 space-y-3 animate-in fade-in-50 duration-150 shrink-0"
        >
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <CheckCircle className="w-5 h-5 text-emerald-600 shrink-0" />
              <div>
                <p className="text-xs font-bold text-textPrimary">สร้างเอกสารสำเร็จ</p>
                <p className="text-[11px] text-textMuted font-mono">ID: {result.generationId}</p>
              </div>
            </div>
            <Badge status="success" dot size="sm">
              พร้อมดาวน์โหลด
            </Badge>
          </div>

          <div className="p-2 bg-surfaceSubtle border border-border rounded-sm flex items-center gap-2 overflow-hidden">
            <span className="text-[11px] font-mono text-textSecondary truncate flex-1">
              {result.url}
            </span>
            <div className="flex items-center gap-1 shrink-0">
              <button
                type="button"
                onClick={() => onCopy(result.url)}
                className="p-1 rounded-sm text-textMuted hover:text-textPrimary hover:bg-surface transition-colors cursor-pointer"
                aria-label="คัดลอกลิงก์"
                title="คัดลอกลิงก์"
              >
                <Copy className="w-3.5 h-3.5" />
              </button>
              <a
                href={proxyDownloadUrl}
                target="_blank"
                rel="noreferrer"
                className="p-1 rounded-sm text-textMuted hover:text-textPrimary hover:bg-surface transition-colors"
                aria-label="เปิดในแท็บใหม่"
                title="เปิดในแท็บใหม่"
              >
                <ExternalLink className="w-3.5 h-3.5" />
              </a>
            </div>
          </div>

          <div className="flex justify-end">
            <a href={proxyDownloadUrl} target="_blank" rel="noreferrer">
              <Button variant="success" size="sm" icon={Download}>
                ดาวน์โหลด ({result.outputFormat.toUpperCase()})
              </Button>
            </a>
          </div>
        </CardBlock>
      )}

      {error && (
        <div
          role="alert"
          className="flex items-center gap-2 p-2.5 bg-red-50 border border-red-200 text-red-800 text-xs rounded-sm shrink-0"
        >
          <AlertCircle className="w-4 h-4 shrink-0 text-red-600" />
          <span>{error}</span>
        </div>
      )}
    </>
  );
};

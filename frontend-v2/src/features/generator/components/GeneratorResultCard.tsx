import React from 'react';
import { CheckCircle, Copy, ExternalLink, Download, AlertCircle } from 'lucide-react';
import { CardBlock, Badge, Button } from '@/components/ui';
import { GenerateDocumentResponse } from '@/types/api';

export interface GeneratorResultCardProps {
  result: GenerateDocumentResponse | null;
  error: string | null;
  onCopy: (url: string) => void;
}

export const GeneratorResultCard: React.FC<GeneratorResultCardProps> = ({
  result,
  error,
  onCopy,
}) => {
  return (
    <>
      {result && (
        <CardBlock category="financial" className="p-4 space-y-3 animate-in fade-in-50 duration-150 shrink-0">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <CheckCircle className="w-5 h-5 text-emerald-600 shrink-0" />
              <div>
                <p className="text-xs font-bold text-textPrimary">สร้างเอกสารสำเร็จ</p>
                <p className="text-[11px] text-textMuted font-mono">ID: {result.generationId}</p>
              </div>
            </div>
            <Badge status="success" dot size="sm">
              พร้อมดาวน์โหลด (24 ชม.)
            </Badge>
          </div>

          <div className="p-2 bg-surfaceSubtle border border-border rounded-sm flex items-center gap-2 overflow-hidden">
            <span className="text-[11px] font-mono text-textSecondary truncate flex-1">{result.url}</span>
            <div className="flex items-center gap-1 shrink-0">
              <button
                onClick={() => onCopy(result.url)}
                className="p-1 rounded-sm text-textMuted hover:text-textPrimary hover:bg-surface transition-colors cursor-pointer"
                title="คัดลอกลิงก์"
              >
                <Copy className="w-3.5 h-3.5" />
              </button>
              <a
                href={result.url}
                target="_blank"
                rel="noreferrer"
                className="p-1 rounded-sm text-textMuted hover:text-textPrimary hover:bg-surface transition-colors"
                title="เปิดในแท็บใหม่"
              >
                <ExternalLink className="w-3.5 h-3.5" />
              </a>
            </div>
          </div>

          <div className="flex justify-end">
            <a href={result.url} target="_blank" rel="noreferrer">
              <Button variant="success" size="sm" icon={Download}>
                ดาวน์โหลด ({result.outputFormat.toUpperCase()})
              </Button>
            </a>
          </div>
        </CardBlock>
      )}

      {error && (
        <div className="flex items-center gap-2 p-2.5 bg-red-50 border border-red-200 text-red-800 text-xs rounded-sm shrink-0">
          <AlertCircle className="w-4 h-4 shrink-0 text-red-600" />
          <span>{error}</span>
        </div>
      )}
    </>
  );
};

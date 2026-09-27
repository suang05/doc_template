'use client';

import { useState } from 'react';
import { Eye, EyeOff, RefreshCw, CheckCircle, XCircle } from 'lucide-react';
import { type MappingRow, PT_MAP } from '@/lib/mapping/mappingMapper';
import { simulatePreview, type PreviewResult } from '@/lib/mapping/previewSimulator';
import type { DataSourceType } from '@/types/api';
import { cn } from '@/utils/cn';

const DEFAULT_SAMPLE = '{\n  "contractNo": "SMK-2026-001",\n  "customerName": "นายสมชาย ใจดี",\n  "payment": { "subtotal": 2500000 }\n}';

export interface MappingPreviewPanelProps {
  rows: MappingRow[];
  jsonText: string;
  onJsonTextChange: (text: string) => void;
  previewing: boolean;
  onClose: () => void;
  onPdfPreview: () => void;
}

function dsColor(ds: DataSourceType) {
  return ds === 'json' ? 'text-teal-600' : 'text-amber-600';
}

export function MappingPreviewPanel({
  rows, jsonText, onJsonTextChange, previewing, onClose, onPdfPreview,
}: MappingPreviewPanelProps) {
  // Auto-run on first render using lazy state initializer — no useEffect needed, no stale closure.
  const [results, setResults] = useState<PreviewResult[]>(() => simulatePreview(rows, jsonText));
  const [ran, setRan] = useState(() => rows.length > 0);

  const run = () => {
    setResults(simulatePreview(rows, jsonText));
    setRan(true);
  };

  return (
    <aside className="w-72 flex-shrink-0 border-l border-border bg-surface flex flex-col overflow-hidden sticky top-0 max-h-screen">
      <div className="px-3 py-2.5 border-b border-border flex items-center justify-between">
        <div className="flex items-center gap-2 text-xs font-semibold text-textPrimary">
          <Eye className="w-3.5 h-3.5 text-cyan-600" />
          <span>จำลองผลลัพธ์</span>
        </div>
        <button
          onClick={onClose}
          className="p-1 rounded text-textMuted hover:text-textPrimary hover:bg-surfaceSubtle transition-colors"
        >
          <EyeOff className="w-3.5 h-3.5" />
        </button>
      </div>

      <div className="px-3 pt-2.5 pb-2 border-b border-border/60">
        <label className="text-[10.5px] font-semibold text-textMuted block mb-1.5">ข้อมูลตัวอย่าง (JSON)</label>
        <textarea
          value={jsonText}
          onChange={e => onJsonTextChange(e.target.value)}
          rows={5}
          spellCheck={false}
          className="w-full font-mono text-[10.5px] px-2 py-1.5 bg-surfaceSubtle border border-border rounded resize-none outline-none focus:border-primary text-textPrimary"
        />
        <div className="flex gap-2 mt-2">
          <button
            onClick={run}
            className="flex-1 flex items-center justify-center gap-1.5 text-[11px] font-medium py-1 rounded border border-border bg-surface hover:bg-surfaceSubtle transition-colors text-textPrimary"
          >
            <RefreshCw className="w-3 h-3" />
            รันจำลอง
          </button>
          <button
            onClick={onPdfPreview}
            disabled={previewing}
            className="flex-1 flex items-center justify-center gap-1.5 text-[11px] font-medium py-1 rounded bg-primary text-white hover:bg-primary/90 disabled:opacity-50 transition-colors"
          >
            {previewing
              ? <RefreshCw className="w-3 h-3 animate-spin" />
              : <Eye className="w-3 h-3" />}
            PDF
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto divide-y divide-border/50">
        {!ran ? (
          <p className="text-[11px] text-textMuted text-center py-8">กด &ldquo;รันจำลอง&rdquo; เพื่อดูผลลัพธ์</p>
        ) : results.length === 0 ? (
          <p className="text-[11px] text-textMuted text-center py-8">ไม่มีฟิลด์</p>
        ) : results.map(r => {
          const ptCfg = PT_MAP[r.phType] ?? PT_MAP.text;
          const ok = r.value != null;
          return (
            <div key={r.ph} className="flex items-start gap-2 px-3 py-2">
              <span className="flex-shrink-0 mt-0.5">
                {ok
                  ? <CheckCircle className="w-3.5 h-3.5 text-emerald-500" />
                  : <XCircle className="w-3.5 h-3.5 text-textMuted" />}
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-1.5 flex-wrap">
                  <span className="font-mono text-[10px] text-textMuted">{`{{${r.ph}}}`}</span>
                  <span className="text-[9.5px] font-semibold" style={{ color: ptCfg.color }}>{ptCfg.label}</span>
                </div>
                {ok
                  ? <p className="text-[11px] font-medium text-textPrimary truncate mt-0.5">{r.value}</p>
                  : <p className="text-[11px] text-textMuted italic mt-0.5">ไม่พบค่า</p>}
              </div>
              <span className={cn('text-[9.5px] font-semibold mt-0.5 flex-shrink-0', dsColor(r.ds))}>{r.ds}</span>
            </div>
          );
        })}
      </div>
    </aside>
  );
}

export { DEFAULT_SAMPLE as MAPPING_PREVIEW_DEFAULT_SAMPLE };

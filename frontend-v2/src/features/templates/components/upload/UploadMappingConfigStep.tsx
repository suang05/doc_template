import React from 'react';
import { Info, AlertCircle, ChevronRight } from 'lucide-react';
import { CardBlock, Input, Button, Pill, PillIntent } from '@/components/ui';

export type MappingDraft = {
  placeholder: string;
  sourcePath: string;
  label: string;
  required: boolean;
  transform: string | null;
  kind: 'text' | 'qr' | 'barcode' | 'image' | 'transform';
};

export const KIND_LABEL: Record<MappingDraft['kind'], string> = {
  text: 'Text',
  transform: 'Transform',
  qr: 'QR',
  barcode: 'Barcode',
  image: 'Image',
};

export interface UploadMappingConfigStepProps {
  mappingDrafts: MappingDraft[];
  jsonPayload: string;
  error: string | null;
  onUpdateDraft: (idx: number, field: keyof MappingDraft, value: unknown) => void;
  onJsonPayloadChange: (val: string) => void;
  onNext: () => void;
}

export const UploadMappingConfigStep: React.FC<UploadMappingConfigStepProps> = ({
  mappingDrafts,
  jsonPayload,
  error,
  onUpdateDraft,
  onJsonPayloadChange,
  onNext,
}) => {
  return (
    <>
      {/* JSON Editor */}
      <CardBlock className="p-4 space-y-3 flex flex-col" style={{ minHeight: '180px' }}>
        <h3 className="text-xs font-bold text-textPrimary border-b border-border pb-2 flex items-center justify-between">
          <span>JSON ทดสอบ</span>
          <span className="text-[10px] font-normal text-textMuted bg-slate-100 px-1.5 py-0.5 rounded-sm">
            Live Preview
          </span>
        </h3>
        <textarea
          className="w-full flex-1 text-xs font-mono border border-border rounded-sm p-3 bg-slate-50 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary/20 resize-none"
          style={{ minHeight: '120px' }}
          value={jsonPayload}
          onChange={(e) => onJsonPayloadChange(e.target.value)}
          spellCheck={false}
        />
      </CardBlock>

      {/* Mapping list */}
      {mappingDrafts.length === 0 ? (
        <CardBlock className="flex-1 flex flex-col items-center justify-center p-8 text-center gap-3">
          <Info className="w-8 h-8 text-slate-300" />
          <div>
            <p className="text-sm font-semibold text-textPrimary">ไม่พบ Placeholder</p>
            <p className="text-xs text-textMuted mt-1">ไม่พบแท็ก {'{{...}}'} ในไฟล์เอกสารนี้</p>
          </div>
        </CardBlock>
      ) : (
        <div className="flex-1 overflow-y-auto scrollbar-hide space-y-2 pr-1">
          {mappingDrafts.map((draft, idx) => (
            <CardBlock key={idx} className="p-3 space-y-2.5">
              <div className="flex items-center justify-between gap-2">
                <code className="text-[11px] font-mono text-indigo-700 bg-indigo-50 px-1.5 py-0.5 rounded-sm truncate flex-1">
                  {`{{${draft.placeholder}}}`}
                </code>
                <Pill intent={draft.kind as PillIntent} size="sm" className="shrink-0">
                  {KIND_LABEL[draft.kind]}
                </Pill>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <Input
                  label="Label"
                  value={draft.label}
                  onChange={(e) => onUpdateDraft(idx, 'label', e.target.value)}
                  placeholder="คำอธิบายฟิลด์"
                />
                <Input
                  label="Source Path"
                  value={draft.sourcePath}
                  onChange={(e) => onUpdateDraft(idx, 'sourcePath', e.target.value)}
                  placeholder="data.field"
                  className="font-mono"
                />
              </div>
              <label className="flex items-center gap-2 cursor-pointer select-none">
                <input
                  type="checkbox"
                  checked={draft.required}
                  onChange={(e) => onUpdateDraft(idx, 'required', e.target.checked)}
                  className="w-3.5 h-3.5 rounded-sm border-border accent-primary"
                />
                <span className="text-[11px] text-textSecondary">บังคับกรอก</span>
              </label>
            </CardBlock>
          ))}
        </div>
      )}

      {error && (
        <div className="p-2.5 bg-red-50 border border-red-200 text-red-800 text-xs rounded-sm flex items-center gap-2 shrink-0">
          <AlertCircle className="w-4 h-4 shrink-0 text-red-600" />
          <span>{error}</span>
        </div>
      )}

      <div className="flex items-center justify-end pt-1 shrink-0">
        <Button variant="primary" icon={ChevronRight} onClick={onNext}>
          ต่อไป
        </Button>
      </div>
    </>
  );
};


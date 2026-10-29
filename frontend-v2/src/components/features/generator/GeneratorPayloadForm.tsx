import React from 'react';
import { Tag, AlertCircle } from 'lucide-react';
import { CardBlock, Input } from '@/components/ui';
import { ThaiTransformTypes, FieldMappingDto } from '@/types/api';

export interface GeneratorPayloadFormProps {
  mappingsLoading: boolean;
  hasMappings: boolean;
  mappings: FieldMappingDto[];
  formValues: Record<string, string>;
  fieldErrors: Record<string, string>;
  fallbackJson: string;
  jsonError: string | null;
  onFillSampleData: () => void;
  onFieldChange: (field: string, value: string) => void;
  onFallbackJsonChange: (json: string) => void;
}

export const GeneratorPayloadForm: React.FC<GeneratorPayloadFormProps> = ({
  mappingsLoading,
  hasMappings,
  mappings,
  formValues,
  fieldErrors,
  fallbackJson,
  jsonError,
  onFillSampleData,
  onFieldChange,
  onFallbackJsonChange,
}) => {
  const transformLabel = (transformKey: string | null | undefined) =>
    ThaiTransformTypes.find((t) => t.value === transformKey)?.label;

  return (
    <CardBlock className="p-4 space-y-3 flex-1 flex flex-col">
      <div className="flex items-center justify-between pb-2 border-b border-border shrink-0">
        <h3 className="text-xs font-bold text-textPrimary">ข้อมูลเอกสาร (Data)</h3>
        <div className="flex items-center gap-2">
          <button
            onClick={onFillSampleData}
            className="text-[10px] text-sky-600 hover:text-sky-700 bg-sky-50 hover:bg-sky-100 px-2 py-1 rounded-[2px] transition-colors border border-sky-200 cursor-pointer"
          >
            + กรอกข้อมูลทดสอบ
          </button>
          <span className="text-[10px] text-textMuted bg-slate-100 px-1.5 py-0.5 rounded-[2px]">
            {mappingsLoading
              ? 'กำลังโหลด...'
              : hasMappings
              ? `${mappings.length} fields`
              : 'JSON mode'}
          </span>
        </div>
      </div>

      {mappingsLoading ? (
        <div className="space-y-3 animate-pulse">
          {[...Array(3)].map((_, i) => (
            <div key={i} className="space-y-1.5">
              <div className="h-3 w-24 bg-slate-200 rounded" />
              <div className="h-8 w-full bg-slate-100 rounded" />
            </div>
          ))}
        </div>
      ) : hasMappings ? (
        <div className="space-y-3 overflow-y-auto flex-1">
          {mappings.map((m) => (
            <div key={m.id} className="space-y-1">
              <div className="flex items-center justify-between gap-2">
                <label className="text-xs font-medium text-textSecondary flex items-center gap-1">
                  {m.label}
                  {m.required && <span className="text-red-500 text-[10px]">*</span>}
                </label>
                {m.transform && (
                  <span className="flex items-center gap-1 text-[10px] text-textMuted shrink-0">
                    <Tag className="w-2.5 h-2.5" />
                    {transformLabel(m.transform) ?? m.transform}
                  </span>
                )}
              </div>
              <Input
                value={formValues[m.sourcePath] ?? ''}
                onChange={(e) => onFieldChange(m.sourcePath, e.target.value)}
                placeholder={m.defaultValue || `ระบุ ${m.label}`}
                error={fieldErrors[m.sourcePath]}
              />
            </div>
          ))}
        </div>
      ) : (
        <div className="flex flex-col flex-1 space-y-1">
          <div className="flex items-center justify-between">
            <span className="text-[10px] text-textMuted">ยังไม่มี Field Mapping — กรอก JSON โดยตรง</span>
            <span className="text-[10px] font-mono text-textMuted">application/json</span>
          </div>
          <textarea
            value={fallbackJson}
            onChange={(e) => onFallbackJsonChange(e.target.value)}
            className={`w-full flex-1 min-h-[220px] p-3 rounded-sm font-mono text-xs bg-zinc-900 text-zinc-300 border outline-none focus:border-primary resize-none ${
              jsonError ? 'border-red-500' : 'border-border'
            }`}
          />
          {jsonError && (
            <p className="text-[11px] text-red-600 flex items-center gap-1">
              <AlertCircle className="w-3.5 h-3.5 shrink-0" />
              {jsonError}
            </p>
          )}
        </div>
      )}
    </CardBlock>
  );
};


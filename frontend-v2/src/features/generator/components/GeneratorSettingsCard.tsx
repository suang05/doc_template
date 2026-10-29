import React from 'react';
import { CardBlock, Select, Input } from '@/components/ui';
import { OutputFormat, TemplateDto, tokens, DocumentFormat } from '@/types/api';

export interface GeneratorSettingsCardProps {
  selectedTemplate?: TemplateDto;
  selectedSlug: string;
  templateOptions: { value: string; label: string }[];
  templatesLoading: boolean;
  outputFormat: OutputFormat;
  formatOptions: { value: OutputFormat; label: string }[];
  documentRef: string;
  changeNote: string;
  onTemplateChange: (slug: string) => void;
  onOutputFormatChange: (format: OutputFormat) => void;
  onDocumentRefChange: (val: string) => void;
  onChangeNoteChange: (val: string) => void;
}

export function engineLabel(template: TemplateDto | undefined): string {
  const fmt = (template?.fileFormat || 'html') as DocumentFormat;
  return tokens.formats[fmt]?.label || fmt.toUpperCase();
}

export const GeneratorSettingsCard: React.FC<GeneratorSettingsCardProps> = ({
  selectedTemplate,
  selectedSlug,
  templateOptions,
  templatesLoading,
  outputFormat,
  formatOptions,
  documentRef,
  changeNote,
  onTemplateChange,
  onOutputFormatChange,
  onDocumentRefChange,
  onChangeNoteChange,
}) => {
  return (
    <CardBlock className="p-4 space-y-3 shrink-0">
      <div className="flex items-center justify-between pb-2 border-b border-border">
        <h3 className="text-xs font-bold text-textPrimary">การตั้งค่า (Settings)</h3>
        {selectedTemplate && (
          <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-100 text-textMuted border border-border">
            {engineLabel(selectedTemplate)}
          </span>
        )}
      </div>

      <Select
        label="แม่แบบเอกสาร (Template)"
        options={templateOptions}
        value={selectedSlug}
        onChange={(e) => onTemplateChange(e.target.value)}
        disabled={templatesLoading}
      />

      <Select
        label="รูปแบบผลลัพธ์ (Output Format)"
        options={formatOptions}
        value={outputFormat}
        onChange={(e) => onOutputFormatChange(e.target.value as OutputFormat)}
      />

      <div className="grid grid-cols-2 gap-2">
        <Input
          label="Document Ref"
          value={documentRef}
          onChange={(e) => onDocumentRefChange(e.target.value)}
          placeholder="เช่น SMK-CONDO-2026-001"
        />
        <Input
          label="Change Note"
          value={changeNote}
          onChange={(e) => onChangeNoteChange(e.target.value)}
          placeholder="เช่น สัญญาฉบับสมบูรณ์"
        />
      </div>
    </CardBlock>
  );
};


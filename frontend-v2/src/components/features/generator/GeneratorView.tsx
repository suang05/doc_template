'use client';

import React, { useState, useEffect } from 'react';
import {
  FileText,
  Download,
  CheckCircle,
  AlertCircle,
  Play,
  Copy,
  ExternalLink,
  RefreshCw,
  Loader2,
  Tag,
} from 'lucide-react';
import { useTemplates } from '@/hooks/useTemplates';
import { useDocumentGenerator } from '@/hooks/useDocumentGenerator';
import { useGeneratorForm } from '@/hooks/useGeneratorForm';
import { useFormPreview } from '@/hooks/useFormPreview';
import { useAutoSelectFirst } from '@/hooks/useAutoSelectFirst';
import { OutputFormat, TemplateDto, ThaiTransformTypes, tokens, DocumentFormat } from '@/types/api';
import {
  Button,
  Badge,
  Input,
  Select,
  CardBlock,
  Toast,
} from '@/components/ui';

export interface GeneratorViewProps {
  initialSlug?: string;
}

function engineLabel(template: TemplateDto | undefined): string {
  const fmt = (template?.fileFormat || 'html') as DocumentFormat;
  return tokens.formats[fmt]?.label || fmt.toUpperCase();
}

export const GeneratorView: React.FC<GeneratorViewProps> = ({ initialSlug }) => {
  const { templates, loading: templatesLoading } = useTemplates();
  const { generate, loading: generating, error: generateError, result, resetResult } = useDocumentGenerator();

  const [selectedSlug, setSelectedSlug] = useAutoSelectFirst(templates, (t) => t.slug, initialSlug);
  const [outputFormat, setOutputFormat] = useState<OutputFormat>('pdf');
  const [documentRef, setDocumentRef] = useState('DOC-2026-0001');
  const [changeNote, setChangeNote] = useState('สร้างเอกสารฉบับจริง');
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(null);

  const selectedTemplate = templates.find((t) => t.slug === selectedSlug);

  // Auto-reset output format if template format changes
  useEffect(() => {
    const tFmt = selectedTemplate?.fileFormat;
    if (tFmt === 'html' && outputFormat !== 'pdf') {
      setOutputFormat('pdf');
    } else if (tFmt === 'docx' && outputFormat === 'xlsx') {
      setOutputFormat('pdf');
    } else if (tFmt === 'xlsx' && outputFormat === 'docx') {
      setOutputFormat('pdf');
    }
  }, [selectedTemplate?.fileFormat, outputFormat]);

  // Field mappings + form state for selected template
  const {
    mappings,
    mappingsLoading,
    hasMappings,
    formValues,
    setField,
    fieldErrors,
    fallbackJson,
    handleFallbackChange,
    jsonError,
    assembledData,
    validateForm,
    fillSampleData,
  } = useGeneratorForm(selectedTemplate?.id);

  // Auto-preview — debounced 800ms on data change
  const preview = useFormPreview(selectedTemplate?.id, assembledData);

  const handleTemplateChange = (slug: string) => {
    setSelectedSlug(slug);
    resetResult();
  };

  const handleGenerate = async () => {
    if (!selectedSlug) {
      setToast({ type: 'error', message: 'กรุณาเลือกแม่แบบเอกสาร' });
      return;
    }
    if (!validateForm()) return;

    try {
      await generate(selectedSlug, {
        payload: assembledData as Record<string, any>,
        outputFormat: outputFormat,
        documentRef: documentRef || undefined,
        changeNote: changeNote || undefined,
      });
    } catch {
      // error already in generateError state
    }
  };

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text).then(() => {
      setToast({ type: 'success', message: 'คัดลอก URL แล้ว' });
    });
  };

  const templateOptions = templates.map((t) => ({
    value: t.slug,
    label: `${t.name} (${t.slug})`,
  }));

  const formatOptions: { value: OutputFormat; label: string }[] = (() => {
    const options: { value: OutputFormat; label: string }[] = [
      { value: 'pdf', label: 'PDF Document (.pdf)' },
    ];
    
    const fmt = selectedTemplate?.fileFormat;
    if (fmt === 'docx' || !fmt) {
      options.push({ value: 'docx', label: 'Microsoft Word (.docx)' });
    }
    if (fmt === 'xlsx' || !fmt) {
      options.push({ value: 'xlsx', label: 'Microsoft Excel (.xlsx)' });
    }
    
    return options;
  })();

  const transformLabel = (transformKey: string | null | undefined) =>
    ThaiTransformTypes.find((t) => t.value === transformKey)?.label;

  return (
    <div className="flex flex-col h-[calc(100vh-8rem)] min-h-[600px] gap-4">

      {/* 🔹 Top action bar 🔹 */}
      <div className="flex items-center justify-between pb-3 border-b border-border shrink-0">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-sm bg-sky-50 text-sky-600 flex items-center justify-center border border-sky-200 shrink-0">
            <FileText className="w-4 h-4" />
          </div>
          <div className="flex items-center flex-wrap gap-2">
            <span className="text-xs text-textMuted">เลือกแม่แบบ ➝ กรอกข้อมูล ➝ ดู Preview ➝ สร้างเอกสารจริง</span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="md"
            icon={RefreshCw}
            loading={preview.loading}
            onClick={preview.refresh}
          >
            รีเฟรช Preview
          </Button>
          <Button
            variant="primary"
            size="md"
            icon={Play}
            loading={generating}
            onClick={handleGenerate}
          >
            สร้างเอกสาร
          </Button>
        </div>
      </div>

      {/* ── Main split content ── */}
      <div className="flex-1 grid grid-cols-1 lg:grid-cols-12 gap-4 min-h-0 overflow-hidden">

        {/* ── Left: Config + Data form ── */}
        <div className="lg:col-span-5 flex flex-col gap-3 overflow-y-auto pr-1 pb-4 custom-scrollbar">

          {/* Result card */}
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
                <Badge status="success" dot size="sm">พร้อมดาวน์โหลด (24 ชม.)</Badge>
              </div>

              <div className="p-2 bg-surfaceSubtle border border-border rounded-sm flex items-center gap-2 overflow-hidden">
                <span className="text-[11px] font-mono text-textSecondary truncate flex-1">{result.url}</span>
                <div className="flex items-center gap-1 shrink-0">
                  <button
                    onClick={() => handleCopy(result.url)}
                    className="p-1 rounded-sm text-textMuted hover:text-textPrimary hover:bg-surface transition-colors"
                    title="คัดลอกลิงก์"
                  >
                    <Copy className="w-3.5 h-3.5" />
                  </button>
                  <a href={result.url} target="_blank" rel="noreferrer"
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

          {/* Generate error */}
          {generateError && (
            <div className="flex items-center gap-2 p-2.5 bg-red-50 border border-red-200 text-red-800 text-xs rounded-sm shrink-0">
              <AlertCircle className="w-4 h-4 shrink-0 text-red-600" />
              <span>{generateError}</span>
            </div>
          )}

          {/* ─ Section 1: Settings ─ */}
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
              onChange={(e) => handleTemplateChange(e.target.value)}
              disabled={templatesLoading}
            />

            <Select
              label="รูปแบบผลลัพธ์ (Output Format)"
              options={formatOptions}
              value={outputFormat}
              onChange={(e) => setOutputFormat(e.target.value as OutputFormat)}
            />

            <div className="grid grid-cols-2 gap-2">
              <Input
                label="Document Ref"
                value={documentRef}
                onChange={(e) => setDocumentRef(e.target.value)}
                placeholder="เช่น SMK-CONDO-2026-001"
              />
              <Input
                label="Change Note"
                value={changeNote}
                onChange={(e) => setChangeNote(e.target.value)}
                placeholder="เช่น สัญญาฉบับสมบูรณ์"
              />
            </div>
          </CardBlock>

          {/* ─ Section 2: Data Fields ─ */}
          <CardBlock className="p-4 space-y-3 flex-1 flex flex-col">
            <div className="flex items-center justify-between pb-2 border-b border-border shrink-0">
              <h3 className="text-xs font-bold text-textPrimary">ข้อมูลเอกสาร (Data)</h3>
              <div className="flex items-center gap-2">
                <button
                  onClick={fillSampleData}
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
              /* Skeleton while loading mappings */
              <div className="space-y-3 animate-pulse">
                {[...Array(3)].map((_, i) => (
                  <div key={i} className="space-y-1.5">
                    <div className="h-3 w-24 bg-slate-200 rounded" />
                    <div className="h-8 w-full bg-slate-100 rounded" />
                  </div>
                ))}
              </div>
            ) : hasMappings ? (
              /* Auto-generated form fields from FieldMappingDto[] */
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
                      onChange={(e) => setField(m.sourcePath, e.target.value)}
                      placeholder={m.defaultValue || `ระบุ ${m.label}`}
                      error={fieldErrors[m.sourcePath]}
                    />
                  </div>
                ))}
              </div>
            ) : (
              /* Fallback: raw JSON textarea when no mappings configured */
              <div className="flex flex-col flex-1 space-y-1">
                <div className="flex items-center justify-between">
                  <span className="text-[10px] text-textMuted">ยังไม่มี Field Mapping — กรอก JSON โดยตรง</span>
                  <span className="text-[10px] font-mono text-textMuted">application/json</span>
                </div>
                <textarea
                  value={fallbackJson}
                  onChange={(e) => handleFallbackChange(e.target.value)}
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
        </div>

        {/* ── Right: Live PDF Preview ── */}
        <div className="lg:col-span-7 h-full flex flex-col bg-slate-100 rounded-sm border border-border overflow-hidden relative">
          {/* Preview Toolbar */}
          <div className="h-9 bg-surfaceSubtle border-b border-border px-3 flex items-center justify-between shrink-0 z-10">
            <div className="flex items-center gap-2">
              <span className="text-[11px] font-semibold text-textSecondary flex items-center gap-1.5">
                <FileText className="w-3.5 h-3.5 text-primary" />
                Live PDF Preview
              </span>
              {preview.loading ? (
                <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-[2px] text-[10px] font-mono bg-blue-50 text-blue-700 border border-blue-200">
                  <span className="w-1.5 h-1.5 rounded-full bg-blue-500 animate-pulse" />
                  เรนเดอร์...
                </span>
              ) : preview.renderLatencyMs !== null ? (
                <span
                  className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded-[2px] text-[10px] font-mono border ${
                    preview.renderLatencyMs < 600
                      ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                      : preview.renderLatencyMs < 2000
                      ? 'bg-amber-50 text-amber-700 border-amber-200'
                      : 'bg-rose-50 text-rose-700 border-rose-200'
                  }`}
                  title="Client roundtrip rendering latency"
                >
                  ⚡ {preview.renderLatencyMs}ms · {preview.contentSizeBytes ? `${(preview.contentSizeBytes / 1024).toFixed(1)} KB` : 'Stream'}
                </span>
              ) : null}
            </div>

            <div className="flex items-center gap-2">
              {preview.pdfUrl && (
                <a
                  href={preview.pdfUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="px-2 py-0.5 bg-primary/10 text-primary hover:bg-primary/20 rounded-[2px] text-[10px] font-semibold transition-colors flex items-center gap-1"
                >
                  <ExternalLink className="w-3 h-3" />
                  เปิด PDF ใหม่
                </a>
              )}
              <button
                onClick={preview.refresh}
                className="p-1 rounded-[2px] text-textMuted hover:bg-slate-200 transition-colors cursor-pointer"
                title="รีเฟรช Preview"
              >
                <RefreshCw className={`w-3.5 h-3.5 ${preview.loading ? 'animate-spin text-primary' : ''}`} />
              </button>
            </div>
          </div>

          <div className="flex-1 relative flex flex-col overflow-hidden">
            {/* Loading overlay */}
            {preview.loading && (
              <div className="absolute inset-0 flex flex-col items-center justify-center bg-white/80 backdrop-blur-sm z-10">
                <Loader2 className="w-8 h-8 text-primary animate-spin mb-2" />
                <p className="text-xs font-medium text-textPrimary">กำลังสร้าง Preview...</p>
              </div>
            )}

            {/* Error state */}
            {preview.error && !preview.loading && (
              <div className="absolute inset-0 flex flex-col items-center justify-center bg-white z-10 p-6 text-center gap-3">
                <div className="bg-white border border-border p-5 rounded-sm text-left text-xs text-red-600 space-y-2.5 max-w-md shadow-sm">
                  <div className="flex items-center gap-2">
                    <AlertCircle className="w-5 h-5 text-red-500 shrink-0" />
                    <span className="font-semibold text-textPrimary">Preview ล้มเหลว</span>
                    {preview.errorDetails?.code && (
                      <span className="ml-auto font-mono text-[10px] bg-red-50 text-red-700 px-1.5 py-0.5 rounded-[2px] border border-red-200">
                        {preview.errorDetails.code}
                      </span>
                    )}
                  </div>
                  <p className="text-[11px] text-textMuted leading-relaxed font-mono bg-slate-50 p-2 rounded-sm border border-slate-200 overflow-x-auto whitespace-pre-wrap">
                    {preview.errorDetails?.detail || preview.error}
                  </p>
                  {preview.errorDetails?.errors && Object.keys(preview.errorDetails.errors).length > 0 && (
                    <div className="text-[11px] space-y-1 bg-red-50/50 p-2 rounded-sm border border-red-100">
                      <span className="font-medium text-red-800">รายละเอียดจุดที่ผิดพลาด:</span>
                      <ul className="list-disc list-inside text-red-700 space-y-0.5">
                        {Object.entries(preview.errorDetails.errors).map(([key, errs]) => (
                          <li key={key}>
                            <strong className="font-mono">{key}:</strong> {errs.join(', ')}
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                  <div className="pt-1 flex justify-end">
                    <Button variant="outline" size="sm" icon={RefreshCw} onClick={preview.refresh}>
                      ลองอีกครั้ง
                    </Button>
                  </div>
                </div>
              </div>
            )}

            {/* Empty state (no preview yet) */}
            {!preview.pdfUrl && !preview.loading && !preview.error && (
              <div className="absolute inset-0 flex flex-col items-center justify-center bg-white z-10 p-6 text-center gap-2">
                <FileText className="w-12 h-12 text-slate-200" />
                <p className="text-sm font-medium text-textSecondary">ยังไม่มี Preview</p>
                <p className="text-xs text-textMuted">กรอกข้อมูลด้านซ้าย Preview จะอัปเดตอัตโนมัติ</p>
              </div>
            )}

            {/* PDF iframe */}
            {preview.pdfUrl && (
              <iframe
                src={`${preview.pdfUrl}#toolbar=0`}
                className="w-full h-full border-none"
                title="PDF Preview"
              />
            )}
          </div>
        </div>
      </div>

      {/* Toast */}
      {toast && (
        <Toast
          type={toast.type}
          message={toast.message}
          onClose={() => setToast(null)}
        />
      )}
    </div>
  );
};

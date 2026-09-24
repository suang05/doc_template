'use client';

import React, { useState, useRef, useCallback } from 'react';
import {
  UploadCloud,
  FileText,
  FileSpreadsheet,
  X,
  AlertCircle,
  Info,
  ChevronRight,
  ChevronLeft,
  Save,
  CheckCircle2,
  Settings2,
} from 'lucide-react';
import { templatesApi } from '@/lib/api/templates.api';
import { ApiError } from '@/lib/api/client';
import { DocumentCategory, tokens } from '@/tokens';
import { Button, Input, Select, CardBlock, Badge, PdfPreviewPanel, Pill, PillIntent } from '@/components/ui';
import { SaveFieldMappingItem } from '@/types/api';
import { buildMockJson } from './mockJsonBuilder';

// ---- helpers ----
function humanizeKey(key: string): string {
  return key
    .split('.')
    .map((part) =>
      part
        .replace(/_/g, ' ')
        .replace(/([a-z])([A-Z])/g, '$1 $2')
        .replace(/\b\w/g, (c) => c.toUpperCase())
    )
    .join(' › ');
}

function parsePlaceholder(raw: string): {
  sourcePath: string;
  transform: string | null;
  kind: 'text' | 'qr' | 'barcode' | 'image' | 'transform';
} {
  const colonIdx = raw.indexOf(':');
  if (colonIdx <= 0) return { sourcePath: raw, transform: null, kind: 'text' };
  const left = raw.slice(0, colonIdx).trim().toLowerCase();
  const right = raw.slice(colonIdx + 1).trim();
  if (left === 'qr' || left === 'qrcode') return { sourcePath: right, transform: null, kind: 'qr' };
  if (left === 'barcode') return { sourcePath: right, transform: null, kind: 'barcode' };
  if (left === 'image') return { sourcePath: right, transform: null, kind: 'image' };
  return { sourcePath: left, transform: right, kind: 'transform' };
}

type MappingDraft = {
  placeholder: string;
  sourcePath: string;
  label: string;
  required: boolean;
  transform: string | null;
  kind: 'text' | 'qr' | 'barcode' | 'image' | 'transform';
};

const KIND_LABEL: Record<MappingDraft['kind'], string> = {
  text: 'Text', transform: 'Transform', qr: 'QR', barcode: 'Barcode', image: 'Image',
};

// Step indicator labels
const STEPS = [
  { label: 'อัปโหลด', icon: UploadCloud },
  { label: 'ตั้งค่า Mapping', icon: Settings2 },
  { label: 'ยืนยัน', icon: CheckCircle2 },
];

export interface UploadTemplateViewProps {
  onSuccess?: (templateId: string) => void;
  onCancel?: () => void;
}

export const UploadTemplateView: React.FC<UploadTemplateViewProps> = ({ onSuccess, onCancel }) => {
  // ---- step ----
  const [step, setStep] = useState<1 | 2 | 3>(1);

  // ---- shared state ----
  const [file, setFile] = useState<File | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // ---- step 1 ----
  const [uploading, setUploading] = useState(false);

  // ---- step 2 ----
  const [draftId, setDraftId] = useState<string | null>(null);
  const [mappingDrafts, setMappingDrafts] = useState<MappingDraft[]>([]);
  const [jsonPayload, setJsonPayload] = useState('{}');

  // ---- step 3 ----
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [slugEdited, setSlugEdited] = useState(false);
  const [category, setCategory] = useState<DocumentCategory>('operations');
  const [saving, setSaving] = useState(false);

  const categories = Object.entries(tokens.categories).map(([key, val]) => ({
    value: key,
    label: val.label,
  }));

  const getFileIcon = (filename: string) => {
    if (filename.endsWith('.docx')) return <FileText className="w-10 h-10 text-blue-500" />;
    if (filename.endsWith('.xlsx')) return <FileSpreadsheet className="w-10 h-10 text-emerald-500" />;
    return <FileText className="w-10 h-10 text-slate-500" />;
  };

  const handleFileChange = (selectedFile: File | null) => {
    setError(null);
    if (!selectedFile) { setFile(null); return; }
    const valid = ['.docx', '.xlsx'].some(ext => selectedFile.name.toLowerCase().endsWith(ext));
    if (!valid) {
      setError('หน้านี้รองรับเฉพาะไฟล์ .docx (Word) และ .xlsx (Excel) เท่านั้น — หากต้องการเขียนโค้ดหรือนำเข้า .html กรุณาใช้ Template Studio');
      setFile(null);
      return;
    }
    setFile(selectedFile);
    if (!name) {
      const base = selectedFile.name.replace(/\.[^/.]+$/, '');
      setName(base);
      if (!slugEdited) {
        setSlug(base.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)+/g, ''));
      }
    }
  };

  const onDragOver = (e: React.DragEvent) => { e.preventDefault(); setIsDragging(true); };
  const onDragLeave = () => setIsDragging(false);
  const onDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    const dropped = e.dataTransfer.files?.[0];
    if (!dropped) return;
    const valid = ['.html', '.htm', '.docx', '.xlsx'].some(ext => dropped.name.toLowerCase().endsWith(ext));
    if (!valid) { setError('รองรับเฉพาะ .html .docx .xlsx'); return; }
    handleFileChange(dropped);
  };

  // ---- step 1 submit — parse draft (no DB write) ----
  const handleParse = async () => {
    if (!file) { setError('ต้องเลือกไฟล์'); return; }
    setUploading(true);
    setError(null);
    try {
      const { draftId: id, placeholders } = await templatesApi.parseDraft(file);
      setDraftId(id);
      const drafts: MappingDraft[] = placeholders.map((raw) => {
        const { sourcePath, transform, kind } = parsePlaceholder(raw);
        return { placeholder: raw, sourcePath, label: humanizeKey(sourcePath), required: false, transform, kind };
      });
      setMappingDrafts(drafts);
      setJsonPayload(buildMockJson(placeholders));
      setStep(2);
    } catch (err: any) {
      setError(err.message || 'วิเคราะห์ไฟล์ล้มเหลว');
    } finally {
      setUploading(false);
    }
  };

  // ---- onPreview callback for PdfPreviewPanel in step 2 ----
  const handlePreview = useCallback(async (payload: string): Promise<Blob> => {
    if (!draftId) throw new Error('ยังไม่มี draft');
    return templatesApi.previewDraft(draftId, payload);
  }, [draftId]);

  // ---- step 2: update draft ----
  const updateDraft = (idx: number, field: keyof MappingDraft, value: unknown) => {
    setMappingDrafts(prev => prev.map((d, i) => i === idx ? { ...d, [field]: value } : d));
  };

  // ---- step 3: commit draft ----
  const handleCommit = async () => {
    if (!draftId || !name || !slug) return;
    setSaving(true);
    setError(null);
    try {
      const mappings: SaveFieldMappingItem[] = mappingDrafts.map((d, i) => ({
        placeholder: d.placeholder,
        sourcePath: d.sourcePath,
        label: d.label,
        required: d.required,
        transform: d.transform ?? null,
        defaultValue: null,
        sortOrder: i,
        dataSourceType: 'json' as const,
        datasetAlias: null,
        resultPath: null,
        mathExpression: null,
      }));
      const { templateId } = await templatesApi.commitDraft(draftId, {
        name: name.trim(),
        slug: slug.trim().toLowerCase(),
        category,
        mappings,
      });
      onSuccess?.(templateId);
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 410) {
        setError('Draft หมดอายุ (30 นาที) กรุณาอัปโหลดไฟล์ใหม่');
        setStep(1);
        setDraftId(null);
      } else {
        setError(err instanceof Error ? err.message : 'บันทึกล้มเหลว');
      }
    } finally {
      setSaving(false);
    }
  };

  // ===== RENDER =====
  const StepIcon = STEPS[step - 1].icon;

  return (
    <div className="flex flex-col h-[calc(100vh-8rem)] min-h-[600px] gap-4">

      {/* Header */}
      <div className="flex items-center justify-between pb-3 border-b border-border shrink-0">
        <div className="flex items-center gap-3">
          {step > 1 && (
            <button
              onClick={() => setStep((step - 1) as 1 | 2 | 3)}
              className="w-8 h-8 rounded-sm border border-border flex items-center justify-center hover:bg-surfaceSubtle transition-colors cursor-pointer text-textMuted shrink-0"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
          )}
          <div className={`w-8 h-8 rounded-sm flex items-center justify-center border shrink-0 ${
            step === 1 ? 'bg-indigo-50 text-indigo-600 border-indigo-200'
            : step === 2 ? 'bg-amber-50 text-amber-600 border-amber-200'
            : 'bg-emerald-50 text-emerald-600 border-emerald-200'
          }`}>
            <StepIcon className="w-4 h-4" />
          </div>
          <div className="flex items-center flex-wrap gap-2">
            <span className={`text-xs font-medium px-2 py-0.5 border rounded-sm ${
              step === 1 ? 'bg-indigo-50 text-indigo-700 border-indigo-200'
              : step === 2 ? 'bg-amber-50 text-amber-700 border-amber-200'
              : 'bg-emerald-50 text-emerald-700 border-emerald-200'
            }`}>
              ขั้น {step}  {STEPS[step - 1].label}
            </span>
            <span className="text-xs text-textMuted">
              {step === 1 ? '.docx · .xlsx (Office)'
               : step === 2 ? `พบ ${mappingDrafts.length} placeholder`
               : 'ชื่อ · Slug · หมวดหมู่'}
            </span>
          </div>
        </div>

        {/* Step dots */}
        <div className="flex items-center gap-1.5">
          {[1, 2, 3].map(s => (
            <div key={s} className={`w-1.5 h-1.5 rounded-full transition-colors ${s === step ? 'bg-primary' : 'bg-border'}`} />
          ))}
        </div>
      </div>

      {/* Split layout */}
      <div className="flex-1 grid grid-cols-1 lg:grid-cols-12 gap-4 min-h-0 overflow-hidden">

        {/* LEFT */}
        <div className="lg:col-span-5 flex flex-col gap-4 overflow-y-auto scrollbar-hide pr-1 pb-4 min-h-0">

          {/* ── Step 1: Upload only ── */}
          {step === 1 && (
            <>
              <CardBlock className="p-4 flex flex-col gap-3">
                <h3 className="text-xs font-bold text-textPrimary border-b border-border pb-2">ไฟล์แม่แบบ Office</h3>
                <div
                  className={`flex flex-col items-center justify-center border-2 border-dashed rounded-sm p-6 transition-colors cursor-pointer min-h-[200px] ${
                    isDragging
                      ? 'border-indigo-500 bg-indigo-50/50'
                      : 'border-slate-300 bg-surfaceSubtle hover:bg-slate-50 hover:border-slate-400'
                  }`}
                  onDragOver={onDragOver}
                  onDragLeave={onDragLeave}
                  onDrop={onDrop}
                  onClick={() => !file && fileInputRef.current?.click()}
                >
                  <input
                    type="file"
                    ref={fileInputRef}
                    onChange={(e) => handleFileChange(e.target.files?.[0] || null)}
                    accept=".docx,.xlsx"
                    className="hidden"
                  />
                  {!file ? (
                    <div className="text-center flex flex-col items-center gap-2">
                      <div className="w-12 h-12 rounded-sm bg-slate-100 flex items-center justify-center">
                        <UploadCloud className="w-6 h-6 text-slate-400" />
                      </div>
                      <p className="text-sm font-semibold text-textPrimary">คลิก หรือ ลากไฟล์ Word / Excel มาวาง</p>
                      <p className="text-[11px] text-textMuted">.docx (Word) · .xlsx (Excel) · สูงสุด 20 MB</p>
                    </div>
                  ) : (
                    <div className="text-center flex flex-col items-center gap-2 w-full">
                      {getFileIcon(file.name)}
                      <p className="text-sm font-semibold text-textPrimary truncate w-full px-4">{file.name}</p>
                      <p className="text-[11px] text-textMuted">{(file.size / 1024).toFixed(1)} KB</p>
                      <button
                        onClick={(e) => { e.stopPropagation(); handleFileChange(null); }}
                        className="mt-1 text-xs font-medium text-red-600 hover:text-red-700 bg-red-50 hover:bg-red-100 px-3 py-1.5 rounded-sm flex items-center gap-1 transition-colors cursor-pointer"
                      >
                        <X className="w-3.5 h-3.5" /> ลบไฟล์
                      </button>
                    </div>
                  )}
                </div>
              </CardBlock>

              {error && (
                <div className="p-2.5 bg-red-50 border border-red-200 text-red-800 text-xs rounded-sm flex items-center gap-2">
                  <AlertCircle className="w-4 h-4 shrink-0 text-red-600" />
                  <span>{error}</span>
                </div>
              )}

              <div className="flex items-center justify-end gap-2 pt-1">
                {onCancel && <Button variant="outline" onClick={onCancel}>ยกเลิก</Button>}
                <Button
                  variant="primary"
                  onClick={handleParse}
                  loading={uploading}
                  icon={uploading ? undefined : ChevronRight}
                  disabled={!file}
                >
                  {uploading ? 'กำลังวิเคราะห์...' : 'วิเคราะห์ Field'}
                </Button>
              </div>
            </>
          )}

          {/* ── Step 2: Mapping + JSON editor ── */}
          {step === 2 && (
            <>
              {/* JSON Editor */}
              <CardBlock className="p-4 space-y-3 flex flex-col" style={{ minHeight: '180px' }}>
                <h3 className="text-xs font-bold text-textPrimary border-b border-border pb-2 flex items-center justify-between">
                  <span>JSON ทดสอบ</span>
                  <span className="text-[10px] font-normal text-textMuted bg-slate-100 px-1.5 py-0.5 rounded-sm">Live Preview</span>
                </h3>
                <textarea
                  className="w-full flex-1 text-xs font-mono border border-border rounded-sm p-3 bg-slate-50 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary/20 resize-none"
                  style={{ minHeight: '120px' }}
                  value={jsonPayload}
                  onChange={(e) => setJsonPayload(e.target.value)}
                  spellCheck={false}
                />
              </CardBlock>

              {/* Mapping list */}
              {mappingDrafts.length === 0 ? (
                <CardBlock className="flex-1 flex flex-col items-center justify-center p-8 text-center gap-3">
                  <Info className="w-8 h-8 text-slate-300" />
                  <div>
                    <p className="text-sm font-semibold text-textPrimary">ไม่พบ Placeholder</p>
                    <p className="text-xs text-textMuted mt-1">
                      ไม่พบแท็ก {'{{...}}'} ในไฟล์เอกสารนี้
                    </p>
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
                          onChange={(e) => updateDraft(idx, 'label', e.target.value)}
                          placeholder="คำอธิบายฟิลด์"
                        />
                        <Input
                          label="Source Path"
                          value={draft.sourcePath}
                          onChange={(e) => updateDraft(idx, 'sourcePath', e.target.value)}
                          placeholder="data.field"
                          className="font-mono"
                        />
                      </div>
                      <label className="flex items-center gap-2 cursor-pointer select-none">
                        <input
                          type="checkbox"
                          checked={draft.required}
                          onChange={(e) => updateDraft(idx, 'required', e.target.checked)}
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
                <Button
                  variant="primary"
                  icon={ChevronRight}
                  onClick={() => { setError(null); setStep(3); }}
                >
                  ต่อไป
                </Button>
              </div>
            </>
          )}

          {/* ── Step 3: Confirm metadata ── */}
          {step === 3 && (
            <>
              <CardBlock className="p-4 space-y-4">
                <h3 className="text-xs font-bold text-textPrimary border-b border-border pb-2">ข้อมูลแม่แบบ</h3>
                <Input
                  label="ชื่อแม่แบบ"
                  value={name}
                  onChange={(e) => {
                    const val = e.target.value;
                    setName(val);
                    if (!slugEdited) {
                      setSlug(val.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)+/g, ''));
                    }
                  }}
                  placeholder="เช่น สัญญาจ้างงานทั่วไป"
                  required
                />
                <Input
                  label="Slug"
                  value={slug}
                  onChange={(e) => {
                    setSlug(e.target.value.toLowerCase().replace(/[^a-z0-9-_]/g, ''));
                    setSlugEdited(true);
                  }}
                  placeholder="เช่น general-contract"
                  helperText="รหัสเรียก API — ต้องไม่ซ้ำ"
                  required
                />
                <Select
                  label="หมวดหมู่"
                  options={categories}
                  value={category}
                  onChange={(e) => setCategory(e.target.value as DocumentCategory)}
                />
              </CardBlock>

              {/* Summary */}
              <CardBlock className="p-4 space-y-2">
                <h3 className="text-xs font-bold text-textPrimary border-b border-border pb-2">สรุป</h3>
                <div className="flex items-center justify-between text-xs">
                  <span className="text-textMuted">ไฟล์</span>
                  <span className="font-medium text-textPrimary truncate ml-2">{file?.name}</span>
                </div>
                <div className="flex items-center justify-between text-xs">
                  <span className="text-textMuted">Placeholder</span>
                  <span className="font-medium text-textPrimary">{mappingDrafts.length} รายการ</span>
                </div>
              </CardBlock>

              {error && (
                <div className="p-2.5 bg-red-50 border border-red-200 text-red-800 text-xs rounded-sm flex items-center gap-2 shrink-0">
                  <AlertCircle className="w-4 h-4 shrink-0 text-red-600" />
                  <span>{error}</span>
                </div>
              )}

              <div className="flex items-center justify-end gap-2 pt-1 shrink-0">
                <Button
                  variant="primary"
                  icon={Save}
                  onClick={handleCommit}
                  loading={saving}
                  disabled={!name || !slug}
                >
                  บันทึก
                </Button>
              </div>
            </>
          )}
        </div>

        {/* RIGHT — PDF preview */}
        <PdfPreviewPanel
          file={step === 1 ? file : null}
          payload={jsonPayload}
          onPreview={step === 2 ? handlePreview : undefined}
          className="lg:col-span-7 h-full"
          badge={file && (
            <Badge
              format={file.name.endsWith('.docx') ? 'docx' : file.name.endsWith('.xlsx') ? 'xlsx' : 'html'}
              size="sm"
            >
              {file.name}
            </Badge>
          )}
          emptySlot={
            !file ? (
              <>
                <FileText className="w-12 h-12 text-slate-200 mb-3" />
                <p className="text-sm font-medium text-textSecondary">เลือกไฟล์เพื่อดูตัวอย่าง</p>
                <p className="text-xs text-textMuted mt-1">Word · Excel · HTML</p>
              </>
            ) : step === 3 ? (
              <>
                <CheckCircle2 className="w-12 h-12 text-emerald-300 mb-3" />
                <p className="text-sm font-medium text-textSecondary">Mapping พร้อมแล้ว</p>
                <p className="text-xs text-textMuted mt-1">กด &quot;บันทึก&quot; เพื่อสร้าง Template</p>
              </>
            ) : (
              <>
                {getFileIcon(file.name)}
                <p className="text-xs text-red-500 mt-2">Preview ล้มเหลว</p>
              </>
            )
          }
        />

      </div>
    </div>
  );
};

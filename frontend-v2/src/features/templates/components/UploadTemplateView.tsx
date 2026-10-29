'use client';

import React, { useState, useRef, useCallback } from 'react';
import { FileText, FileSpreadsheet, CheckCircle2 } from 'lucide-react';
import { templatesApi } from '../api/templates.api';
import { ApiError } from '@/lib/api/client';
import { DocumentCategory, tokens } from '@/tokens';
import { Badge, PdfPreviewPanel } from '@/components/ui';
import { SaveFieldMappingItem } from '@/types/api';
import { buildMockJson } from './mockJsonBuilder';
import { UploadWizardHeader } from './upload/UploadWizardHeader';
import { UploadFileDropzoneStep } from './upload/UploadFileDropzoneStep';
import {
  UploadMappingConfigStep,
  MappingDraft,
} from './upload/UploadMappingConfigStep';
import { UploadConfirmStep } from './upload/UploadConfirmStep';

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

export interface UploadTemplateViewProps {
  onSuccess?: (templateId: string) => void;
  onCancel?: () => void;
}

export const UploadTemplateView: React.FC<UploadTemplateViewProps> = ({ onSuccess, onCancel }) => {
  const [step, setStep] = useState<1 | 2 | 3>(1);

  // Shared state
  const [file, setFile] = useState<File | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Step 1 state
  const [uploading, setUploading] = useState(false);

  // Step 2 state
  const [draftId, setDraftId] = useState<string | null>(null);
  const [mappingDrafts, setMappingDrafts] = useState<MappingDraft[]>([]);
  const [jsonPayload, setJsonPayload] = useState('{}');

  // Step 3 state
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
    if (!selectedFile) {
      setFile(null);
      return;
    }
    const valid = ['.docx', '.xlsx'].some((ext) => selectedFile.name.toLowerCase().endsWith(ext));
    if (!valid) {
      setError(
        'หน้านี้รองรับเฉพาะไฟล์ .docx (Word) และ .xlsx (Excel) เท่านั้น — หากต้องการเขียนโค้ดหรือนำเข้า .html กรุณาใช้ Template Studio'
      );
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

  const onDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  };
  const onDragLeave = () => setIsDragging(false);
  const onDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    const dropped = e.dataTransfer.files?.[0];
    if (!dropped) return;
    const valid = ['.docx', '.xlsx'].some((ext) => dropped.name.toLowerCase().endsWith(ext));
    if (!valid) {
      setError('รองรับเฉพาะ .docx .xlsx');
      return;
    }
    handleFileChange(dropped);
  };

  const handleParse = async () => {
    if (!file) {
      setError('ต้องเลือกไฟล์');
      return;
    }
    setUploading(true);
    setError(null);
    try {
      const { draftId: id, placeholders } = await templatesApi.parseDraft(file);
      setDraftId(id);
      const drafts: MappingDraft[] = placeholders.map((raw) => {
        const { sourcePath, transform, kind } = parsePlaceholder(raw);
        return {
          placeholder: raw,
          sourcePath,
          label: humanizeKey(sourcePath),
          required: false,
          transform,
          kind,
        };
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

  const handlePreview = useCallback(
    async (payload: string): Promise<Blob> => {
      if (!draftId) throw new Error('ยังไม่มี draft');
      return templatesApi.previewDraft(draftId, payload);
    },
    [draftId]
  );

  const updateDraft = (idx: number, field: keyof MappingDraft, value: unknown) => {
    setMappingDrafts((prev) =>
      prev.map((d, i) => (i === idx ? { ...d, [field]: value } : d))
    );
  };

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

  return (
    <div className="flex flex-col h-[calc(100vh-8rem)] min-h-[600px] gap-4">
      <UploadWizardHeader
        step={step}
        mappingCount={mappingDrafts.length}
        onStepBack={step > 1 ? () => setStep((step - 1) as 1 | 2 | 3) : undefined}
      />

      {/* Split layout */}
      <div className="flex-1 grid grid-cols-1 lg:grid-cols-12 gap-4 min-h-0 overflow-hidden">
        {/* Left step content */}
        <div className="lg:col-span-5 flex flex-col gap-4 overflow-y-auto scrollbar-hide pr-1 pb-4 min-h-0">
          {step === 1 && (
            <UploadFileDropzoneStep
              file={file}
              isDragging={isDragging}
              uploading={uploading}
              error={error}
              fileInputRef={fileInputRef}
              onDragOver={onDragOver}
              onDragLeave={onDragLeave}
              onDrop={onDrop}
              onFileChange={handleFileChange}
              onParse={handleParse}
              onCancel={onCancel}
              getFileIcon={getFileIcon}
            />
          )}

          {step === 2 && (
            <UploadMappingConfigStep
              mappingDrafts={mappingDrafts}
              jsonPayload={jsonPayload}
              error={error}
              onUpdateDraft={updateDraft}
              onJsonPayloadChange={setJsonPayload}
              onNext={() => {
                setError(null);
                setStep(3);
              }}
            />
          )}

          {step === 3 && (
            <UploadConfirmStep
              name={name}
              slug={slug}
              category={category}
              categories={categories}
              fileName={file?.name}
              mappingCount={mappingDrafts.length}
              saving={saving}
              error={error}
              onNameChange={(val) => {
                setName(val);
                if (!slugEdited) {
                  setSlug(val.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)+/g, ''));
                }
              }}
              onSlugChange={(val) => {
                setSlug(val.toLowerCase().replace(/[^a-z0-9-_]/g, ''));
                setSlugEdited(true);
              }}
              onCategoryChange={setCategory}
              onCommit={handleCommit}
            />
          )}
        </div>

        {/* Right PDF preview panel */}
        <PdfPreviewPanel
          file={step === 1 ? file : null}
          payload={jsonPayload}
          onPreview={step === 2 ? handlePreview : undefined}
          className="lg:col-span-7 h-full"
          badge={
            file && (
              <Badge
                format={
                  file.name.endsWith('.docx')
                    ? 'docx'
                    : file.name.endsWith('.xlsx')
                    ? 'xlsx'
                    : 'html'
                }
                size="sm"
              >
                {file.name}
              </Badge>
            )
          }
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

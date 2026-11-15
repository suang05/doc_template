'use client';

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import { CheckCircle2 } from 'lucide-react';
import { useTemplates } from '@/features/templates';
import { useDocumentGenerator } from '../hooks/useDocumentGenerator';
import { useGeneratorForm } from '../hooks/useGeneratorForm';
import { useFormPreview } from '../hooks/useFormPreview';
import { useAutoSelectFirst } from '@/hooks/useAutoSelectFirst';
import { ApiError } from '@/lib/api/client';
import type { OutputFormat } from '@/constants/domain';
import { Toast } from '@/components/ui';
import { GeneratorActionBar } from './GeneratorActionBar';
import { GeneratorSettingsCard } from './GeneratorSettingsCard';
import { GeneratorPayloadForm } from './GeneratorPayloadForm';
import { GeneratorResultCard } from './GeneratorResultCard';
import { GeneratorPreviewPanel } from './GeneratorPreviewPanel';

export interface GeneratorViewProps {
  initialSlug?: string;
}

function focusFieldByName(fieldName: string | null) {
  if (!fieldName || typeof document === 'undefined') return;
  const el = document.querySelector<HTMLElement>(
    `[name="${CSS.escape(fieldName)}"]`,
  );
  el?.focus();
}

export const GeneratorView: React.FC<GeneratorViewProps> = ({ initialSlug }) => {
  const { templates, loading: templatesLoading } = useTemplates();
  const {
    generate,
    enqueueBackground,
    loading: generating,
    enqueuing,
    error: generateError,
    result,
    queuedJob,
    resetResult,
  } = useDocumentGenerator();

  const [selectedSlug, setSelectedSlug] = useAutoSelectFirst(
    templates,
    (t) => t.slug,
    initialSlug,
  );
  const [outputFormat, setOutputFormat] = useState<OutputFormat>('pdf');
  const [documentRef, setDocumentRef] = useState('DOC-2026-0001');
  const [changeNote, setChangeNote] = useState('สร้างเอกสารฉบับจริง');
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(
    null,
  );

  const selectedTemplate = templates.find((t) => t.slug === selectedSlug);

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

  const {
    mappings,
    mappingsLoading,
    hasMappings,
    formValues,
    setField,
    fieldErrors,
    applyServerFieldErrors,
    fallbackJson,
    handleFallbackChange,
    jsonError,
    assembledData,
    validateForm,
    fillSampleData,
  } = useGeneratorForm(selectedTemplate?.id);

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
    const validation = validateForm();
    if (!validation.valid) {
      focusFieldByName(validation.firstErrorField);
      return;
    }

    try {
      await generate(selectedSlug, {
        payload: assembledData,
        outputFormat,
        documentRef: documentRef || undefined,
        changeNote: changeNote || undefined,
      });
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        const firstServerField = applyServerFieldErrors(err.fieldErrors);
        focusFieldByName(firstServerField);
      }
    }
  };

  const handleEnqueueBackground = async () => {
    if (!selectedSlug) {
      setToast({ type: 'error', message: 'กรุณาเลือกแม่แบบเอกสาร' });
      return;
    }
    const validation = validateForm();
    if (!validation.valid) {
      focusFieldByName(validation.firstErrorField);
      return;
    }

    try {
      const job = await enqueueBackground(selectedSlug, {
        payload: assembledData,
        outputFormat,
        documentRef: documentRef || undefined,
      });
      setToast({
        type: 'success',
        message: `ส่งเข้าคิว Pipeline C แล้ว (Job: ${job.jobId.slice(0, 8)})`,
      });
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        const firstServerField = applyServerFieldErrors(err.fieldErrors);
        focusFieldByName(firstServerField);
      }
    }
  };

  const handleCopy = (text: string) => {
    void navigator.clipboard.writeText(text).then(() => {
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

  return (
    <div className="flex flex-col h-full w-full min-h-0 gap-3">
      <GeneratorActionBar
        generating={generating}
        enqueuing={enqueuing}
        previewLoading={preview.loading}
        onRefreshPreview={preview.refresh}
        onGenerate={() => void handleGenerate()}
        onEnqueueBackground={() => void handleEnqueueBackground()}
      />

      <div className="flex-1 grid grid-cols-1 lg:grid-cols-12 gap-4 min-h-0 overflow-hidden">
        <div className="lg:col-span-5 flex flex-col gap-3 overflow-y-auto pr-1 pb-4 custom-scrollbar">
          {queuedJob && (
            <div className="p-3 bg-sky-50 border border-sky-200 rounded-sm text-xs text-sky-900 flex items-center justify-between gap-2 shrink-0">
              <div className="flex items-center gap-2">
                <CheckCircle2 className="w-4 h-4 text-sky-600 shrink-0" />
                <div>
                  <p className="font-semibold">ส่งคำสั่งเข้าคิว Pipeline C แล้ว</p>
                  <p className="font-mono text-[11px] text-sky-700">
                    Job ID: {queuedJob.jobId} ({queuedJob.status})
                  </p>
                </div>
              </div>
              <Link href="/jobs" className="text-primary font-semibold hover:underline shrink-0">
                ติดตามงาน &rarr;
              </Link>
            </div>
          )}

          <GeneratorResultCard result={result} error={generateError} onCopy={handleCopy} />

          <GeneratorSettingsCard
            selectedTemplate={selectedTemplate}
            selectedSlug={selectedSlug}
            templateOptions={templateOptions}
            templatesLoading={templatesLoading}
            outputFormat={outputFormat}
            formatOptions={formatOptions}
            documentRef={documentRef}
            changeNote={changeNote}
            onTemplateChange={handleTemplateChange}
            onOutputFormatChange={setOutputFormat}
            onDocumentRefChange={setDocumentRef}
            onChangeNoteChange={setChangeNote}
          />

          <GeneratorPayloadForm
            mappingsLoading={mappingsLoading}
            hasMappings={hasMappings}
            mappings={mappings}
            formValues={formValues}
            fieldErrors={fieldErrors}
            fallbackJson={fallbackJson}
            jsonError={jsonError}
            onFillSampleData={fillSampleData}
            onFieldChange={setField}
            onFallbackJsonChange={handleFallbackChange}
          />
        </div>

        <GeneratorPreviewPanel
          pdfUrl={preview.pdfUrl}
          loading={preview.loading}
          error={preview.error}
          errorDetails={preview.errorDetails}
          renderLatencyMs={preview.renderLatencyMs}
          contentSizeBytes={preview.contentSizeBytes}
          onRefresh={preview.refresh}
        />
      </div>

      {toast && <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} />}
    </div>
  );
};

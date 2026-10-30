'use client';

import React, { useState, useEffect } from 'react';
import { useTemplates } from '@/features/templates';
import { useDocumentGenerator } from '../hooks/useDocumentGenerator';
import { useGeneratorForm } from '../hooks/useGeneratorForm';
import { useFormPreview } from '../hooks/useFormPreview';
import { useAutoSelectFirst } from '@/hooks/useAutoSelectFirst';
import { OutputFormat } from '@/types/api';
import { Toast } from '@/components/ui';
import { GeneratorActionBar } from './GeneratorActionBar';
import { GeneratorSettingsCard } from './GeneratorSettingsCard';
import { GeneratorPayloadForm } from './GeneratorPayloadForm';
import { GeneratorResultCard } from './GeneratorResultCard';
import { GeneratorPreviewPanel } from './GeneratorPreviewPanel';

export interface GeneratorViewProps {
  initialSlug?: string;
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
      // error handled by generateError state
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

  return (
    <div className="flex flex-col h-full w-full min-h-0 gap-3">
      {/* Top action bar */}
      <GeneratorActionBar
        generating={generating}
        previewLoading={preview.loading}
        onRefreshPreview={preview.refresh}
        onGenerate={handleGenerate}
      />

      {/* Main split content */}
      <div className="flex-1 grid grid-cols-1 lg:grid-cols-12 gap-4 min-h-0 overflow-hidden">
        {/* Left: Config + Data form */}
        <div className="lg:col-span-5 flex flex-col gap-3 overflow-y-auto pr-1 pb-4 custom-scrollbar">
          <GeneratorResultCard
            result={result}
            error={generateError}
            onCopy={handleCopy}
          />

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

        {/* Right: Live PDF Preview */}
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

      {/* Toast notifications */}
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

'use client';

import React, { useState, useEffect, useRef } from 'react';
import { CheckCircle, AlertTriangle } from 'lucide-react';
import { useTemplateStudio, parseCombinedHtml } from '../hooks/useTemplateStudio';
import { useLivePreview } from '../hooks/useLivePreview';
import { extractVariablesFromHtml } from '../domain/variableExtractor';
import { registerTemplateCompletion } from '../domain/templateCompletion';
import { StudioHeader } from './StudioHeader';
import { StudioEditorTabs, StudioTabKey } from './StudioEditorTabs';
import { StudioPreviewSection } from './StudioPreviewSection';
import { StudioSaveModal } from './StudioSaveModal';
import { StudioHistoryModal } from './StudioHistoryModal';

export interface TemplateStudioViewProps {
  templateId: string;
  onBack: () => void;
}

export const TemplateStudioView: React.FC<TemplateStudioViewProps> = ({
  templateId,
  onBack,
}) => {
  useEffect(() => {
    if (!templateId) onBack();
  }, [templateId, onBack]);

  const {
    template,
    html,
    mainHtml,
    headerHtml,
    footerHtml,
    updateMainHtml,
    updateHeaderHtml,
    updateFooterHtml,
    isDirty,
    loading,
    saving,
    validating,
    versions,
    validationResult,
    persistedSamplePayload,
    saveHtml,
    validate,
  } = useTemplateStudio(templateId);

  const [sampleDataJson, setSampleDataJson] = useState('{}');
  const [leftTab, setLeftTab] = useState<StudioTabKey>('main');
  const [isHistoryOpen, setIsHistoryOpen] = useState(false);
  const [changeNote, setChangeNote] = useState('');
  const [isSaveModalOpen, setIsSaveModalOpen] = useState(false);
  const [importNotice, setImportNotice] = useState<string | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const editorRef = useRef<any>(null);
  const monacoRef = useRef<any>(null);
  const completionDisposableRef = useRef<any>(null);

  const handleFileImport = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (event) => {
      const content = event.target?.result as string;
      if (!content) return;
      const { main, header, footer } = parseCombinedHtml(content);
      updateMainHtml(main);
      updateHeaderHtml(header);
      updateFooterHtml(footer);
      if (header || footer) {
        setImportNotice('แยก Header / Footer ออกจากไฟล์อัตโนมัติ — ตรวจสอบใน Tab "Header & Footer"');
        setTimeout(() => setImportNotice(null), 5000);
      }
    };
    reader.readAsText(file);
    e.target.value = '';
  };

  const handleEditorMount = (editor: any, monaco: any) => {
    editorRef.current = editor;
    monacoRef.current = monaco;

    if (!completionDisposableRef.current) {
      completionDisposableRef.current = registerTemplateCompletion(monaco, {
        getAvailableFields: () => validationResult?.fields || [],
      });
    }
  };

  useEffect(() => {
    return () => {
      if (completionDisposableRef.current) {
        completionDisposableRef.current.dispose();
        completionDisposableRef.current = null;
      }
    };
  }, []);

  const insertSnippet = (snippetText: string) => {
    if (!editorRef.current) return;
    const editor = editorRef.current;
    const selection = editor.getSelection();
    if (!selection) return;

    editor.executeEdits('snippet-insert', [
      {
        range: selection,
        text: snippetText,
        forceMoveMarkers: true,
      },
    ]);
    editor.focus();
  };

  // Auto-extract Handlebars variables and array loops from HTML into sample data
  useEffect(() => {
    if (!html) return;
    try {
      const current = JSON.parse(sampleDataJson) as Record<string, unknown>;
      const next = extractVariablesFromHtml(html, current);
      if (next !== current) setSampleDataJson(JSON.stringify(next, null, 2));
    } catch {
      // skip until sampleDataJson is valid JSON
    }
  }, [html, sampleDataJson]);

  // Synchronize persisted sample payload from server when loaded
  useEffect(() => {
    if (persistedSamplePayload && persistedSamplePayload.trim() !== '' && persistedSamplePayload.trim() !== '{}') {
      try {
        JSON.parse(persistedSamplePayload);
        setSampleDataJson(persistedSamplePayload);
      } catch {
        // ignore invalid JSON
      }
    }
  }, [persistedSamplePayload]);

  const {
    pdfUrl,
    loading: previewLoading,
    error: previewError,
    errorDetails: previewErrorDetails,
    renderLatencyMs,
    contentSizeBytes,
    refreshPreview,
  } = useLivePreview(
    template?.slug,
    html,
    sampleDataJson
  );

  const handleSave = async () => {
    try {
      await saveHtml(changeNote, sampleDataJson);
      setIsSaveModalOpen(false);
      setChangeNote('');
      refreshPreview();
    } catch {
      // Error handled by hook
    }
  };

  if (loading) {
    return (
      <div className="p-12 text-center text-xs text-textMuted flex items-center justify-center gap-2">
        <span className="w-4 h-4 border-2 border-primary border-t-transparent rounded-full animate-spin" />
        <span>กำลังโหลดสตูดิโอ...</span>
      </div>
    );
  }

  const currentVersion = versions.length > 0 ? Math.max(...versions.map((v) => v.version)) : 0;

  return (
    <div className="flex flex-col h-full w-full min-h-0 border border-border rounded-sm bg-surface overflow-hidden shadow-sm">
      {/* Studio Header Bar */}
      <StudioHeader
        templateName={template?.name}
        isDirty={isDirty}
        currentVersion={currentVersion}
        versionsCount={versions.length}
        validating={validating}
        fileInputRef={fileInputRef}
        onBack={onBack}
        onFileImport={handleFileImport}
        onOpenHistory={() => setIsHistoryOpen(true)}
        onValidate={validate}
        onOpenSave={() => setIsSaveModalOpen(true)}
      />

      {/* Validation alert banner if present */}
      {validationResult && (
        <div
          className={`px-3 py-1.5 border-b text-xs flex items-center justify-between ${
            validationResult.valid
              ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
              : 'bg-red-50 border-red-200 text-red-800'
          }`}
        >
          <div className="flex items-center gap-2">
            {validationResult.valid ? (
              <CheckCircle className="w-5 h-5 text-success" />
            ) : (
              <AlertTriangle className="w-3.5 h-3.5 text-red-600" />
            )}
            <span>
              {validationResult.valid
                ? `โครงสร้าง HTML ถูกต้องตามมาตรฐาน (พบตัวแปร ${validationResult.fields.length} ฟิลด์)`
                : `พบข้อผิดพลาด: ${validationResult.errors.join(', ')}`}
            </span>
          </div>

          {validationResult.fields.length > 0 && (
            <div className="flex flex-wrap gap-2 mt-2">
              {validationResult.fields.slice(0, 5).map((f: string) => (
                <span key={f} className="px-1 py-0.2 bg-surface text-[10px] font-mono border rounded-xs">
                  {f}
                </span>
              ))}
              {validationResult.fields.length > 5 && (
                <span className="text-[10px] text-textMuted">+{validationResult.fields.length - 5}</span>
              )}
            </div>
          )}
        </div>
      )}

      {/* Import notice banner */}
      {importNotice && (
        <div className="px-3 py-1.5 bg-sky-50 border-b border-sky-200 text-xs text-sky-800 flex items-center justify-between shrink-0">
          <span>📂 {importNotice}</span>
          <button
            onClick={() => setImportNotice(null)}
            className="text-sky-500 hover:text-sky-700 ml-3 font-bold leading-none cursor-pointer"
          >
            ×
          </button>
        </div>
      )}

      {/* Split-Screen: Left Input Tabs (50%), Right Live PDF Preview (50%) */}
      <div className="flex-1 flex flex-col md:flex-row overflow-hidden">
        <StudioEditorTabs
          leftTab={leftTab}
          onTabChange={setLeftTab}
          mainHtml={mainHtml}
          headerHtml={headerHtml}
          footerHtml={footerHtml}
          sampleDataJson={sampleDataJson}
          onUpdateMainHtml={updateMainHtml}
          onUpdateHeaderHtml={updateHeaderHtml}
          onUpdateFooterHtml={updateFooterHtml}
          onUpdateSampleDataJson={setSampleDataJson}
          onInsertSnippet={insertSnippet}
          onEditorMount={handleEditorMount}
        />

        <StudioPreviewSection
          pdfUrl={pdfUrl}
          previewLoading={previewLoading}
          previewError={previewError}
          previewErrorDetails={previewErrorDetails}
          renderLatencyMs={renderLatencyMs}
          contentSizeBytes={contentSizeBytes}
          hasTemplateSlug={Boolean(template?.slug)}
          onRefreshPreview={refreshPreview}
        />
      </div>

      {/* Save Modal */}
      <StudioSaveModal
        isOpen={isSaveModalOpen}
        nextVersion={currentVersion + 1}
        changeNote={changeNote}
        saving={saving}
        onChangeNote={setChangeNote}
        onConfirm={handleSave}
        onClose={() => setIsSaveModalOpen(false)}
      />

      {/* Revisions History Modal */}
      <StudioHistoryModal
        isOpen={isHistoryOpen}
        versions={versions}
        onClose={() => setIsHistoryOpen(false)}
      />
    </div>
  );
};

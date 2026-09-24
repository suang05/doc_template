'use client';

import React, { useState } from 'react';
import dynamic from 'next/dynamic';
import {
  Save,
  CheckCircle,
  AlertTriangle,
  History,
  ArrowLeft,
  Eye,
  RefreshCw,
  Upload,
} from 'lucide-react';
import { useTemplateStudio } from '@/hooks/useTemplateStudio';
import { useLivePreview } from '@/hooks/useLivePreview';
import { Button, Modal } from '@/components/ui';
import { registerTemplateCompletion } from '@/lib/monaco/templateCompletion';

// Dynamically import Monaco Editor to avoid SSR issues
const Editor = dynamic(() => import('@monaco-editor/react'), { ssr: false });

export interface TemplateStudioViewProps {
  templateId: string;
  onBack: () => void;
}

export const TemplateStudioView: React.FC<TemplateStudioViewProps> = ({
  templateId,
  onBack,
}) => {
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
  const [leftTab, setLeftTab] = useState<'main' | 'headerFooter' | 'payload'>('main');
  const [isHistoryOpen, setIsHistoryOpen] = useState(false);
  const [changeNote, setChangeNote] = useState('');
  const [isSaveModalOpen, setIsSaveModalOpen] = useState(false);

  const fileInputRef = React.useRef<HTMLInputElement>(null);
  const editorRef = React.useRef<any>(null);
  const monacoRef = React.useRef<any>(null);
  const completionDisposableRef = React.useRef<any>(null);

  const handleFileImport = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (event) => {
      const content = event.target?.result as string;
      if (content) {
        updateMainHtml(content);
      }
    };
    reader.readAsText(file);
    e.target.value = '';
  };

  const handleEditorMount = (editor: any, monaco: any) => {
    editorRef.current = editor;
    monacoRef.current = monaco;

    // Register template completion once
    if (!completionDisposableRef.current) {
      completionDisposableRef.current = registerTemplateCompletion(monaco, {
        getAvailableFields: () => validationResult?.fields || [],
      });
    }
  };

  React.useEffect(() => {
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

  // Auto-extract Handlebars variables and array loops from HTML
  React.useEffect(() => {
    if (!html) return;
    
    try {
      const currentData = JSON.parse(sampleDataJson);
      let updated = false;

      // 1. Detect arrays from {{#each items}}
      const eachRegex = /{{#each\s+([a-zA-Z0-9_.]+)\s*}}([\s\S]*?){{\/each}}/g;
      let eachMatch: RegExpExecArray | null;
      const arrayKeys = new Set<string>();

      while ((eachMatch = eachRegex.exec(html)) !== null) {
        const arrayKey = eachMatch[1];
        const innerContent = eachMatch[2];
        arrayKeys.add(arrayKey);

        if (!Array.isArray(currentData[arrayKey]) || currentData[arrayKey].length === 0) {
          const innerFieldRegex = /{{\s*([a-zA-Z0-9_.]+)(?::\w+)?\s*}}/g;
          let innerFieldMatch: RegExpExecArray | null;
          const innerItem: Record<string, any> = {};

          while ((innerFieldMatch = innerFieldRegex.exec(innerContent)) !== null) {
            const f = innerFieldMatch[1];
            if (['addOne', 'inc', '@index', 'this', 'else', 'if'].includes(f)) continue;
            innerItem[f] = f.toLowerCase().includes('amount') || f.toLowerCase().includes('price')
              ? 1500.0
              : f.toLowerCase().includes('no') ? '1' : `Sample ${f}`;
          }

          if (Object.keys(innerItem).length === 0) {
            innerItem['name'] = 'ตัวอย่างรายการที่ 1';
          }

          const innerItem2 = { ...innerItem };
          if (innerItem2['no']) innerItem2['no'] = '2';
          if (typeof innerItem2['amount'] === 'number') innerItem2['amount'] = 2500.0;

          currentData[arrayKey] = [innerItem, innerItem2];
          updated = true;
        }
      }

      // 2. Detect plain variables & nested objects (e.g. customer.name)
      const varRegex = /{{\s*(?:qr:|barcode:|image:)?([a-zA-Z0-9_.]+)(?::\w+)?\s*}}/g;
      let varMatch: RegExpExecArray | null;
      while ((varMatch = varRegex.exec(html)) !== null) {
        const v = varMatch[1];
        if (v.startsWith('#') || v.startsWith('/') || v.startsWith('@') || v.startsWith('^')) continue;
        if (['addOne', 'inc', 'else', 'this', 'if', 'each', 'ifEquals'].includes(v)) continue;
        if (arrayKeys.has(v)) continue;

        if (v.includes('.')) {
          const parts = v.split('.');
          const objKey = parts[0];
          const fieldKey = parts.slice(1).join('.');

          if (!currentData[objKey] || typeof currentData[objKey] !== 'object' || Array.isArray(currentData[objKey])) {
            currentData[objKey] = {};
          }

          if (currentData[objKey][fieldKey] === undefined) {
            const lk = fieldKey.toLowerCase();
            if (lk.includes('tax_id') || lk.includes('citizen_id') || lk.includes('idcard')) {
              currentData[objKey][fieldKey] = '0107536000123';
            } else if (lk.includes('email')) {
              currentData[objKey][fieldKey] = 'contact@sammakorn.co.th';
            } else if (lk.includes('amount') || lk.includes('total') || lk.includes('price')) {
              currentData[objKey][fieldKey] = 250000.0;
            } else if (lk.includes('date')) {
              currentData[objKey][fieldKey] = '2026-09-20';
            } else {
              currentData[objKey][fieldKey] = `ตัวอย่าง ${fieldKey}`;
            }
            updated = true;
          }
        } else {
          if (currentData[v] === undefined) {
            const lk = v.toLowerCase();
            if (lk.includes('tax_id') || lk.includes('citizen_id') || lk.includes('idcard')) {
              currentData[v] = '0107536000123';
            } else if (lk.includes('email')) {
              currentData[v] = 'contact@sammakorn.co.th';
            } else if (lk.includes('amount') || lk.includes('total') || lk.includes('price')) {
              currentData[v] = 250000.0;
            } else if (lk.includes('date')) {
              currentData[v] = '2026-09-20';
            } else {
              currentData[v] = `ตัวอย่าง ${v}`;
            }
            updated = true;
          }
        }
      }

      if (updated) {
        setSampleDataJson(JSON.stringify(currentData, null, 2));
      }
    } catch {
      // If parsing fails, skip until valid JSON
    }
  }, [html]);

  // Synchronize persisted sample payload from server when loaded
  React.useEffect(() => {
    if (persistedSamplePayload && persistedSamplePayload.trim() !== '' && persistedSamplePayload.trim() !== '{}') {
      try {
        JSON.parse(persistedSamplePayload); // validate JSON
        setSampleDataJson(persistedSamplePayload);
      } catch {
        // ignore invalid JSON
      }
    }
  }, [persistedSamplePayload]);

  // Live PDF preview hook with debouncing
  const { pdfUrl, loading: previewLoading, error: previewError, refreshPreview } = useLivePreview(
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

  return (
    <div className="flex flex-col h-[calc(100vh-6rem)] border border-border rounded-sm bg-surface overflow-hidden shadow-sm">
      {/* Studio Header Bar */}
      <div className="h-11 bg-surfaceSubtle border-b border-border px-3 flex items-center justify-between shrink-0">
        <div className="flex items-center gap-2">
          <button
            onClick={onBack}
            className="p-1 rounded-sm text-textSecondary hover:text-textPrimary hover:bg-slate-200/50 transition-colors cursor-pointer"
            title="กลับหน้ารายการ"
          >
            <ArrowLeft className="w-4 h-4" />
          </button>

          <span className="text-xs font-bold text-textPrimary flex items-center gap-1">
            {template?.name || 'สตูดิโอแม่แบบ (ยังไม่บันทึก)'}
            {isDirty && <span className="text-amber-500 text-sm leading-none" title="มีการแก้ไขที่ยังไม่บันทึก">*</span>}
          </span>

          <span className="text-[11px] font-mono text-textMuted bg-surface px-1.5 py-0.5 rounded-sm border border-border">
            v{versions.length > 0 ? Math.max(...versions.map(v => v.version)) : 0}
          </span>
        </div>

        <div className="flex items-center gap-2">
          <input
            type="file"
            ref={fileInputRef}
            accept=".html,.htm"
            className="hidden"
            onChange={handleFileImport}
          />

          <Button
            variant="outline"
            size="sm"
            icon={Upload}
            onClick={() => fileInputRef.current?.click()}
            title="เลือกไฟล์ .html จากเครื่องแล้วใส่ลง Monaco Editor"
          >
            นำเข้า HTML
          </Button>

          <Button
            variant="ghost"
            size="sm"
            icon={History}
            onClick={() => setIsHistoryOpen(true)}
          >
            ประวัติ (v{versions.length})
          </Button>

          <Button
            variant="secondary"
            size="sm"
            icon={CheckCircle}
            loading={validating}
            onClick={validate}
          >
            ตรวจสอบโค้ด
          </Button>

          <Button
            variant="primary"
            size="sm"
            icon={Save}
            disabled={!isDirty}
            onClick={() => setIsSaveModalOpen(true)}
          >
            บันทึก v{(versions.length > 0 ? Math.max(...versions.map(v => v.version)) : 0) + 1}
          </Button>
        </div>
      </div>

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

      {/* Split-Screen: Left Input (50%), Right Live PDF Preview (50%) */}
      <div className="flex-1 flex flex-col md:flex-row overflow-hidden">
        
        {/* Left: Input Tabs (50%) */}
        <div className="w-full md:w-1/2 flex flex-col border-r border-border h-full bg-surface min-w-0">
          {/* Tab Header */}
          <div className="h-9 bg-surfaceSubtle border-b border-border px-3 flex items-center justify-between shrink-0">
            <div className="flex items-center gap-1 h-full pt-2">
              <button
                onClick={() => setLeftTab('main')}
                className={`px-4 h-full flex items-center text-[11px] font-semibold rounded-t-sm transition-colors ${
                  leftTab === 'main' 
                    ? 'bg-surface text-primary border-t border-l border-r border-border border-b-2 border-b-primary -mb-px' 
                    : 'text-textMuted hover:text-textPrimary border-b-2 border-b-transparent -mb-px'
                }`}
              >
                Main HTML
              </button>
              <button
                onClick={() => setLeftTab('headerFooter')}
                className={`px-4 h-full flex items-center text-[11px] font-semibold rounded-t-sm transition-colors ${
                  leftTab === 'headerFooter' 
                    ? 'bg-surface text-primary border-t border-l border-r border-border border-b-2 border-b-primary -mb-px' 
                    : 'text-textMuted hover:text-textPrimary border-b-2 border-b-transparent -mb-px'
                }`}
              >
                Header & Footer
              </button>
              <button
                onClick={() => setLeftTab('payload')}
                className={`px-4 h-full flex items-center text-[11px] font-semibold rounded-t-sm transition-colors ${
                  leftTab === 'payload' 
                    ? 'bg-surface text-primary border-t border-l border-r border-border border-b-2 border-b-primary -mb-px' 
                    : 'text-textMuted hover:text-textPrimary border-b-2 border-b-transparent -mb-px'
                }`}
              >
                JSON Payload
              </button>
            </div>
            {leftTab !== 'payload' && (
              <span className="text-[10px] text-textMuted">UTF-8 • Sarabun Font</span>
            )}
            {leftTab === 'payload' && (
              <span className="text-[10px] bg-slate-100 text-textMuted px-1.5 py-0.5 rounded-[2px]">Auto-extracted</span>
            )}
          </div>

          {/* Tab Content */}
          <div className="flex-1 relative flex flex-col overflow-hidden bg-zinc-900">
            {/* Quick Insert Snippet Bar for HTML templates */}
            {leftTab !== 'payload' && (
              <div className="bg-zinc-950 border-b border-zinc-800 px-3 py-1.5 flex items-center gap-1.5 overflow-x-auto shrink-0 scrollbar-thin">
                <span className="text-zinc-500 font-mono text-[10px] uppercase shrink-0 mr-1">แทรกด่วน:</span>
                <button
                  type="button"
                  onClick={() => insertSnippet('{{#each items}}\n<tr>\n  <td>{{addOne @index}}</td>\n  <td>{{name}}</td>\n  <td style="text-align: right;">{{amount:number}}</td>\n</tr>\n{{/each}}')}
                  className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
                  title="แทรกบล็อกวนลูปตารางรายการ"
                >
                  <span className="text-indigo-400 font-bold">+</span> ตารางรายการ (each)
                </button>
                <button
                  type="button"
                  onClick={() => insertSnippet('{{#if condition}}\n  <!-- เมื่อเป็นจริง -->\n{{else}}\n  <!-- เมื่อเป็นเท็จ -->\n{{/if}}')}
                  className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
                  title="แทรกบล็อกเงื่อนไข True/False"
                >
                  <span className="text-sky-400 font-bold">+</span> เงื่อนไข (if)
                </button>
                <button
                  type="button"
                  onClick={() => insertSnippet('{{#ifEquals status "APPROVED"}}\n  <span>อนุมัติแล้ว</span>\n{{else}}\n  <span>รออนุมัติ</span>\n{{/ifEquals}}')}
                  className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
                  title="ตรวจสอบค่าเท่ากัน"
                >
                  <span className="text-sky-400 font-bold">+</span> เช็คค่า (ifEquals)
                </button>
                <button
                  type="button"
                  onClick={() => insertSnippet('{{addOne @index}}')}
                  className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
                  title="ลำดับแถวเริ่มต้นที่ 1"
                >
                  <span className="text-emerald-400 font-bold">+</span> ลำดับ (1,2,3..)
                </button>
                <button
                  type="button"
                  onClick={() => insertSnippet('<div style="page-break-after: always;"></div>')}
                  className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
                  title="แบ่งหน้าขึ้นหน้าใหม่ A4"
                >
                  <span className="text-amber-400 font-bold">+</span> แบ่งหน้า A4
                </button>
                <button
                  type="button"
                  onClick={() => insertSnippet('{{qr:trackingUrl}}')}
                  className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
                  title="แทรกรูป QR Code ขนาด 150x150"
                >
                  <span className="text-purple-400 font-bold">+</span> QR Code
                </button>
                <button
                  type="button"
                  onClick={() => insertSnippet('{{barcode:code}}')}
                  className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
                  title="แทรกรูป Barcode Code128"
                >
                  <span className="text-purple-400 font-bold">+</span> Barcode
                </button>
                <button
                  type="button"
                  onClick={() => insertSnippet(':thai_baht_text}}')}
                  className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
                  title="แปลงตัวเลขเป็นตัวหนังสือบาทไทย"
                >
                  <span className="text-emerald-400 font-bold">:</span> ตัวหนังสือบาท
                </button>
                <button
                  type="button"
                  onClick={() => insertSnippet(':number}}')}
                  className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
                  title="จัดฟอร์แมตตัวเลขมีคอมม่าทศนิยม"
                >
                  <span className="text-emerald-400 font-bold">:</span> ตัวเลขคอมม่า
                </button>
                <button
                  type="button"
                  onClick={() => insertSnippet(':thai_date}}')}
                  className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
                  title="จัดรูปแบบวันที่ไทย พ.ศ."
                >
                  <span className="text-amber-400 font-bold">:</span> วันที่ไทย
                </button>
              </div>
            )}

            {leftTab === 'main' && (
              <Editor
                height="100%"
                defaultLanguage="html"
                theme="vs-dark"
                value={mainHtml}
                onChange={(val) => { updateMainHtml(val || ''); }}
                onMount={handleEditorMount}
                path="main"
                options={{
                  minimap: { enabled: false },
                  fontSize: 12,
                  fontFamily: "'Fira Code', monospace",
                  lineNumbers: 'on',
                  wordWrap: 'on',
                  automaticLayout: true,
                }}
              />
            )}
            
            {leftTab === 'headerFooter' && (
              <div className="flex-1 flex flex-col">
                <div className="flex-1 flex flex-col border-b border-zinc-800">
                  <div className="text-[10px] font-mono text-zinc-500 bg-zinc-900 px-3 py-1 border-b border-zinc-800">Header HTML (Top)</div>
                  <div className="flex-1 relative">
                    <Editor
                      height="100%"
                      defaultLanguage="html"
                      theme="vs-dark"
                      value={headerHtml}
                      onChange={(val) => { updateHeaderHtml(val || ''); }}
                      onMount={handleEditorMount}
                      path="header"
                      options={{
                        minimap: { enabled: false },
                        fontSize: 12,
                        fontFamily: "'Fira Code', monospace",
                        lineNumbers: 'on',
                        wordWrap: 'on',
                        automaticLayout: true,
                      }}
                    />
                  </div>
                </div>
                <div className="flex-1 flex flex-col">
                  <div className="text-[10px] font-mono text-zinc-500 bg-zinc-900 px-3 py-1 border-b border-zinc-800">Footer HTML (Bottom)</div>
                  <div className="flex-1 relative">
                    <Editor
                      height="100%"
                      defaultLanguage="html"
                      theme="vs-dark"
                      value={footerHtml}
                      onChange={(val) => { updateFooterHtml(val || ''); }}
                      onMount={handleEditorMount}
                      path="footer"
                      options={{
                        minimap: { enabled: false },
                        fontSize: 12,
                        fontFamily: "'Fira Code', monospace",
                        lineNumbers: 'on',
                        wordWrap: 'on',
                        automaticLayout: true,
                      }}
                    />
                  </div>
                </div>
              </div>
            )}
            
            {leftTab === 'payload' && (
              <Editor
                height="100%"
                defaultLanguage="json"
                theme="vs-dark"
                value={sampleDataJson}
                onChange={(v) => setSampleDataJson(v || '{}')}
                path="payload"
                options={{
                  minimap: { enabled: false },
                  fontSize: 12,
                  fontFamily: "'Fira Code', monospace",
                  lineNumbers: 'on',
                  wordWrap: 'on',
                  automaticLayout: true,
                }}
              />
            )}
          </div>
        </div>

        {/* Right: Live PDF Preview Panel (50%) */}
        <div className="w-full md:w-1/2 flex flex-col h-full bg-slate-100 min-w-0">
          <div className="h-9 bg-surfaceSubtle border-b border-border px-3 flex items-center justify-between shrink-0">
            <div className="flex items-center gap-2">
              <span className="text-[11px] font-semibold text-textSecondary flex items-center gap-1.5">
                <Eye className="w-3.5 h-3.5 text-primary" />
                Live PDF Preview (Gotenberg)
              </span>
              {previewLoading && (
                <span className="w-3 h-3 border-2 border-primary border-t-transparent rounded-full animate-spin" />
              )}
            </div>

            <div className="flex items-center gap-2">
              {pdfUrl && (
                <a
                  href={pdfUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="px-2 py-0.5 bg-primary/10 text-primary hover:bg-primary/20 rounded-[2px] text-[10px] font-semibold transition-colors flex items-center gap-1"
                >
                  เปิด PDF ใหม่
                </a>
              )}
              <button
                onClick={refreshPreview}
                className="p-1 rounded-[2px] text-textMuted hover:bg-slate-200 transition-colors cursor-pointer"
                title="รีเฟรช Preview"
              >
                <RefreshCw className={`w-3.5 h-3.5 ${previewLoading ? 'animate-spin text-primary' : ''}`} />
              </button>
            </div>
          </div>

          <div className="flex-1 relative flex flex-col overflow-hidden items-center justify-center">
            {previewLoading && !pdfUrl ? (
              <div className="flex flex-col items-center justify-center text-xs text-textMuted gap-3">
                <span className="w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin" />
                <span>กำลังจำลองผล PDF (Gotenberg)...</span>
              </div>
            ) : previewError ? (
              <div className="bg-white border border-border p-6 rounded-sm text-center text-xs text-red-600 space-y-2 max-w-sm shadow-sm">
                <AlertTriangle className="w-8 h-8 mx-auto text-red-500" />
                <p className="font-medium">เกิดข้อผิดพลาดในการสร้างพรีวิว</p>
                <p className="text-[11px] text-textMuted">{previewError}</p>
              </div>
            ) : pdfUrl ? (
              <div className="w-full h-full bg-white shadow-sm flex flex-col">
                <iframe
                  src={`${pdfUrl}#toolbar=0`}
                  className="w-full h-full border-none"
                  title="PDF Live Preview"
                />
              </div>
            ) : (
              <div className="text-xs text-textMuted flex flex-col items-center gap-2">
                {!template?.slug ? (
                  <>
                    <Eye className="w-6 h-6 text-border" />
                    <p>กรุณาบันทึกแม่แบบก่อนเพื่อเปิดใช้งาน Live Preview</p>
                  </>
                ) : (
                  <p>กำลังเตรียมแสดงผล...</p>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Save Modal */}
      <Modal
        isOpen={isSaveModalOpen}
        onClose={() => setIsSaveModalOpen(false)}
        title={`ยืนยันการบันทึกแม่แบบ v${(versions.length > 0 ? Math.max(...versions.map(v => v.version)) : 0) + 1}`}
        footer={
          <>
            <Button variant="ghost" onClick={() => setIsSaveModalOpen(false)}>
              ยกเลิก
            </Button>
            <Button variant="primary" onClick={handleSave} loading={saving}>
              บันทึกเวอร์ชันใหม่
            </Button>
          </>
        }
      >
        <div className="space-y-2">
          <p className="text-xs text-textSecondary">
            ระบบจะสร้างเวอร์ชันใหม่ (v{(versions.length > 0 ? Math.max(...versions.map(v => v.version)) : 0) + 1}) และจัดเก็บประวัติการแก้ไขเดิมไว้ใน MinIO
          </p>
          <label className="block text-xs font-medium text-textPrimary">บันทึกข้อความสรุปการแก้ไข (Change Note):</label>
          <input
            type="text"
            className="w-full h-8 px-2.5 text-xs rounded-sm border border-border bg-surface outline-none focus:border-primary"
            placeholder="เช่น ปรับตำแหน่งลายเซ็น, แก้ไขรูปแบบวันที่..."
            value={changeNote}
            onChange={(e) => setChangeNote(e.target.value)}
          />
        </div>
      </Modal>

      {/* Revisions History Modal */}
      <Modal
        isOpen={isHistoryOpen}
        onClose={() => setIsHistoryOpen(false)}
        title="ประวัติการแก้ไขแม่แบบ (Revision History)"
        maxWidth="lg"
      >
        <div className="space-y-2">
          {versions.length === 0 ? (
            <p className="text-xs text-textMuted text-center py-4">ไม่พบประวัติเวอร์ชันเดิม</p>
          ) : (
            <div className="divide-y divide-border border border-border rounded-sm">
              {versions.map((v: any) => (
                <div key={v.id} className="p-2.5 flex items-center justify-between text-xs hover:bg-slate-50">
                  <div className="space-y-0.5">
                    <div className="flex items-center gap-2">
                      <span className="font-bold text-textPrimary">v{v.version}</span>
                      <span className="text-textMuted text-[11px]">{new Date(v.createdAt).toLocaleString('th-TH')}</span>
                    </div>
                    <p className="text-textSecondary text-[11px]">{v.commitMessage || 'ไม่มีรายละเอียด'}</p>
                  </div>
                  <span className="text-[10px] font-mono text-textMuted bg-surfaceSubtle px-1.5 py-0.5 border rounded-xs">
                    {v.storageKey.split('/').pop()}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      </Modal>
    </div>
  );
};

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Upload, RefreshCw, Play, Download, Clock, FileCheck2 } from 'lucide-react';
import { api } from '@/lib/api-client';
import { PreviewPanel } from '@/components/features/preview/PreviewPanel';
import { JsonEditor } from './JsonEditor';
import { FormEditor } from './FormEditor';
import { FormatBadge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { cn } from '@/utils/cn';
import type { TemplateDetail, TemplateVariable } from '@/types/api';

interface TemplateStudioViewProps {
  apiKey: string;
  onToast: (message: string, type?: 'success' | 'error' | 'info') => void;
}

type InputMode = 'form' | 'json';

function getFormat(t: TemplateDetail): string {
  const ext = t.fileName.split('.').pop()?.toLowerCase();
  if (ext === 'docx') return 'docx';
  if (ext === 'xlsx') return 'xlsx';
  if (ext === 'html' || t.format === 'html') return 'html';
  if (ext === 'json') return 'json';
  return ext ?? '';
}

function dataToJson(data: Record<string, unknown>): string {
  return JSON.stringify(data, null, 2);
}

const AUTO_REFRESH_DELAY = 1500; // ms

export function TemplateStudioView({ apiKey, onToast }: TemplateStudioViewProps) {
  const [templates, setTemplates] = useState<TemplateDetail[]>([]);
  const [selected, setSelected] = useState('');
  const [selectedDetail, setSelectedDetail] = useState<TemplateDetail | null>(null);
  const [variables, setVariables] = useState<TemplateVariable[]>([]);
  const [mode, setMode] = useState<InputMode>('form');
  const [jsonText, setJsonText] = useState('{\n  "replace": {}\n}');
  const [jsonError, setJsonError] = useState<string | null>(null);
  const [formData, setFormData] = useState<Record<string, unknown>>({ replace: {} });
  const [previewData, setPreviewData] = useState<Record<string, unknown>>({ replace: {} });
  const [showPreview, setShowPreview] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(false);
  const [renderMs, setRenderMs] = useState<number | null>(null);
  const [loadingTemplates, setLoadingTemplates] = useState(false);
  const [loadingSchema, setLoadingSchema] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const autoRefreshTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => { if (apiKey) api.setApiKey(apiKey); }, [apiKey]);

  // ── Load template list ──────────────────────────────────────────────────────
  const loadTemplates = useCallback(async () => {
    setLoadingTemplates(true);
    try {
      const res = await api.getTemplates();
      setTemplates(res.details);
      if (res.details.length > 0 && !selected) {
        setSelected(res.details[0].fileName);
      }
    } catch {
      onToast('ไม่สามารถโหลดรายการ template ได้', 'error');
    } finally {
      setLoadingTemplates(false);
    }
  }, [onToast, selected]);

  useEffect(() => { loadTemplates(); }, [loadTemplates]);

  // ── Load schema when template changes ───────────────────────────────────────
  useEffect(() => {
    if (!selected) { setVariables([]); return; }
    const detail = templates.find(t => t.fileName === selected) ?? null;
    setSelectedDetail(detail);
    setShowPreview(false);
    setRenderMs(null);
    setLoadingSchema(true);

    api.getTemplateSchema(selected)
      .then(res => {
        setVariables(res.variables ?? []);
        if (res.sampleData) {
          const json = dataToJson(res.sampleData);
          setJsonText(json);
          setFormData(res.sampleData as Record<string, unknown>);
          setJsonError(null);
        }
      })
      .catch(() => setVariables([]))
      .finally(() => setLoadingSchema(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selected]);

  // ── JSON editor change ───────────────────────────────────────────────────────
  const handleJsonChange = (val: string) => {
    setJsonText(val);
    try {
      const parsed = JSON.parse(val);
      setJsonError(null);
      setFormData(parsed);
      triggerAutoRefresh(parsed);
    } catch (e) {
      setJsonError(e instanceof Error ? e.message : 'JSON ไม่ถูกต้อง');
    }
  };

  // ── Form editor change ───────────────────────────────────────────────────────
  const handleFormChange = (data: Record<string, unknown>) => {
    setFormData(data);
    setJsonText(dataToJson(data));
    setJsonError(null);
    triggerAutoRefresh(data);
  };

  // ── Auto-refresh debounce ────────────────────────────────────────────────────
  const triggerAutoRefresh = (data: Record<string, unknown>) => {
    if (!autoRefresh || !selected) return;
    if (autoRefreshTimer.current) clearTimeout(autoRefreshTimer.current);
    autoRefreshTimer.current = setTimeout(() => {
      setPreviewData(data);
      setShowPreview(true);
    }, AUTO_REFRESH_DELAY);
  };

  // ── Manual preview ───────────────────────────────────────────────────────────
  const handlePreview = () => {
    if (!selected) { onToast('เลือก template ก่อน', 'error'); return; }
    if (jsonError) { onToast('JSON ไม่ถูกต้อง', 'error'); return; }
    setPreviewData(formData);
    setShowPreview(true);
  };

  // ── Download PDF ─────────────────────────────────────────────────────────────
  const handleDownload = async () => {
    if (!selected || jsonError) return;
    setDownloading(true);
    try {
      const blob = await api.previewDocument({ templateName: selected, outputFormat: 'pdf', data: formData });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = selected.replace(/\.[^.]+$/, '') + '.pdf';
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      onToast(e instanceof Error ? e.message : 'Download ล้มเหลว', 'error');
    } finally {
      setDownloading(false);
    }
  };

  // ── Upload template ──────────────────────────────────────────────────────────
  const handleUpload = async (file: File) => {
    const ext = file.name.split('.').pop()?.toLowerCase();
    if (!['xlsx', 'docx'].includes(ext ?? '')) {
      onToast('รองรับ .docx และ .xlsx เท่านั้น (HTML/JSON ใช้ Template Editor)', 'error');
      return;
    }
    setUploading(true);
    try {
      await api.uploadTemplate(file, false);
      onToast(`อัปโหลด ${file.name} สำเร็จ`, 'success');
      await loadTemplates();
      setSelected(file.name);
    } catch (e) {
      onToast(e instanceof Error ? e.message : 'อัปโหลดล้มเหลว', 'error');
    } finally {
      setUploading(false);
    }
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    const file = e.dataTransfer.files[0];
    if (file) handleUpload(file);
  };

  // ── Prettify JSON ─────────────────────────────────────────────────────────────
  const handlePrettify = () => {
    try { setJsonText(dataToJson(JSON.parse(jsonText))); } catch {}
  };

  // ── Reset to sample ───────────────────────────────────────────────────────────
  const handleResetSample = async () => {
    if (!selected) return;
    try {
      const res = await api.getTemplateSchema(selected);
      if (res.sampleData) {
        const json = dataToJson(res.sampleData);
        setJsonText(json);
        setFormData(res.sampleData as Record<string, unknown>);
        setJsonError(null);
      }
    } catch {}
  };

  const fmt = selectedDetail ? getFormat(selectedDetail) : null;

  return (
    <div className="flex h-[calc(100vh-var(--topbar-h))] overflow-hidden gap-0">

      {/* ── Left panel (380px) ───────────────────────────────────────────────── */}
      <div className="w-[380px] flex-none flex flex-col border-r border-[var(--border)] bg-[var(--surf)]">

        {/* Template selector */}
        <div className="flex items-center gap-2 px-3 py-2.5 border-b border-[var(--border)] bg-[var(--sep)]">
          <svg width="13" height="13" viewBox="0 0 16 16" fill="none" className="flex-none text-[var(--t3)]">
            <path d="M3 2h7l3 3v9a1 1 0 01-1 1H3a1 1 0 01-1-1V3a1 1 0 011-1z" stroke="currentColor" strokeWidth="1.5"/>
            <path d="M10 2v3h3" stroke="currentColor" strokeWidth="1.5"/>
          </svg>
          <div className="relative flex-1 min-w-0">
            <select
              value={selected}
              onChange={e => setSelected(e.target.value)}
              disabled={loadingTemplates}
              className="w-full appearance-none bg-[var(--surf)] border border-[var(--border)] rounded-[var(--ri)] pl-2 pr-6 py-1.5 text-t-sm text-[var(--t1)] focus:outline-none focus:border-[var(--blue)] focus:ring-2 focus:ring-[var(--blue-t)] transition"
            >
              {templates.length === 0 && <option value="">ไม่มี template</option>}
              {templates.map(t => (
                <option key={t.fileName} value={t.fileName}>{t.fileName}</option>
              ))}
            </select>
            <span className="pointer-events-none absolute right-2 inset-y-0 flex items-center text-[var(--t3)] text-t-xs">▾</span>
          </div>
          {fmt && <FormatBadge format={fmt} />}
        </div>

        {/* Upload zone */}
        <input ref={fileInputRef} type="file" accept=".xlsx,.docx" className="hidden"
          onChange={e => { const f = e.target.files?.[0]; if (f) handleUpload(f); e.target.value = ''; }} />
        <div
          onClick={() => fileInputRef.current?.click()}
          onDrop={handleDrop}
          onDragOver={e => e.preventDefault()}
          className={cn(
            "mx-3 my-3 flex-none border-2 border-dashed rounded-[var(--r)] px-4 py-4 flex flex-col items-center gap-2 cursor-pointer transition-colors",
            uploading
              ? "border-[var(--blue)] bg-[var(--blue-t)]"
              : "border-[var(--border)] hover:border-[var(--navy)] hover:bg-[var(--blue-t)]"
          )}
        >
          <div className={cn(
            "w-9 h-9 rounded-full flex items-center justify-center transition-colors",
            uploading ? "bg-[var(--blue-t)] text-[var(--navy)]" : "bg-[var(--sep)] text-[var(--t3)]"
          )}>
            {uploading
              ? <RefreshCw className="w-4 h-4 animate-spin" />
              : <Upload className="w-4 h-4" />
            }
          </div>
          <div className="text-center">
            <p className="text-t-sm font-semibold text-[var(--t1)]">
              {uploading ? 'กำลังอัปโหลด...' : 'ลากไฟล์มาวาง หรือ คลิกเพื่อเลือก'}
            </p>
            <p className="text-t-xs text-[var(--t3)] mt-0.5">เพิ่ม template ใหม่หรืออัปเดตเวอร์ชัน</p>
          </div>
          <div className="flex items-center gap-1.5 flex-wrap justify-center">
            {['DOCX', 'XLSX'].map(ext => (
              <span key={ext} className="text-[10px] font-mono font-semibold px-1.5 py-0.5 rounded border border-[var(--border)] text-[var(--t3)] bg-[var(--surf)]">{ext}</span>
            ))}
          </div>
        </div>

        {/* Divider + section label */}
        <div className="border-t border-[var(--border)] mx-0" />
        <div className="flex items-center justify-between px-3 pt-2.5 pb-1">
          <span className="text-t-xs font-bold text-[var(--t3)] uppercase tracking-[.5px]">กรอกข้อมูลทดสอบ</span>
          <div className="flex items-center gap-1">
            {mode === 'json' && (
              <button onClick={handlePrettify}
                className="text-[10px] px-2 py-0.5 rounded border border-[var(--border)] text-[var(--t3)] hover:text-[var(--t1)] transition-colors">
                จัดรูปแบบ
              </button>
            )}
            <button onClick={handleResetSample}
              className="text-[10px] px-2 py-0.5 rounded border border-[var(--border)] text-[var(--t3)] hover:text-[var(--t1)] transition-colors">
              รีเซ็ต
            </button>
            {mode === 'json' && jsonError && (
              <span className="text-[10px] text-[var(--rose)] font-semibold">✗</span>
            )}
            {mode === 'json' && !jsonError && (
              <span className="text-[10px] text-[var(--emerald)] font-semibold">✓</span>
            )}
          </div>
        </div>

        {/* Mode toggle */}
        <div className="flex items-center gap-0.5 bg-[var(--sep)] rounded-[var(--rp)] p-0.5 mx-3 mb-2">
          {(['form', 'json'] as InputMode[]).map(m => (
            <button key={m} onClick={() => setMode(m)}
              className={cn(
                "flex-1 flex items-center justify-center gap-1 px-2.5 py-1 rounded-[var(--rp)] text-t-xs font-medium transition-colors",
                mode === m
                  ? "bg-[var(--surf)] text-[var(--navy)] font-semibold shadow-sm"
                  : "text-[var(--t3)] hover:text-[var(--t1)]"
              )}>
              {m === 'form' ? 'ฟอร์ม' : '{ } JSON'}
            </button>
          ))}
        </div>

        {/* Editor area */}
        <div className="flex-1 min-h-0 overflow-hidden px-0">
          {loadingSchema ? (
            <div className="flex items-center justify-center h-full text-[var(--t3)] text-t-xs gap-2">
              <RefreshCw className="w-3.5 h-3.5 animate-spin" /> กำลังโหลด schema…
            </div>
          ) : mode === 'form' ? (
            <FormEditor variables={variables} onChange={handleFormChange} initialData={formData} />
          ) : (
            <JsonEditor value={jsonText} onChange={handleJsonChange} placeholder='{ "replace": { "variable": "value" } }' />
          )}
        </div>

        {/* Bottom bar */}
        <div className="flex items-center gap-2 px-3 py-2.5 border-t border-[var(--border)] bg-[var(--sep)] flex-none">
          {/* Auto-refresh toggle */}
          <button onClick={() => setAutoRefresh(p => !p)}
            className="flex items-center gap-1.5 text-t-xs text-[var(--t2)] hover:text-[var(--t1)] transition-colors">
            <div className={cn(
              "w-7 h-3.5 rounded-full relative flex-none transition-colors",
              autoRefresh ? "bg-[var(--navy)]" : "bg-[var(--border)]"
            )}>
              <div className={cn(
                "absolute top-0.5 w-2.5 h-2.5 rounded-full bg-white shadow transition-transform",
                autoRefresh ? "translate-x-3.5" : "translate-x-0.5"
              )} />
            </div>
            Auto
          </button>

          <div className="flex-1" />

          <Button size="sm" variant="outline" onClick={handleDownload}
            disabled={!selected || downloading || !!jsonError}>
            {downloading ? <RefreshCw className="w-3.5 h-3.5 animate-spin" /> : <Download className="w-3.5 h-3.5" />}
            PDF
          </Button>

          <Button size="sm" variant="navy" onClick={handlePreview}
            disabled={!selected || !!jsonError}>
            <Play className="w-3.5 h-3.5" />
            Preview
          </Button>
        </div>
      </div>

      {/* ── Right panel: Preview ─────────────────────────────────────────────── */}
      <div className="flex-1 min-w-0 flex flex-col bg-[var(--bg)]">
        {/* Preview header */}
        <div className="flex items-center justify-between px-4 py-2.5 border-b border-[var(--border)] bg-[var(--surf)] flex-none">
          <span className="text-t-sm font-semibold text-[var(--t1)]">ตัวอย่างเอกสาร</span>
          <div className="flex items-center gap-2">
            {renderMs !== null && (
              <span className="flex items-center gap-1 text-t-xs text-[var(--emerald)] font-medium px-2 py-0.5 rounded-full bg-[var(--emerald-t)]">
                <Clock className="w-3 h-3" /> {renderMs} ms
              </span>
            )}
            {showPreview && (
              <button onClick={handlePreview} disabled={!selected || !!jsonError}
                className="w-6 h-6 flex items-center justify-center rounded text-[var(--t3)] hover:text-[var(--t1)] hover:bg-[var(--sep)] transition-colors">
                <RefreshCw className="w-3.5 h-3.5" />
              </button>
            )}
          </div>
        </div>

        {/* Preview body */}
        <div className="flex-1 min-h-0">
          {!showPreview ? (
            <div className="flex flex-col items-center justify-center h-full gap-4 text-center p-8">
              <div className="w-14 h-14 rounded-xl bg-[var(--sep)] flex items-center justify-center text-[var(--t3)]">
                <FileCheck2 className="w-6 h-6" />
              </div>
              <div>
                <p className="text-t-sm font-semibold text-[var(--t1)]">กรอกข้อมูลแล้วกด Preview</p>
                <p className="text-t-xs text-[var(--t3)] mt-1">
                  {autoRefresh
                    ? `Auto-refresh เปิดอยู่ — จะรีเฟรชอัตโนมัติใน ${AUTO_REFRESH_DELAY / 1000} วินาที`
                    : 'ระบบจะแสดง PDF preview ที่ฝั่งขวา'
                  }
                </p>
              </div>
              <Button size="sm" variant="navy" onClick={handlePreview} disabled={!selected || !!jsonError}>
                <Play className="w-3.5 h-3.5" /> Preview
              </Button>
            </div>
          ) : (
            <div className="h-full">
              <RenderTimedPreview
                templateName={selected}
                data={previewData}
                onRenderMs={setRenderMs}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ── Timed wrapper around PreviewPanel ─────────────────────────────────────────
function RenderTimedPreview({
  templateName,
  data,
  onRenderMs,
}: {
  templateName: string;
  data: Record<string, unknown>;
  onRenderMs: (ms: number) => void;
}) {
  const startRef = useRef(Date.now());

  useEffect(() => {
    startRef.current = Date.now();
  }, [templateName, data]);

  return (
    <PreviewPanel
      templateName={templateName}
      data={data}
      outputFormat="pdf"
      onRendered={() => onRenderMs(Date.now() - startRef.current)}
    />
  );
}

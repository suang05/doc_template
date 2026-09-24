'use client';

import { useState, useRef, useCallback, useEffect } from 'react';
import dynamic from 'next/dynamic';
import {
  ArrowDownToLine,
  Braces,
  Check,
  Clock,
  Code,
  Columns,
  Eye,
  FileCode,
  FileText,
  LayoutTemplate,
  RefreshCw,
} from 'lucide-react';
import { TiptapEditor } from '@/components/features/tiptap/TiptapEditor';
import { api } from '@/lib/api-client';

const MonacoEditor = dynamic(() => import('@monaco-editor/react'), {
  ssr: false,
  loading: () => (
    <div className="h-full w-full flex items-center justify-center bg-[#1e1e1e] text-slate-400 text-xs">
      <div className="flex items-center gap-2">
        <div className="w-4 h-4 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
        <span>กำลังโหลด Monaco Editor...</span>
      </div>
    </div>
  ),
});

// ── Default content ──────────────────────────────────────────────────────────
const DEFAULT_FULL_HTML = `<!DOCTYPE html>
<html lang="th">
<head>
<meta charset="UTF-8" />
<style>
  @import url('https://fonts.googleapis.com/css2?family=Sarabun:wght@400;600;700&display=swap');
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    font-family: 'Sarabun', sans-serif;
    font-size: 14px;
    color: #1e293b;
    padding: 40px 48px;
    line-height: 1.7;
  }
  h1 { font-size: 20px; font-weight: 700; margin-bottom: 4px; }
  .sub { color: #64748b; font-size: 12px; margin-bottom: 32px; }
  table { width: 100%; border-collapse: collapse; margin-top: 16px; }
  th { background: #1e40af; color: #fff; text-align: left; padding: 8px 12px; font-size: 13px; }
  td { padding: 8px 12px; border-bottom: 1px solid #e2e8f0; font-size: 13px; }
  tr:nth-child(even) td { background: #f8fafc; }
  .total { text-align: right; font-weight: 700; padding: 12px; }
</style>
</head>
<body>
  <h1>ใบเสนอราคา / Quotation</h1>
  <p class="sub">เลขที่: QT-2026-0001 &nbsp;|&nbsp; วันที่: 15 กันยายน 2569</p>
  <table>
    <thead>
      <tr><th>#</th><th>รายการ</th><th>จำนวน</th><th>ราคาต่อหน่วย</th><th>รวม</th></tr>
    </thead>
    <tbody>
      <tr><td>1</td><td>บริการออกแบบระบบ</td><td>1</td><td>50,000</td><td>50,000</td></tr>
      <tr><td>2</td><td>พัฒนา Backend API</td><td>3</td><td>30,000</td><td>90,000</td></tr>
      <tr><td>3</td><td>พัฒนา Frontend</td><td>2</td><td>25,000</td><td>50,000</td></tr>
    </tbody>
    <tfoot>
      <tr><td colspan="4" class="total">รวมทั้งสิ้น</td><td><strong>190,000 บาท</strong></td></tr>
    </tfoot>
  </table>
</body>
</html>`;

const DEFAULT_HEADER = `<html><head><style>
  body { font-family: 'Sarabun', sans-serif; font-size: 10px; color: #64748b; margin: 0; padding: 6px 48px; display: flex; justify-content: space-between; }
</style></head>
<body>
  <span>SAMMAKORN GROUP</span>
  <span>เอกสารลับ — ห้ามเผยแพร่</span>
</body></html>`;

const DEFAULT_FOOTER = `<html><head><style>
  body { font-family: 'Sarabun', sans-serif; font-size: 10px; color: #94a3b8; margin: 0; padding: 6px 48px; display: flex; justify-content: space-between; }
</style></head>
<body>
  <span>สร้างโดย SMK Document Service</span>
  <span>หน้า <span class="pageNumber"></span> / <span class="totalPages"></span></span>
</body></html>`;

// ── Helpers ──────────────────────────────────────────────────────────────────
function extractBody(fullHtml: string): string {
  const m = fullHtml.match(/<body[^>]*>([\s\S]*?)<\/body>/i);
  return m ? m[1].trim() : fullHtml;
}

function assembleFullHtml(bodyHtml: string, existingFull: string): string {
  if (!existingFull.includes('<body')) return bodyHtml;
  return existingFull.replace(/<body[^>]*>[\s\S]*?<\/body>/i, `<body>\n${bodyHtml}\n</body>`);
}

// ── Props ────────────────────────────────────────────────────────────────────
interface Props {
  apiKey: string;
  onToast?: (msg: string, type?: 'success' | 'error' | 'info') => void;
}

export function HtmlToPdfView({ onToast }: Props) {
  // Editor mode
  const [editorType, setEditorType]   = useState<'visual' | 'code'>('visual');
  const [monacoTab, setMonacoTab]     = useState<'template' | 'data' | 'headerFooter'>('template');
  const [viewMode, setViewMode]       = useState<'split' | 'editor' | 'preview'>('split');

  // Content
  const [fullHtml, setFullHtml]         = useState(DEFAULT_FULL_HTML);
  const [bodyHtml, setBodyHtml]         = useState(() => extractBody(DEFAULT_FULL_HTML));
  const [headerHtml, setHeaderHtml]     = useState(DEFAULT_HEADER);
  const [footerHtml, setFooterHtml]     = useState(DEFAULT_FOOTER);
  const [dataJson, setDataJson]         = useState('{\n  "companyName": "SAMMAKORN GROUP",\n  "docNo": "QT-2026-0001"\n}');
  const [parsedData, setParsedData]     = useState<Record<string, string>>({ companyName: 'SAMMAKORN GROUP', docNo: 'QT-2026-0001' });
  const [jsonError, setJsonError]       = useState<string | null>(null);

  // Preview state
  const [pdfBlobUrl, setPdfBlobUrl]   = useState<string | null>(null);
  const [isRendering, setIsRendering] = useState(false);
  const [durationMs, setDurationMs]   = useState<number | null>(null);
  const [renderError, setRenderError] = useState<string | null>(null);
  const [isDownloading, setIsDownloading] = useState(false);
  const [downloadOk, setDownloadOk]   = useState(false);

  const debounceRef = useRef<NodeJS.Timeout | null>(null);
  const blobUrlRef  = useRef<string | null>(null);

  useEffect(() => { blobUrlRef.current = pdfBlobUrl; }, [pdfBlobUrl]);
  useEffect(() => () => {
    if (blobUrlRef.current) URL.revokeObjectURL(blobUrlRef.current);
    if (debounceRef.current) clearTimeout(debounceRef.current);
  }, []);

  // Replace {{variable}} in HTML with data values before sending to Gotenberg
  const interpolate = (html: string, data: Record<string, string>) =>
    html.replace(/\{\{(\w+)\}\}/g, (_, key) => data[key] ?? `{{${key}}}`);

  const renderPdf = useCallback(async (html: string, hdr: string, ftr: string, data?: Record<string, string>) => {
    if (!html.trim()) return;
    setIsRendering(true);
    setRenderError(null);
    try {
      const resolved = data ? interpolate(html, data) : html;
      const { blob, durationMs: ms } = await api.convertHtmlToPdf(resolved, hdr, ftr);
      const url = URL.createObjectURL(blob);
      if (blobUrlRef.current) URL.revokeObjectURL(blobUrlRef.current);
      setPdfBlobUrl(url);
      setDurationMs(ms);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'เกิดข้อผิดพลาด';
      setRenderError(msg);
    } finally {
      setIsRendering(false);
    }
  }, []);

  const scheduleRender = useCallback((html: string, hdr: string, ftr: string, data?: Record<string, string>) => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => renderPdf(html, hdr, ftr, data), 400);
  }, [renderPdf]);

  // Initial render
  useEffect(() => { renderPdf(DEFAULT_FULL_HTML, DEFAULT_HEADER, DEFAULT_FOOTER, parsedData); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // ── Editor handlers ────────────────────────────────────────────────────────
  const handleTipTapChange = (newBody: string) => {
    setBodyHtml(newBody);
    const updated = assembleFullHtml(newBody, fullHtml);
    setFullHtml(updated);
    scheduleRender(updated, headerHtml, footerHtml, parsedData);
  };

  const handleMonacoHtmlChange = (value: string | undefined) => {
    if (value === undefined) return;
    setFullHtml(value);
    setBodyHtml(extractBody(value));
    scheduleRender(value, headerHtml, footerHtml, parsedData);
  };

  const handleDataJsonChange = (value: string | undefined) => {
    if (value === undefined) return;
    setDataJson(value);
    try {
      const parsed = JSON.parse(value);
      setParsedData(parsed);
      setJsonError(null);
      scheduleRender(fullHtml, headerHtml, footerHtml, parsed);
    } catch {
      setJsonError('JSON ไม่ถูกต้อง');
    }
  };

  const handleHeaderChange = (value: string | undefined) => {
    if (value === undefined) return;
    setHeaderHtml(value);
    scheduleRender(fullHtml, value, footerHtml, parsedData);
  };

  const handleFooterChange = (value: string | undefined) => {
    if (value === undefined) return;
    setFooterHtml(value);
    scheduleRender(fullHtml, headerHtml, value, parsedData);
  };

  const handleSwitchEditor = (next: 'visual' | 'code') => {
    if (next === editorType) return;
    if (next === 'code') {
      setFullHtml(assembleFullHtml(bodyHtml, fullHtml));
    } else {
      setBodyHtml(extractBody(fullHtml));
    }
    setEditorType(next);
  };

  const handleDownload = async () => {
    setIsDownloading(true);
    try {
      const resolved = interpolate(fullHtml, parsedData);
      const { blob } = await api.convertHtmlToPdf(resolved, headerHtml, footerHtml);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `html-export-${Date.now()}.pdf`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      setDownloadOk(true);
      setTimeout(() => setDownloadOk(false), 2500);
      onToast?.('ดาวน์โหลด PDF สำเร็จ', 'success');
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'เกิดข้อผิดพลาด';
      onToast?.(`ดาวน์โหลดล้มเหลว: ${msg}`, 'error');
    } finally {
      setIsDownloading(false);
    }
  };

  // ── Render ─────────────────────────────────────────────────────────────────
  return (
    <div className="flex flex-col h-full bg-slate-100">

      {/* ── Top Header ── */}
      <header className="shrink-0 bg-white border-b border-slate-200 px-4 py-2.5 flex flex-wrap items-center justify-between gap-3">

        {/* Brand */}
        <div className="flex items-center gap-2">
          <FileText className="w-4 h-4 text-blue-600" />
          <span className="font-bold text-slate-900 text-sm">HTML → PDF Studio</span>
          <span className="px-2 py-0.5 text-xs rounded bg-slate-100 text-slate-600 font-mono hidden sm:inline">
            Gotenberg Chromium
          </span>
        </div>

        {/* Editor mode toggle */}
        <div className="flex items-center bg-slate-100 p-1 rounded-lg border border-slate-200 text-xs font-medium">
          <button
            type="button"
            onClick={() => handleSwitchEditor('visual')}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md transition-all ${
              editorType === 'visual'
                ? 'bg-white text-blue-700 shadow-sm font-semibold'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <FileText className="w-3.5 h-3.5 text-blue-600" />
            โหมดวิชวล (TipTap)
          </button>
          <button
            type="button"
            onClick={() => handleSwitchEditor('code')}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md transition-all ${
              editorType === 'code'
                ? 'bg-[#1e1e1e] text-white shadow-sm font-semibold'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <Code className="w-3.5 h-3.5 text-emerald-400" />
            โหมดโค้ด (Monaco)
          </button>
        </div>

        {/* View mode */}
        <div className="hidden md:flex items-center bg-slate-100 p-1 rounded-lg border border-slate-200 text-xs font-medium text-slate-600">
          {([
            { id: 'split',   icon: Columns,   label: 'แบ่งครึ่ง' },
            { id: 'editor',  icon: FileCode,  label: 'ตัวแก้ไข' },
            { id: 'preview', icon: Eye,       label: 'PDF' },
          ] as const).map(({ id, icon: Icon, label }) => (
            <button
              key={id}
              type="button"
              onClick={() => setViewMode(id)}
              className={`flex items-center gap-1 px-2.5 py-1.5 rounded-md transition-colors ${
                viewMode === id
                  ? 'bg-white text-blue-600 shadow-sm font-semibold'
                  : 'hover:text-slate-900'
              }`}
            >
              <Icon className="w-3.5 h-3.5" />
              {label}
            </button>
          ))}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2">
          {durationMs !== null && !isRendering && (
            <span className="hidden sm:flex items-center gap-1 px-2 py-1 bg-emerald-50 border border-emerald-200 rounded text-xs font-mono text-emerald-700">
              <Clock className="w-3.5 h-3.5" />
              {durationMs}ms
            </span>
          )}
          <button
            type="button"
            onClick={handleDownload}
            disabled={isDownloading || !pdfBlobUrl}
            className="inline-flex items-center gap-1.5 px-4 py-1.5 bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition-colors disabled:opacity-50 cursor-pointer"
          >
            {isDownloading ? (
              <div className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
            ) : downloadOk ? (
              <Check className="w-3.5 h-3.5 text-emerald-300" />
            ) : (
              <ArrowDownToLine className="w-3.5 h-3.5" />
            )}
            {isDownloading ? 'กำลังดาวน์โหลด...' : downloadOk ? 'ดาวน์โหลดแล้ว!' : 'ดาวน์โหลด PDF'}
          </button>
        </div>
      </header>

      {/* ── Main body ── */}
      <div className="flex-1 flex overflow-hidden">

        {/* LEFT — Editor pane */}
        {(viewMode === 'split' || viewMode === 'editor') && (
          <div className={`flex flex-col overflow-hidden bg-slate-50 ${
            viewMode === 'split' ? 'w-1/2 border-r border-slate-200' : 'w-full'
          }`}>

            {/* Visual — TipTap */}
            {editorType === 'visual' && (
              <div className="flex-1 flex flex-col p-3 overflow-hidden">
                <div className="flex items-center gap-2 mb-2 px-1">
                  <span className="text-xs font-bold text-slate-800 flex items-center gap-1.5">
                    <FileText className="w-4 h-4 text-blue-600" />
                    Visual WYSIWYG Editor (TipTap)
                  </span>
                  <span className="text-[10px] bg-blue-50 text-blue-700 px-2 py-0.5 rounded-full border border-blue-200 font-medium">
                    สำหรับผู้ใช้ทั่วไป
                  </span>
                </div>
                <div className="flex-1 overflow-hidden">
                  <TiptapEditor
                    value={bodyHtml}
                    onChange={handleTipTapChange}
                    placeholder="พิมพ์เนื้อหาเอกสาร..."
                    className="h-full"
                  />
                </div>
              </div>
            )}

            {/* Code — Monaco */}
            {editorType === 'code' && (
              <div className="flex-1 flex flex-col overflow-hidden bg-[#1e1e1e]">
                {/* Monaco tab bar */}
                <div className="flex items-center justify-between bg-[#252526] border-b border-[#333] px-3 py-1.5 text-xs select-none">
                  <div className="flex items-center gap-1">
                    <button
                      type="button"
                      onClick={() => setMonacoTab('template')}
                      className={`flex items-center gap-1.5 px-3 py-1.5 rounded text-xs transition-colors ${
                        monacoTab === 'template'
                          ? 'bg-[#1e1e1e] text-white font-semibold border-t-2 border-blue-500'
                          : 'text-slate-400 hover:text-slate-200 hover:bg-[#2d2d2d]'
                      }`}
                    >
                      <Code className="w-3.5 h-3.5 text-blue-400" />
                      &lt;/&gt; HTML + CSS
                    </button>
                    <button
                      type="button"
                      onClick={() => setMonacoTab('data')}
                      className={`flex items-center gap-1.5 px-3 py-1.5 rounded text-xs transition-colors ${
                        monacoTab === 'data'
                          ? 'bg-[#1e1e1e] text-white font-semibold border-t-2 border-emerald-500'
                          : 'text-slate-400 hover:text-slate-200 hover:bg-[#2d2d2d]'
                      }`}
                    >
                      <Braces className="w-3.5 h-3.5 text-emerald-400" />
                      &#123;&#125; JSON Data
                      {jsonError && <span className="w-2 h-2 rounded-full bg-red-500 ml-1" />}
                    </button>
                    <button
                      type="button"
                      onClick={() => setMonacoTab('headerFooter')}
                      className={`flex items-center gap-1.5 px-3 py-1.5 rounded text-xs transition-colors ${
                        monacoTab === 'headerFooter'
                          ? 'bg-[#1e1e1e] text-white font-semibold border-t-2 border-indigo-500'
                          : 'text-slate-400 hover:text-slate-200 hover:bg-[#2d2d2d]'
                      }`}
                    >
                      <LayoutTemplate className="w-3.5 h-3.5 text-indigo-400" />
                      Header &amp; Footer
                    </button>
                  </div>
                  <span className="text-[10px] text-slate-500 font-mono">
                    {monacoTab === 'template' ? 'HTML / Sarabun A4' : monacoTab === 'data' ? 'RFC 8259 JSON — แทนค่า {{variable}}' : 'Chromium header/footer.html'}
                  </span>
                </div>

                {/* Monaco content */}
                <div className="flex-1 overflow-hidden">
                  {monacoTab === 'template' && (
                    <MonacoEditor
                      height="100%"
                      language="html"
                      theme="vs-dark"
                      value={fullHtml}
                      onChange={handleMonacoHtmlChange}
                      options={{
                        minimap: { enabled: false },
                        fontSize: 13,
                        wordWrap: 'on',
                        scrollBeyondLastLine: false,
                        automaticLayout: true,
                        tabSize: 2,
                        padding: { top: 12, bottom: 12 },
                      }}
                    />
                  )}

                  {monacoTab === 'data' && (
                    <MonacoEditor
                      height="100%"
                      language="json"
                      theme="vs-dark"
                      value={dataJson}
                      onChange={handleDataJsonChange}
                      options={{
                        minimap: { enabled: false },
                        fontSize: 13,
                        wordWrap: 'on',
                        scrollBeyondLastLine: false,
                        automaticLayout: true,
                        tabSize: 2,
                        padding: { top: 12, bottom: 12 },
                      }}
                    />
                  )}

                  {monacoTab === 'headerFooter' && (
                    <div className="h-full flex flex-col divide-y divide-[#333]">
                      <div className="flex-1 flex flex-col">
                        <div className="bg-[#252526] px-3 py-1 text-[11px] font-semibold text-slate-300 flex items-center justify-between">
                          <span>Header HTML (แนะนำความสูง 2.5cm)</span>
                          <span className="text-[10px] text-slate-500 font-mono">header.html</span>
                        </div>
                        <div className="flex-1">
                          <MonacoEditor
                            height="100%"
                            language="html"
                            theme="vs-dark"
                            value={headerHtml}
                            onChange={handleHeaderChange}
                            options={{ minimap: { enabled: false }, fontSize: 12, wordWrap: 'on', automaticLayout: true }}
                          />
                        </div>
                      </div>
                      <div className="flex-1 flex flex-col">
                        <div className="bg-[#252526] px-3 py-1 text-[11px] font-semibold text-slate-300 flex items-center justify-between">
                          <span>Footer HTML (แนะนำความสูง 2.0cm)</span>
                          <span className="text-[10px] text-slate-500 font-mono">footer.html</span>
                        </div>
                        <div className="flex-1">
                          <MonacoEditor
                            height="100%"
                            language="html"
                            theme="vs-dark"
                            value={footerHtml}
                            onChange={handleFooterChange}
                            options={{ minimap: { enabled: false }, fontSize: 12, wordWrap: 'on', automaticLayout: true }}
                          />
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        )}

        {/* RIGHT — PDF Preview pane */}
        {(viewMode === 'split' || viewMode === 'preview') && (
          <div className={`flex flex-col p-3 bg-slate-100 overflow-hidden ${
            viewMode === 'split' ? 'w-1/2' : 'w-full'
          }`}>
            {/* Preview toolbar */}
            <div className="flex items-center justify-between mb-2 px-1 gap-2">
              <div className="flex items-center gap-2">
                <span className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-200 px-2.5 py-1 rounded-md shadow-sm">
                  <FileText className="w-3.5 h-3.5 text-blue-600" />
                  ตัวอย่าง PDF จริง (Gotenberg Chromium)
                </span>
                {durationMs !== null && !isRendering && (
                  <span className="text-[11px] font-mono text-emerald-700 bg-emerald-50 border border-emerald-200 px-2 py-0.5 rounded-full">
                    ⚡ {durationMs}ms
                  </span>
                )}
              </div>
              <div className="flex items-center gap-2">
                {isRendering && (
                  <span className="text-[11px] text-blue-600 flex items-center gap-1 font-medium bg-blue-50 border border-blue-200 px-2 py-0.5 rounded-full">
                    <RefreshCw className="w-3 h-3 animate-spin" />
                    กำลังเรนเดอร์...
                  </span>
                )}
                <button
                  type="button"
                  onClick={() => renderPdf(fullHtml, headerHtml, footerHtml)}
                  disabled={isRendering}
                  className="flex items-center gap-1 text-xs px-2.5 py-1 bg-white border border-slate-200 hover:bg-slate-50 text-slate-700 rounded-md shadow-sm cursor-pointer transition-colors"
                >
                  <RefreshCw className={`w-3.5 h-3.5 ${isRendering ? 'animate-spin text-blue-600' : 'text-slate-500'}`} />
                  รีเฟรช
                </button>
              </div>
            </div>

            {/* PDF iframe */}
            <div className="flex-1 bg-slate-200 border border-slate-300 rounded-lg shadow-sm overflow-hidden relative">
              {pdfBlobUrl ? (
                <iframe
                  src={`${pdfBlobUrl}#toolbar=0&navpanes=0&view=FitH`}
                  className={`w-full h-full border-none transition-opacity duration-200 ${isRendering ? 'opacity-50' : 'opacity-100'}`}
                  title="PDF Preview"
                />
              ) : (
                <div className="h-full flex flex-col items-center justify-center text-slate-400 text-sm gap-2">
                  <div className="w-8 h-8 border-[3px] border-slate-300 border-t-blue-600 rounded-full animate-spin" />
                  <span>กำลังส่งข้อมูลไปยัง Gotenberg...</span>
                </div>
              )}
              {renderError && (
                <div className="absolute bottom-4 left-4 right-4 bg-red-50 border border-red-200 p-3 rounded text-xs text-red-700 flex items-start gap-2 shadow-md">
                  <span className="shrink-0 mt-0.5">⚠</span>
                  <div>
                    <strong>เกิดข้อผิดพลาด:</strong>
                    <p className="mt-0.5">{renderError}</p>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

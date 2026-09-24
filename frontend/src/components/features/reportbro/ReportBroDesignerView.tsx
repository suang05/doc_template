'use client';

import { useEffect, useRef, useState, useCallback } from 'react';
import Script from 'next/script';
import { api } from '@/lib/api-client';
import type { ToastIntent } from '@/hooks/useToast';

interface ReportBroDesignerViewProps {
  apiKey: string;
  onToast: (message: string, intent?: ToastIntent) => void;
}

// Minimal valid A4 report skeleton for "New template"
const EMPTY_REPORT = {
  version: 6,
  docElements: [],
  parameters: [],
  styles: [],
  watermarks: [],
  documentProperties: {
    pageFormat: 'A4',
    orientation: 'portrait',
    marginLeft: '15mm',
    marginTop: '15mm',
    marginRight: '15mm',
    marginBottom: '15mm',
    patternLocale: 'en',
    patternCurrencySymbol: '฿',
  },
};

// Sample invoice template for demonstration
const SAMPLE_INVOICE = {
  version: 6,
  docElements: [
    {
      elementType: 'text',
      id: 1,
      x: 30,
      y: 20,
      width: 300,
      height: 35,
      content: 'ใบเสร็จรับเงิน / RECEIPT',
      eval: false,
      style: { bold: true, fontSize: 20, color: '#1e293b' },
    },
    {
      elementType: 'text',
      id: 2,
      x: 30,
      y: 70,
      width: 535,
      height: 20,
      content: 'เลขที่: ${invoice_no}   วันที่: ${invoice_date}',
      eval: false,
      style: { fontSize: 11, color: '#475569' },
    },
    {
      elementType: 'line',
      id: 3,
      x: 30,
      y: 100,
      width: 535,
      height: 2,
      style: { borderWidth: 1, borderColor: '#cbd5e1' },
    },
    {
      elementType: 'text',
      id: 4,
      x: 30,
      y: 120,
      width: 535,
      height: 20,
      content: 'ลูกค้า: ${customer_name}',
      eval: false,
      style: { fontSize: 11, color: '#1e293b' },
    },
  ],
  parameters: [
    { name: 'invoice_no',     type: 'string', eval: false, testData: 'INV-2026-001' },
    { name: 'invoice_date',   type: 'string', eval: false, testData: '01/09/2026' },
    { name: 'customer_name',  type: 'string', eval: false, testData: 'บริษัท ตัวอย่าง จำกัด' },
  ],
  styles: [],
  watermarks: [],
  documentProperties: {
    pageFormat: 'A4',
    orientation: 'portrait',
    marginLeft: '15mm',
    marginTop: '15mm',
    marginRight: '15mm',
    marginBottom: '15mm',
    patternLocale: 'en',
    patternCurrencySymbol: '฿',
  },
};

declare global {
  interface Window {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    ReportBro: new (container: HTMLElement, options: Record<string, unknown>) => any;
  }
}

export function ReportBroDesignerView({ apiKey, onToast }: ReportBroDesignerViewProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const rbRef = useRef<any>(null);
  const [scriptReady, setScriptReady] = useState(false);
  const [initialized, setInitialized] = useState(false);
  const [saving, setSaving] = useState(false);
  const [fileName, setFileName] = useState('my_report');
  const [showSaveModal, setShowSaveModal] = useState(false);

  const initDesigner = useCallback((report: object) => {
    if (!containerRef.current || !window.ReportBro) return;
    if (rbRef.current) {
      try { rbRef.current.destroy?.(); } catch { /* ignore */ }
    }
    rbRef.current = new window.ReportBro(containerRef.current, {
      reportServerUrl: 'https://www.reportbro.com/report/run',
      showGrid: true,
      locale: 'en_us',
    });
    rbRef.current.load(report);
    setInitialized(true);
  }, []);

  useEffect(() => {
    if (scriptReady && containerRef.current && !initialized) {
      initDesigner(EMPTY_REPORT);
    }
  }, [scriptReady, initialized, initDesigner]);

  // Re-sync api key to client on change
  useEffect(() => {
    if (apiKey) api.setApiKey(apiKey);
  }, [apiKey]);

  const handleSave = async () => {
    if (!rbRef.current) return;
    const report = rbRef.current.getReport();
    const content = JSON.stringify(report);
    const name = fileName.trim() || 'my_report';
    const fullName = name.endsWith('.json') ? name : `${name}.json`;

    setSaving(true);
    try {
      await api.saveReportBroTemplate({ fileName: fullName, content, isGlobal: false });
      onToast(`บันทึก "${fullName}" สำเร็จ`, 'success');
      setShowSaveModal(false);
    } catch (err) {
      onToast(`บันทึกล้มเหลว: ${err instanceof Error ? err.message : 'Unknown error'}`, 'error');
    } finally {
      setSaving(false);
    }
  };

  const handleDownload = () => {
    if (!rbRef.current) return;
    const report = rbRef.current.getReport();
    const blob = new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${fileName || 'report'}.json`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="flex flex-col h-full w-full -m-6 overflow-hidden">
      {/* ReportBro CSS injected globally */}
      {/* eslint-disable-next-line @next/next/no-css-tags */}
      <link rel="stylesheet" href="/reportbro/reportbro.css" />

      <Script
        src="/reportbro/reportbro.js"
        strategy="afterInteractive"
        onLoad={() => setScriptReady(true)}
      />

      {/* Toolbar */}
      <div className="flex items-center justify-between px-4 py-2 border-b border-[var(--border)] bg-[var(--bg)] shrink-0">
        <div className="flex items-center gap-2">
          <span className="w-6 h-6 rounded bg-indigo-600 text-white text-[10px] font-bold flex items-center justify-center">RB</span>
          <span className="text-sm font-semibold text-[var(--t1)]">ReportBro Designer</span>
          <span className="text-[10px] text-[var(--t3)] hidden sm:block">Pixel-perfect drag & drop → JSON → PDF</span>
        </div>

        <div className="flex items-center gap-2">
          {/* Quick-load buttons */}
          <button
            onClick={() => initDesigner(EMPTY_REPORT)}
            className="text-xs px-2 py-1 rounded border border-[var(--border)] text-[var(--t2)] hover:text-[var(--t1)] transition-colors"
          >
            หน้าเปล่า
          </button>
          <button
            onClick={() => initDesigner(SAMPLE_INVOICE)}
            className="text-xs px-2 py-1 rounded border border-[var(--border)] text-[var(--t2)] hover:text-[var(--t1)] transition-colors"
          >
            ตัวอย่างใบเสร็จ
          </button>

          <div className="w-px h-4 bg-[var(--border)]" />

          <button
            onClick={handleDownload}
            disabled={!initialized}
            className="text-xs px-2.5 py-1 rounded border border-[var(--border)] text-[var(--t2)] hover:text-[var(--t1)] disabled:opacity-40 transition-colors"
          >
            ⬇ ดาวน์โหลด JSON
          </button>
          <button
            onClick={() => setShowSaveModal(true)}
            disabled={!initialized}
            className="text-xs px-3 py-1 rounded bg-[var(--accent)] text-white font-medium hover:opacity-90 disabled:opacity-40 transition-opacity"
          >
            💾 บันทึกลง Server
          </button>
        </div>
      </div>

      {/* Designer canvas */}
      <div className="flex-1 relative overflow-hidden bg-[#e8eaed]">
        {!initialized && (
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-white/80 z-10">
            <div className="w-7 h-7 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin" />
            <p className="text-sm text-[var(--t2)]">กำลังโหลด ReportBro Designer…</p>
          </div>
        )}
        <div ref={containerRef} id="reportbro_container" className="h-full w-full" />
      </div>

      {/* Save modal */}
      {showSaveModal && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
          <div className="bg-[var(--bg)] rounded-xl border border-[var(--border)] shadow-2xl w-[380px] p-5">
            <h3 className="text-sm font-semibold text-[var(--t1)] mb-3">บันทึก Template ลง Server</h3>
            <label className="text-xs text-[var(--t2)] mb-1 block">ชื่อไฟล์ (ไม่ต้องใส่ .json)</label>
            <input
              autoFocus
              value={fileName}
              onChange={e => setFileName(e.target.value)}
              onKeyDown={e => { if (e.key === 'Enter') handleSave(); if (e.key === 'Escape') setShowSaveModal(false); }}
              placeholder="my_report"
              className="w-full text-sm px-3 py-2 rounded border border-[var(--border)] bg-[var(--surf)] text-[var(--t1)] focus:outline-none focus:border-[var(--accent)] mb-4"
            />
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setShowSaveModal(false)}
                className="text-xs px-3 py-1.5 rounded border border-[var(--border)] text-[var(--t2)] hover:text-[var(--t1)]"
              >
                ยกเลิก
              </button>
              <button
                onClick={handleSave}
                disabled={saving}
                className="text-xs px-4 py-1.5 rounded bg-[var(--accent)] text-white font-medium hover:opacity-90 disabled:opacity-50"
              >
                {saving ? 'กำลังบันทึก…' : 'บันทึก'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

"use client";
import { useEffect, useRef, useState } from "react";
import { RefreshCw, Eye, QrCode, Barcode, Table as TableIcon, Plus, Trash2,
         FileCheck2, Clock, Download, AlertCircle, Sparkles, Pencil } from "lucide-react";
import { useGenerator } from "@/hooks/useGenerator";
import { Button } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";
import { cn } from "@/utils/cn";
import { api } from "@/lib/api-client";
import type { TemplateDetail, OutputFormat } from "@/types/api";

interface GeneratorViewProps {
  templates: TemplateDetail[];
  onRefreshTemplates: () => void;
  onGenerationSuccess?: () => void;
  onToast?: (msg: string, intent?: "success" | "error" | "warning" | "info") => void;
}

// ── Format card icons ──────────────────────────────────────────────────────
function PdfIcon() {
  return (
    <svg width="36" height="44" viewBox="0 0 36 44" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="1" y="1" width="34" height="42" rx="4" fill="white" stroke="#e5e7eb" strokeWidth="1.5"/>
      <path d="M22 1v9h9" stroke="#e5e7eb" strokeWidth="1.5" fill="none"/>
      <path d="M22 1l9 9" stroke="#e5e7eb" strokeWidth="1.5"/>
      <rect x="4" y="27" width="28" height="12" rx="2" fill="#e11d48"/>
      <text x="18" y="37" textAnchor="middle" fontSize="7" fontWeight="700" fill="white" fontFamily="system-ui">PDF</text>
      <line x1="8" y1="20" x2="28" y2="20" stroke="#e5e7eb" strokeWidth="1.5" strokeLinecap="round"/>
      <line x1="8" y1="24" x2="22" y2="24" stroke="#e5e7eb" strokeWidth="1.5" strokeLinecap="round"/>
    </svg>
  );
}

function DocxIcon() {
  return (
    <svg width="36" height="44" viewBox="0 0 36 44" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="1" y="1" width="34" height="42" rx="4" fill="white" stroke="#e5e7eb" strokeWidth="1.5"/>
      <path d="M22 1v9h9" stroke="#e5e7eb" strokeWidth="1.5" fill="none"/>
      <path d="M22 1l9 9" stroke="#e5e7eb" strokeWidth="1.5"/>
      <rect x="4" y="27" width="28" height="12" rx="2" fill="#2954ff"/>
      <text x="18" y="37" textAnchor="middle" fontSize="6" fontWeight="700" fill="white" fontFamily="system-ui">DOCX</text>
      <line x1="8" y1="20" x2="28" y2="20" stroke="#e5e7eb" strokeWidth="1.5" strokeLinecap="round"/>
      <line x1="8" y1="24" x2="22" y2="24" stroke="#e5e7eb" strokeWidth="1.5" strokeLinecap="round"/>
    </svg>
  );
}

function XlsxIcon() {
  return (
    <svg width="36" height="44" viewBox="0 0 36 44" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="1" y="1" width="34" height="42" rx="4" fill="white" stroke="#e5e7eb" strokeWidth="1.5"/>
      <path d="M22 1v9h9" stroke="#e5e7eb" strokeWidth="1.5" fill="none"/>
      <path d="M22 1l9 9" stroke="#e5e7eb" strokeWidth="1.5"/>
      <rect x="4" y="27" width="28" height="12" rx="2" fill="#059669"/>
      <text x="18" y="37" textAnchor="middle" fontSize="6" fontWeight="700" fill="white" fontFamily="system-ui">XLSX</text>
      <line x1="8" y1="20" x2="28" y2="20" stroke="#e5e7eb" strokeWidth="1.5" strokeLinecap="round"/>
      <line x1="8" y1="24" x2="22" y2="24" stroke="#e5e7eb" strokeWidth="1.5" strokeLinecap="round"/>
    </svg>
  );
}

function HtmlIcon() {
  return (
    <svg width="36" height="44" viewBox="0 0 36 44" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="1" y="1" width="34" height="42" rx="4" fill="white" stroke="#e5e7eb" strokeWidth="1.5"/>
      <path d="M22 1v9h9" stroke="#e5e7eb" strokeWidth="1.5" fill="none"/>
      <path d="M22 1l9 9" stroke="#e5e7eb" strokeWidth="1.5"/>
      <rect x="4" y="27" width="28" height="12" rx="2" fill="#d97706"/>
      <text x="18" y="37" textAnchor="middle" fontSize="6" fontWeight="700" fill="white" fontFamily="system-ui">HTML</text>
      <line x1="8" y1="20" x2="28" y2="20" stroke="#e5e7eb" strokeWidth="1.5" strokeLinecap="round"/>
      <line x1="8" y1="24" x2="22" y2="24" stroke="#e5e7eb" strokeWidth="1.5" strokeLinecap="round"/>
    </svg>
  );
}

function NativeFileIcon() {
  return (
    <svg width="36" height="44" viewBox="0 0 36 44" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="1" y="1" width="34" height="42" rx="4" fill="white" stroke="#e5e7eb" strokeWidth="1.5"/>
      <path d="M22 1v9h9" stroke="#e5e7eb" strokeWidth="1.5" fill="none"/>
      <path d="M22 1l9 9" stroke="#e5e7eb" strokeWidth="1.5"/>
      <rect x="4" y="27" width="28" height="12" rx="2" fill="#6b7280"/>
      <text x="18" y="37" textAnchor="middle" fontSize="5.5" fontWeight="700" fill="white" fontFamily="system-ui">ต้นฉบับ</text>
      <line x1="8" y1="20" x2="28" y2="20" stroke="#e5e7eb" strokeWidth="1.5" strokeLinecap="round"/>
      <line x1="8" y1="24" x2="22" y2="24" stroke="#e5e7eb" strokeWidth="1.5" strokeLinecap="round"/>
    </svg>
  );
}

const NATIVE_ICON_MAP: Record<string, () => React.ReactElement> = {
  docx: DocxIcon,
  xlsx: XlsxIcon,
  html: HtmlIcon,
};

export function GeneratorView({ templates, onRefreshTemplates, onGenerationSuccess, onToast }: GeneratorViewProps) {
  const g = useGenerator(templates);
  const [formatMode, setFormatMode] = useState<"pdf" | "native">("pdf");

  // derive native format from schema or filename extension
  const nativeExt = (g.schema?.format?.toLowerCase() ?? g.selectedFileName.split(".").pop() ?? "docx") as OutputFormat;
  const NativeIcon = NATIVE_ICON_MAP[nativeExt] ?? NativeFileIcon;

  const handleFormatMode = (mode: "pdf" | "native") => {
    setFormatMode(mode);
    g.setOutputFormat(mode === "pdf" ? "pdf" : nativeExt);
  };

  // re-sync native format when template changes
  useEffect(() => {
    if (formatMode === "native") {
      g.setOutputFormat(nativeExt);
    }
  }, [nativeExt]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (templates.length > 0 && !g.selectedFileName) {
      g.selectTemplate(templates[0].fileName);
    }
  }, [templates]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (g.result) {
      onGenerationSuccess?.();
      onToast?.("สร้างเอกสารสำเร็จ", "success");
    }
  }, [g.result]); // eslint-disable-line react-hooks/exhaustive-deps

  const { textVars, qrVars, barVars, tableVars, fallbackScalar, fallbackTable } = g;

  return (
    <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 items-start">

      {/* ── Left panel ────────────────────────────────────────── */}
      <div className="lg:col-span-5 flex flex-col gap-3">

        {/* 1. Template selector */}
        <div className="p-4 rounded-[var(--r)] border border-[var(--border)] bg-[var(--surf)]">
          <div className="flex items-center justify-between mb-2">
            <span className="text-t-xs font-bold text-[var(--t3)] uppercase tracking-[.5px]">เลือกแม่แบบ</span>
            <div className="flex items-center gap-2 text-t-xs">
              <button onClick={() => g.setShowPreviewModal(true)} disabled={!g.selectedFileName}
                className="flex items-center gap-1 text-[var(--navy)] hover:text-[var(--blue)] font-semibold disabled:opacity-40 transition-colors">
                <Eye className="w-3.5 h-3.5" /> ดูแม่แบบ
              </button>
              <span className="text-[var(--border)]">|</span>
              <button onClick={onRefreshTemplates}
                className="flex items-center gap-1 text-[var(--t3)] hover:text-[var(--t1)] transition-colors">
                <RefreshCw className="w-3 h-3" /> รีเฟรช
              </button>
            </div>
          </div>
          <div className="relative">
            <div className="pointer-events-none absolute left-3 inset-y-0 flex items-center text-[var(--t3)]">
              <svg width="13" height="13" viewBox="0 0 16 16" fill="none"><path d="M3 2h7l3 3v9a1 1 0 01-1 1H3a1 1 0 01-1-1V3a1 1 0 011-1z" stroke="currentColor" strokeWidth="1.5"/><path d="M10 2v3h3" stroke="currentColor" strokeWidth="1.5"/></svg>
            </div>
            <select value={g.selectedFileName} onChange={(e) => g.selectTemplate(e.target.value)}
              className="w-full appearance-none bg-[var(--sep)] border border-[var(--border)] rounded-[var(--ri)] pl-7 pr-8 py-2 text-t-sm text-[var(--t1)] focus:outline-none focus:border-[var(--blue)] focus:ring-2 focus:ring-[var(--blue-t)] transition cursor-pointer">
              {templates.length === 0
                ? <option value="">(ไม่พบแม่แบบในระบบ)</option>
                : templates.map((t) => (
                    <option key={t.fileName} value={t.fileName}>{t.fileName}</option>
                  ))
              }
            </select>
            <span className="pointer-events-none absolute right-3 inset-y-0 flex items-center text-[var(--t3)]">▾</span>
          </div>
          {g.schema && (
            <div className="flex items-center justify-between text-t-xs text-[var(--t3)] mt-1.5">
              <span className="text-[var(--emerald)] font-medium">✓ พบ {g.schema.count} ตัวแปร</span>
              <span className="font-mono font-bold text-[var(--navy)]">{g.schema.format}</span>
            </div>
          )}
        </div>

        {/* 2. Data input */}
        <div className="p-4 rounded-[var(--r)] border border-[var(--border)] bg-[var(--surf)]">
          <div className="flex items-center justify-between mb-3">
            <span className="text-t-xs font-bold text-[var(--t3)] uppercase tracking-[.5px]">กรอกข้อมูลเอกสาร</span>
            <div className="flex items-center gap-0.5 bg-[var(--sep)] rounded-[var(--rp)] p-0.5">
              {(["form", "json"] as const).map((mode) => (
                <button key={mode} onClick={() => g.setInputMode(mode)}
                  className={cn("px-2.5 py-0.5 rounded-[var(--rp)] text-t-xs font-medium transition-colors",
                    g.inputMode === mode
                      ? "bg-[var(--surf)] text-[var(--navy)] font-semibold shadow-sm"
                      : "text-[var(--t3)] hover:text-[var(--t1)]"
                  )}>
                  {mode === "form" ? "ฟอร์ม" : "JSON"}
                </button>
              ))}
            </div>
          </div>

          {g.inputMode === "form"
            ? <FormFields g={g} textVars={textVars} qrVars={qrVars} barVars={barVars} tableVars={tableVars} fallbackScalar={fallbackScalar} fallbackTable={fallbackTable} />
            : <JsonEditor g={g} />
          }
        </div>

        {/* 3. Format + Generate */}
        <div className="p-4 rounded-[var(--r)] border border-[var(--border)] bg-[var(--surf)]">
          <span className="text-t-xs font-bold text-[var(--t3)] uppercase tracking-[.5px]">รูปแบบเอกสาร</span>
          <div className="grid grid-cols-2 gap-2 mt-2">
            {/* PDF card */}
            <button onClick={() => handleFormatMode("pdf")}
              className={cn("py-3 px-2 rounded-[var(--rb)] text-center flex flex-col items-center gap-2 transition-colors border",
                formatMode === "pdf"
                  ? "border-[var(--navy)] bg-[var(--blue-t)]"
                  : "border-[var(--border)] bg-[var(--sep)] hover:border-[var(--t2)]"
              )}>
              <PdfIcon />
              <span className={cn("text-t-xs font-semibold", formatMode === "pdf" ? "text-[var(--navy)]" : "text-[var(--t2)]")}>PDF</span>
            </button>

            {/* Native format card */}
            <button onClick={() => handleFormatMode("native")}
              className={cn("py-3 px-2 rounded-[var(--rb)] text-center flex flex-col items-center gap-2 transition-colors border",
                formatMode === "native"
                  ? "border-[var(--navy)] bg-[var(--blue-t)]"
                  : "border-[var(--border)] bg-[var(--sep)] hover:border-[var(--t2)]"
              )}>
              <NativeIcon />
              <span className={cn("text-t-xs font-semibold", formatMode === "native" ? "text-[var(--navy)]" : "text-[var(--t2)]")}>
                ตามนามสกุลไฟล์
              </span>
            </button>
          </div>

          {g.error && (
            <div className="mt-3 flex items-start gap-2 p-2.5 bg-[var(--rose-t)] border border-[var(--rose)] rounded-[var(--rb)] text-t-xs text-[var(--rose)]">
              <AlertCircle className="w-3.5 h-3.5 shrink-0 mt-0.5" />
              <span>{g.error}</span>
            </div>
          )}

          <Button className="w-full mt-3" size="lg" variant="navy"
            onClick={async () => { await g.generate(); }}
            disabled={g.generating || !g.selectedFileName}>
            {g.generating
              ? <><RefreshCw className="w-4 h-4 animate-spin" /> กำลังประมวลผล...</>
              : <><Sparkles className="w-4 h-4" /> แสดงตัวอย่าง</>
            }
          </Button>
        </div>
      </div>

      {/* ── Right panel: Preview ───────────────────────────────── */}
      <div className="lg:col-span-7">
        <ResultPanel g={g} />
      </div>

      {/* Schema preview modal */}
      <Modal open={g.showPreviewModal} onClose={() => g.setShowPreviewModal(false)}
        title={`โครงสร้างแม่แบบ: ${g.selectedFileName}`}
        size="md"
        footer={
          <Button size="sm" variant="navy" onClick={() => g.setShowPreviewModal(false)}>ปิดหน้าต่าง</Button>
        }>
        <SchemaPreview g={g} />
      </Modal>
    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────────

type G = ReturnType<typeof useGenerator>;

function FormFields({ g, textVars, qrVars, barVars, tableVars, fallbackScalar, fallbackTable }: {
  g: G;
  textVars: G["schemaVars"];
  qrVars: G["schemaVars"];
  barVars: G["schemaVars"];
  tableVars: G["schemaVars"];
  fallbackScalar: string[];
  fallbackTable: string[];
}) {
  const inputCls = "w-full bg-[var(--sep)] border border-[var(--border)] rounded-[var(--ri)] px-3 py-1.5 text-t-sm text-[var(--t1)] focus:outline-none focus:border-[var(--blue)] focus:ring-2 focus:ring-[var(--blue-t)] transition";

  const label = (key: string) => g.mappingLabels[key] || key;

  return (
    <div className="space-y-3">
      {/* Text / Number / Date */}
      {(textVars.length > 0 || fallbackScalar.length > 0) && (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 max-h-60 overflow-y-auto pr-0.5">
          {textVars.map((v) => (
            <div key={v.key}>
              <label className="block text-t-xs font-medium text-[var(--t2)] mb-0.5">
                {label(v.key)}{g.mappingLabels[v.key] ? " *" : ""}
              </label>
              <input type={v.type === "number" ? "number" : v.type === "date" ? "date" : "text"}
                value={String(g.formData[v.key] ?? "")}
                onChange={(e) => g.updateFormField(v.key, v.type === "number" ? (parseFloat(e.target.value) || 0) : e.target.value)}
                placeholder={label(v.key)} className={inputCls} />
            </div>
          ))}
          {fallbackScalar.map((k) => (
            <div key={k}>
              <label className="block text-t-xs font-medium text-[var(--t2)] mb-0.5">{label(k)}</label>
              <input type={typeof g.formData[k] === "number" ? "number" : "text"}
                value={String(g.formData[k] ?? "")}
                onChange={(e) => g.updateFormField(k, typeof g.formData[k] === "number" ? (parseFloat(e.target.value) || 0) : e.target.value)}
                className={inputCls} />
            </div>
          ))}
        </div>
      )}

      {/* QR Codes */}
      {qrVars.length > 0 && (
        <div className="p-2.5 bg-[var(--blue-t)] border border-[var(--border)] rounded-[var(--rb)]">
          <p className="text-t-xs font-bold text-[var(--navy)] flex items-center gap-1.5 mb-2">
            <QrCode className="w-3.5 h-3.5" /> QR Code ({qrVars.length})
          </p>
          {qrVars.map((v) => (
            <div key={v.key} className="mb-1.5 last:mb-0">
              <label className="block text-t-xs font-medium text-[var(--t2)] mb-0.5">{label(v.key)}</label>
              <input type="text" value={String(g.formData[v.key] ?? "")}
                onChange={(e) => g.updateFormField(v.key, e.target.value)}
                placeholder="เช่น https://www.sammakorn.co.th" className={inputCls} />
            </div>
          ))}
        </div>
      )}

      {/* Barcodes */}
      {barVars.length > 0 && (
        <div className="p-2.5 bg-[var(--amber-t)] border border-[var(--border)] rounded-[var(--rb)]">
          <p className="text-t-xs font-bold text-[var(--amber)] flex items-center gap-1.5 mb-2">
            <Barcode className="w-3.5 h-3.5" /> Barcode ({barVars.length})
          </p>
          {barVars.map((v) => (
            <div key={v.key} className="mb-1.5 last:mb-0">
              <label className="block text-t-xs font-medium text-[var(--t2)] mb-0.5">{label(v.key)}</label>
              <input type="text" value={String(g.formData[v.key] ?? "")}
                onChange={(e) => g.updateFormField(v.key, e.target.value)}
                placeholder="เช่น SMK-INV-001" className={inputCls} />
            </div>
          ))}
        </div>
      )}

      {/* Tables — Spreadsheet editor */}
      {[...tableVars.map((v) => ({ key: v.key, cols: v.columns?.length ? v.columns : ["ItemNo", "Description", "Quantity", "UnitPrice", "Amount"] })),
         ...fallbackTable.map((k) => {
           const rows = g.formData[k] as Record<string, unknown>[];
           return { key: k, cols: rows?.length ? Object.keys(rows[0]) : ["ItemNo", "Description"] };
         })
      ].map(({ key, cols }) => {
        const rows = Array.isArray(g.formData[key]) ? g.formData[key] as Record<string, unknown>[] : [];
        return (
          <SpreadsheetEditor
            key={key}
            tableKey={key}
            cols={cols}
            rows={rows}
            label={label(key)}
            onUpdate={(ri, col, val) => g.updateTableCell(key, ri, col, val)}
            onAdd={() => g.addTableRow(key, cols)}
            onDelete={(ri) => g.deleteTableRow(key, ri)}
          />
        );
      })}
    </div>
  );
}

// ── Spreadsheet-style table editor ───────────────────────────────────────
function SpreadsheetEditor({ tableKey, cols, rows, label, onUpdate, onAdd, onDelete }: {
  tableKey: string;
  cols: string[];
  rows: Record<string, unknown>[];
  label: string;
  onUpdate: (ri: number, col: string, val: string) => void;
  onAdd: () => void;
  onDelete: (ri: number) => void;
}) {
  const [activeCell, setActiveCell] = useState<{ ri: number; col: string } | null>(null);
  const inputRefs = useRef<Record<string, HTMLInputElement | null>>({});

  const cellKey = (ri: number, col: string) => `${tableKey}-${ri}-${col}`;

  const focusCell = (ri: number, col: string) => {
    setActiveCell({ ri, col });
    requestAnimationFrame(() => inputRefs.current[cellKey(ri, col)]?.focus());
  };

  const handleKeyDown = (e: React.KeyboardEvent, ri: number, col: string) => {
    const ci = cols.indexOf(col);
    if (e.key === "Tab") {
      e.preventDefault();
      if (ci < cols.length - 1) focusCell(ri, cols[ci + 1]);
      else if (ri < rows.length - 1) focusCell(ri + 1, cols[0]);
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (ri < rows.length - 1) focusCell(ri + 1, col);
    } else if (e.key === "Escape") {
      setActiveCell(null);
      (document.activeElement as HTMLElement)?.blur();
    } else if (e.key === "ArrowRight" && ci < cols.length - 1) { e.preventDefault(); focusCell(ri, cols[ci + 1]); }
    else if (e.key === "ArrowLeft"  && ci > 0)                  { e.preventDefault(); focusCell(ri, cols[ci - 1]); }
    else if (e.key === "ArrowDown"  && ri < rows.length - 1)    { e.preventDefault(); focusCell(ri + 1, col); }
    else if (e.key === "ArrowUp"    && ri > 0)                  { e.preventDefault(); focusCell(ri - 1, col); }
  };

  return (
    <div className="rounded-[var(--rb)] border border-[var(--border)] overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-2.5 py-2 bg-[var(--emerald-t)]">
        <span className="text-t-xs font-bold text-[var(--emerald)] flex items-center gap-1.5">
          <TableIcon className="w-3.5 h-3.5" />
          {label}
          <span className="font-normal text-[var(--t3)]">({rows.length} แถว)</span>
        </span>
        <button onClick={onAdd}
          className="flex items-center gap-1 text-t-xs font-medium text-[var(--emerald)] border border-[var(--emerald)] hover:bg-[var(--emerald)] hover:text-white px-2 py-0.5 rounded transition-colors">
          <Plus className="w-3 h-3" /> เพิ่มแถว
        </button>
      </div>

      {/* Spreadsheet */}
      <div className="overflow-x-auto max-h-52 bg-[var(--surf)]">
        <table className="w-full border-collapse text-t-xs">
          <thead>
            <tr className="bg-[var(--sep)] border-b border-[var(--border)]">
              <th className="w-7 border-r border-[var(--border)] select-none" />
              {cols.map((c) => (
                <th key={c} className="px-3 py-1.5 text-left font-mono font-semibold text-[var(--t3)] whitespace-nowrap border-r border-[var(--border)] last:border-r-0">
                  {c}
                </th>
              ))}
              <th className="w-7" />
            </tr>
          </thead>
          <tbody>
            {rows.map((row, ri) => (
              <tr key={ri} className="border-b border-[var(--sep)] last:border-0 group">
                {/* Row number */}
                <td className="w-7 text-center text-[10px] text-[var(--t3)] bg-[var(--sep)] border-r border-[var(--border)] select-none py-0.5">
                  {ri + 1}
                </td>
                {cols.map((c) => {
                  const isActive = activeCell?.ri === ri && activeCell?.col === c;
                  return (
                    <td key={c}
                      onClick={() => focusCell(ri, c)}
                      className={cn(
                        "relative border-r border-[var(--sep)] last:border-r-0 p-0 cursor-text",
                        isActive && "ring-2 ring-inset ring-[var(--blue)] z-10"
                      )}>
                      <input
                        ref={(el) => { inputRefs.current[cellKey(ri, c)] = el; }}
                        type="text"
                        value={String(row[c] ?? "")}
                        onChange={(e) => onUpdate(ri, c, e.target.value)}
                        onKeyDown={(e) => handleKeyDown(e, ri, c)}
                        onFocus={() => setActiveCell({ ri, col: c })}
                        onBlur={() => setActiveCell(null)}
                        className="w-full min-w-[80px] bg-transparent px-3 py-1.5 text-[var(--t1)] focus:outline-none"
                      />
                    </td>
                  );
                })}
                {/* Delete — visible on row hover */}
                <td className="w-7 text-center py-0.5">
                  <button onClick={() => onDelete(ri)}
                    className="opacity-0 group-hover:opacity-100 text-[var(--t3)] hover:text-[var(--rose)] p-0.5 rounded transition-all">
                    <Trash2 className="w-3 h-3" />
                  </button>
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={cols.length + 2} className="py-5 text-center text-t-xs text-[var(--t3)] italic">
                  ยังไม่มีแถว — กด <span className="font-semibold not-italic text-[var(--emerald)]">+ เพิ่มแถว</span> เพื่อเริ่มต้น
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function JsonEditor({ g }: { g: G }) {
  return (
    <div>
      <div className="flex items-center justify-between mb-1.5">
        <label className="text-t-xs font-bold text-[var(--t3)] uppercase tracking-[.5px]">JSON Data Binding</label>
        <div className="flex items-center gap-2 text-t-xs">
          <button onClick={g.prettifyJson} className="text-[var(--navy)] hover:text-[var(--blue)] font-mono font-medium transition-colors">จัดรูปแบบ</button>
          <span className="text-[var(--border)]">•</span>
          <button onClick={() => { g.updateJson("{}"); }} className="text-[var(--t3)] hover:text-[var(--rose)] transition-colors">ล้าง</button>
        </div>
      </div>
      <textarea value={g.jsonText} onChange={(e) => g.updateJson(e.target.value)} rows={12} spellCheck={false}
        className="w-full bg-[var(--t1)] text-[var(--emerald)] font-mono text-t-xs p-3 rounded-[var(--rb)] border border-[var(--border)] focus:outline-none focus:border-[var(--blue)] resize-y"
        placeholder={'{\n  "key": "value"\n}'} />
      {g.jsonError && (
        <p className="text-t-xs text-[var(--rose)] mt-1 font-mono flex items-center gap-1">
          <AlertCircle className="w-3.5 h-3.5" /> {g.jsonError}
        </p>
      )}
    </div>
  );
}

// ── Document skeleton ──────────────────────────────────────────────────────
function DocSkeleton() {
  return (
    <div className="flex-1 flex flex-col p-8 gap-3 min-h-[480px]">
      <div className="w-32 h-3 rounded bg-[var(--navy)] opacity-70" />
      <div className="w-64 h-4 rounded bg-[var(--navy)] opacity-80" />
      <div className="w-48 h-2.5 rounded bg-[var(--border)]" />
      <div className="h-3" />
      {[100, 95, 85, 90, 70].map((w, i) => (
        <div key={i} className="h-2 rounded bg-[var(--sep)]" style={{ width: `${w}%` }} />
      ))}
      <div className="h-2" />
      <div className="grid grid-cols-2 gap-3">
        {[1, 2].map((i) => (
          <div key={i} className="h-12 rounded-[var(--rb)] border border-[var(--border)] bg-[var(--sep)]" />
        ))}
      </div>
      <div className="h-2" />
      {[90, 75].map((w, i) => (
        <div key={i} className="h-2 rounded bg-[var(--sep)]" style={{ width: `${w}%` }} />
      ))}
      <div className="mt-auto flex justify-end">
        <div className="w-7 h-7 rounded-full border border-[var(--border)] bg-[var(--sep)]" />
      </div>
    </div>
  );
}

function ResultPanel({ g }: { g: G }) {
  return (
    <div className="rounded-[var(--r)] border border-[var(--border)] bg-[var(--surf)] flex flex-col min-h-[560px]">
      <div className="flex items-center justify-between px-4 py-2.5 border-b border-[var(--border)]">
        <span className="text-t-sm font-semibold text-[var(--t1)]">ตัวอย่างเอกสาร</span>
        {g.result && (
          <div className="flex items-center gap-2">
            <Button size="sm" variant="outline"
              onClick={() => api.downloadDocument(g.result!.response.logId, g.selectedFileName)}>
              <Pencil className="w-3.5 h-3.5" /> เปิดไฟล์
            </Button>
            <Button size="sm" style={{ background: "var(--amber)", color: "white", border: "none" }}
              onClick={() => api.downloadDocument(g.result!.response.logId, g.selectedFileName)}>
              <Download className="w-3.5 h-3.5" /> ดาวน์โหลด
            </Button>
          </div>
        )}
      </div>

      <div className="flex-1 flex flex-col">
        {g.generating ? (
          <div className="flex-1 flex flex-col items-center justify-center gap-3 text-center p-8">
            <RefreshCw className="w-7 h-7 text-[var(--navy)] animate-spin" />
            <p className="text-t-sm font-medium text-[var(--t2)]">กำลังรวมข้อมูลและเรนเดอร์เอกสาร...</p>
            <p className="text-t-xs text-[var(--t3)]">ระบบกำลังส่งคำขอไปยัง Engine ({g.outputFormat.toUpperCase()})</p>
          </div>
        ) : g.result ? (
          <div className="flex-1 flex flex-col">
            <div className="flex items-center justify-between px-4 py-2 bg-[var(--emerald-t)] border-b border-[var(--border)] text-t-xs text-[var(--emerald)]">
              <span className="flex items-center gap-2">
                <FileCheck2 className="w-3.5 h-3.5" />
                สร้างสำเร็จ: <strong className="font-mono">{g.result.templateName}</strong>
              </span>
              <span className="flex items-center gap-1 font-mono">
                <Clock className="w-3 h-3" /> {g.result.executionTimeMs} ms
              </span>
            </div>
            <div className="flex-1 min-h-[480px]">
              <iframe
                src={`${api.getBaseUrl()}/api/Document/preview-file?obj=${encodeURIComponent(g.result.response.previewObjectName)}&apiKey=${encodeURIComponent(api.getApiKey())}#toolbar=1`}
                className="w-full h-full min-h-[480px] border-none" title="PDF Preview" />
            </div>
          </div>
        ) : (
          <DocSkeleton />
        )}
      </div>
    </div>
  );
}

function SchemaPreview({ g }: { g: G }) {
  const typeColor: Record<string, string> = {
    table:   "bg-[var(--blue-t)] text-[var(--blue)]",
    qrcode:  "bg-[var(--sky-t)] text-[var(--sky)]",
    barcode: "bg-[var(--amber-t)] text-[var(--amber)]",
    text:    "bg-[var(--sep)] text-[var(--t2)]",
    number:  "bg-[var(--sep)] text-[var(--t2)]",
    date:    "bg-[var(--sep)] text-[var(--t2)]",
  };

  return (
    <div className="space-y-4 text-t-sm">
      <div className="flex items-center justify-between p-3 bg-[var(--sep)] border border-[var(--border)] rounded-[var(--rb)]">
        <span className="text-[var(--t2)]">รูปแบบ: <strong className="text-[var(--navy)] font-mono uppercase">{g.schema?.format || "DOCX"}</strong></span>
        <a href={api.getTemplateDownloadUrl(g.selectedFileName)} download>
          <Button size="sm" variant="outline"><Download className="w-3.5 h-3.5" /> ดาวน์โหลดแม่แบบ</Button>
        </a>
      </div>
      <div>
        <p className="text-t-xs font-bold text-[var(--t2)] uppercase tracking-[.5px] mb-2">
          ตัวแปรที่ตรวจพบ ({g.schema?.variables?.length ?? 0} ตัวแปร)
        </p>
        {g.schemaLoading ? (
          <p className="text-[var(--t3)] py-4 text-center text-t-xs">กำลังตรวจสอบ...</p>
        ) : !g.schema?.variables?.length ? (
          <p className="text-[var(--t3)] py-4 text-center italic text-t-xs">ไม่พบตัวแปร</p>
        ) : (
          <div className="space-y-1.5 max-h-60 overflow-y-auto pr-1">
            {g.schema.variables.map((v) => (
              <div key={v.key} className="flex items-center justify-between p-2 bg-[var(--sep)] border border-[var(--border)] rounded">
                <div className="flex items-center gap-2">
                  <span className={cn("text-t-xs font-bold uppercase px-1.5 py-0.5 rounded", typeColor[v.type] ?? typeColor.text)}>{v.type}</span>
                  <span className="font-mono text-[var(--navy)] font-semibold text-t-xs">{`{{${v.key}}}`}</span>
                </div>
                {v.columns && (
                  <span className="text-[var(--t3)] font-mono truncate max-w-[200px] text-t-xs" title={v.columns.join(", ")}>
                    {v.columns.join(", ")}
                  </span>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

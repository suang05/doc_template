'use client';

import React, { useState, useEffect, useCallback, Fragment } from 'react';
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  SortableContext,
  verticalListSortingStrategy,
  useSortable,
  sortableKeyboardCoordinates,
  arrayMove,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import {
  GripVertical, Plus, Trash2, Save, Eye, EyeOff,
  RefreshCw, CheckCircle, XCircle, Database,
  ChevronDown, ChevronRight, GitFork, AlertTriangle,
} from 'lucide-react';
import { useTemplates } from '@/hooks/useTemplates';
import { templatesApi } from '@/lib/api/templates.api';
import { getTemplateDatasets } from '@/lib/api/templateDatasets.api';
import { SaveFieldMappingItem, DataSourceType, ThaiTransformTypes, TemplateDatasetDto } from '@/types/api';
import { Button, Select, Toolbar } from '@/components/ui';
import { cn } from '@/utils/cn';
import { tokens } from '@/tokens';

// ── PlaceholderType (frontend-only — affects rendering hints & transform logic) ─
const PH_TYPES = [
  { value: 'text',    label: 'ข้อความ',  color: tokens.colors.textMuted },
  { value: 'table',   label: 'ตาราง',    color: tokens.categories.official.primary },
  { value: 'qrcode',  label: 'QR Code',  color: tokens.categories.financial.primary },
  { value: 'barcode', label: 'Barcode',  color: tokens.formats.html.primary },
  { value: 'image',   label: 'รูปภาพ',  color: tokens.categories.contract.primary },
] as const;
type PlaceholderType = typeof PH_TYPES[number]['value'];
const PT_MAP = Object.fromEntries(PH_TYPES.map(t => [t.value, t])) as Record<PlaceholderType, typeof PH_TYPES[number]>;

// ── Internal row type ──────────────────────────────────────────────────────────
type MappingRow = SaveFieldMappingItem & {
  _id: string;
  placeholderType: PlaceholderType;
};

// ── Static config ──────────────────────────────────────────────────────────────
const DS_PLACEHOLDER: Record<DataSourceType, string> = {
  json: 'เช่น payment.amount',
  sql:  '',
};
const TRANSFORM_OPTIONS = [
  { value: '', label: '— ไม่แปลง —' },
  ...ThaiTransformTypes.map(t => ({ value: t.value, label: t.label })),
];
const CONN_OPTIONS_LOADING = [{ value: '', label: 'กำลังโหลด...' }];

function genId() { return Math.random().toString(36).slice(2, 10); }
function newRow(sortOrder: number): MappingRow {
  return {
    _id: genId(), placeholder: '', placeholderType: 'text',
    dataSourceType: 'json', sourcePath: '', label: '',
    required: false, defaultValue: null, transform: null,
    datasetAlias: null, resultPath: null, mathExpression: null, sortOrder,
  };
}

// ── DS-type pill styling ───────────────────────────────────────────────────────
function dsPillCls(ds: DataSourceType) {
  if (ds === 'json') return 'bg-teal-50 text-teal-700 border-teal-200 hover:border-teal-300';
  return 'bg-amber-50 text-amber-700 border-amber-200 hover:border-amber-300';
}

// ── Preview simulation (client-side, approximate) ─────────────────────────────
type PreviewResult = {
  ph: string; phType: PlaceholderType; ds: DataSourceType;
  value: string | null; transform: string | null;
};

function simulatePreview(rows: MappingRow[], jsonText: string): PreviewResult[] {
  let data: Record<string, unknown> = {};
  try { data = JSON.parse(jsonText); } catch { /* noop */ }

  function dotGet(path: string, obj: Record<string, unknown>): string | null {
    const val = path.split('.').reduce<unknown>((o, k) => (o && typeof o === 'object' ? (o as Record<string, unknown>)[k] : undefined), obj);
    return val != null ? String(val) : null;
  }

  const resolved: Record<string, string | null> = {};

  // Pass 1: json
  for (const r of rows) {
    if (r.dataSourceType === 'json') {
      resolved[r.placeholder] = r.sourcePath ? dotGet(r.sourcePath, data) : null;
    }
  }
  // Pass 1b: sql
  for (const r of rows) {
    if (r.dataSourceType === 'sql') {
      resolved[r.placeholder] = r.datasetAlias ? '[DB result]' : null;
    }
  }
  // Pass 2: mathExpression (simple eval, demo only)
  for (const r of rows) {
    if (r.mathExpression) {
      let expr = r.mathExpression;
      for (const [k, v] of Object.entries(resolved)) {
        if (v != null) expr = expr.replace(new RegExp(`\\b${k}\\b`, 'g'), v);
      }
      try {
        // eslint-disable-next-line no-new-func
        const res = Function('"use strict";return (' + expr + ')')();
        resolved[r.placeholder] = typeof res === 'number' ? String(+res.toFixed(4)) : String(res);
      } catch { resolved[r.placeholder] = null; }
    }
  }

  return rows.map(r => ({
    ph: r.placeholder, phType: r.placeholderType,
    ds: (r.dataSourceType ?? 'json') as DataSourceType,
    value: resolved[r.placeholder] ?? null,
    transform: r.transform ?? null,
  }));
}

// ─────────────────────────────────────────────────────────────────────────────
// SortableMappingRow
// ─────────────────────────────────────────────────────────────────────────────
interface RowProps {
  row: MappingRow;
  idx: number;
  sqlExpanded: boolean;
  fxExpanded: boolean;
  templateDatasets: TemplateDatasetDto[];
  onField: (id: string, field: string, value: unknown) => void;
  onDelete: (id: string) => void;
  onToggleSql: (id: string) => void;
  onToggleFx: (id: string) => void;
}

function SortableMappingRow({ row, idx, sqlExpanded, fxExpanded, templateDatasets, onField, onDelete, onToggleSql, onToggleFx }: RowProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: row._id });
  const style = { transform: CSS.Transform.toString(transform), transition };
  const ds = (row.dataSourceType ?? 'json') as DataSourceType;
  const pt = PT_MAP[row.placeholderType] ?? PT_MAP.text;
  const isText = row.placeholderType === 'text';
  const isSql = ds === 'sql';
  const hasFx = !!row.mathExpression;

  const aliasOpts = [
    { value: '', label: '— เลือก Alias —' },
    ...templateDatasets.map(td => ({ value: td.alias, label: `${td.alias} (${td.datasetName})` })),
  ];

  return (
    <Fragment>
      {/* ── Data row ── */}
      <tr
        ref={setNodeRef}
        style={style}
        className={cn(
          'border-b border-border/50 group transition-colors',
          isDragging ? 'opacity-40 bg-primary/5 shadow-md' : 'hover:bg-slate-50/60',
        )}
      >
        {/* Drag handle */}
        <td className="pl-3 pr-1 w-7 align-middle">
          <span
            {...attributes}
            {...listeners}
            className="flex items-center text-textMuted opacity-0 group-hover:opacity-100 cursor-grab active:cursor-grabbing transition-opacity select-none"
          >
            <GripVertical className="w-3.5 h-3.5" />
          </span>
        </td>

        {/* # */}
        <td className="px-1 w-8 text-center text-[11px] text-textMuted tabular-nums align-middle">{idx + 1}</td>

        {/* Placeholder + phType */}
        <td className="px-2 w-44 align-middle">
          <div className="flex flex-col gap-1">
            {/* Editable placeholder — styled as monospace tag */}
            <div className="flex items-center gap-0.5">
              <span className="text-[11px] text-primary font-mono select-none">{'{{' }</span>
              <input
                type="text"
                value={row.placeholder}
                onChange={e => onField(row._id, 'placeholder', e.target.value)}
                placeholder="placeholder"
                className="font-mono text-[11.5px] font-medium text-primary bg-transparent border-none outline-none flex-1 min-w-0 focus:bg-primary/5 rounded px-0.5"
              />
              <span className="text-[11px] text-primary font-mono select-none">{'}}'}</span>
            </div>
            {/* phType dot + select */}
            <div className="flex items-center gap-1.5">
              <span className="w-[5px] h-[5px] rounded-full flex-shrink-0" style={{ background: pt.color }} />
              <select
                value={row.placeholderType}
                onChange={e => onField(row._id, 'placeholderType', e.target.value)}
                className="appearance-none bg-transparent border-none text-[10.5px] text-textMuted cursor-pointer outline-none font-medium p-0 hover:text-textPrimary transition-colors"
              >
                {PH_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>
          </div>
        </td>

        {/* DataSourceType pill */}
        <td className="px-2 w-24 align-middle">
          <div className="relative inline-flex items-center">
            <select
              value={ds}
              onChange={e => onField(row._id, 'dataSourceType', e.target.value)}
              className={cn(
                'appearance-none pl-2 pr-5 py-0.5 rounded-[4px] text-[11px] font-semibold border cursor-pointer outline-none transition-colors',
                dsPillCls(ds),
              )}
            >
              <option value="json">JSON</option>
              <option value="sql">SQL</option>
            </select>
            <svg className="absolute right-1.5 top-1/2 -translate-y-1/2 pointer-events-none" width="7" height="5" viewBox="0 0 7 5" fill="none">
              <path d="M.5.5 3.5 4.5 6.5.5" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
            </svg>
          </div>
        </td>

        {/* Source / Formula / SQL config */}
        <td className="px-2 w-52 align-middle">
          {isSql ? (
            <button
              type="button"
              onClick={() => onToggleSql(row._id)}
              className="inline-flex items-center gap-1.5 text-[11px] font-medium text-amber-700 bg-amber-50 border border-amber-200 rounded px-2 py-1 cursor-pointer hover:bg-amber-100 transition-colors"
            >
              <Database className="w-3 h-3" />
              {sqlExpanded ? 'ซ่อน SQL' : 'กำหนด SQL'}
              {sqlExpanded ? <ChevronDown className="w-2.5 h-2.5" /> : <ChevronRight className="w-2.5 h-2.5" />}
            </button>
          ) : (
            <input
              type="text"
              value={row.sourcePath}
              placeholder={DS_PLACEHOLDER[ds]}
              onChange={e => onField(row._id, 'sourcePath', e.target.value)}
              className="w-full font-mono text-[11.5px] px-2 py-1 bg-transparent border border-transparent rounded hover:border-border focus:border-primary focus:bg-surface focus:shadow-[0_0_0_2px_rgba(37,99,235,.08)] outline-none transition-colors text-textPrimary placeholder:text-textMuted"
            />
          )}
        </td>

        {/* Label */}
        <td className="px-2 w-40 align-middle">
          <input
            type="text"
            value={row.label}
            placeholder="ชื่อฟิลด์ภาษาไทย"
            onChange={e => onField(row._id, 'label', e.target.value)}
            className="w-full text-xs px-2 py-1 bg-transparent border border-transparent rounded hover:border-border focus:border-primary focus:bg-surface focus:shadow-[0_0_0_2px_rgba(37,99,235,.08)] outline-none transition-colors text-textPrimary placeholder:text-textMuted"
          />
        </td>

        {/* Transform (disabled for non-text phType) */}
        <td className="px-2 w-44 align-middle">
          <div className="relative">
            <select
              value={row.transform ?? ''}
              disabled={!isText}
              onChange={e => onField(row._id, 'transform', e.target.value || null)}
              className={cn(
                'w-full appearance-none text-[11.5px] px-2 py-1 pr-6 bg-transparent border border-transparent rounded outline-none transition-colors',
                isText
                  ? 'hover:border-border focus:border-primary focus:bg-surface cursor-pointer text-textPrimary'
                  : 'opacity-35 cursor-not-allowed text-textMuted',
              )}
              title={!isText ? 'Transform ใช้ได้เฉพาะประเภท ข้อความ' : undefined}
            >
              {TRANSFORM_OPTIONS.map(t => (
                <option key={t.value} value={t.value}>{t.label}</option>
              ))}
            </select>
            {isText && (
              <svg className="absolute right-2 top-1/2 -translate-y-1/2 pointer-events-none text-textMuted" width="10" height="6" viewBox="0 0 10 6" fill="none">
                <path d="M1 1l4 4 4-4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
              </svg>
            )}
          </div>
        </td>

        {/* Required */}
        <td className="px-2 w-14 align-middle text-center">
          <button
            type="button"
            role="checkbox"
            aria-checked={row.required}
            onClick={() => onField(row._id, 'required', !row.required)}
            className={cn(
              'w-[17px] h-[17px] rounded border-2 flex items-center justify-center transition-colors mx-auto cursor-pointer focus-visible:ring-2 focus-visible:ring-primary',
              row.required ? 'bg-primary border-primary' : 'bg-transparent border-border hover:border-primary/50',
            )}
          >
            {row.required && (
              <svg width="9" height="7" viewBox="0 0 9 7" fill="none">
                <path d="M1 3.5l2.5 2L8 1" stroke="white" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            )}
          </button>
        </td>

        {/* fx + Delete */}
        <td className="pr-3 pl-1 w-16 align-middle text-center">
          <div className="flex items-center gap-0.5 justify-center">
            <button
              type="button"
              onClick={() => onToggleFx(row._id)}
              title="สูตรคำนวณ"
              className={cn(
                'p-1.5 rounded text-[10px] font-bold transition-all cursor-pointer',
                hasFx || fxExpanded
                  ? 'text-violet-700 bg-violet-50 opacity-100'
                  : 'text-textMuted opacity-0 group-hover:opacity-100 hover:text-violet-600 hover:bg-violet-50',
              )}
            >
              fx
            </button>
            <button
              type="button"
              onClick={() => onDelete(row._id)}
              title="ลบ"
              className="p-1.5 rounded text-textMuted opacity-0 group-hover:opacity-100 hover:text-red-600 hover:bg-red-50 transition-all cursor-pointer"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        </td>
      </tr>

      {/* ── Math expression expand row ── */}
      {fxExpanded && (
        <tr className="border-b border-violet-100">
          <td colSpan={9} className="p-0">
            <div className="bg-violet-50/60 border-y border-violet-100 px-4 py-3">
              <div className="flex items-center gap-2 mb-2 text-[11px] font-semibold text-violet-700">
                <span className="font-mono font-bold">fx</span>
                <span>สูตรคำนวณ — <span className="font-mono">{'{{' + (row.placeholder || '…') + '}}'}</span></span>
              </div>
              <input
                type="text"
                value={row.mathExpression ?? ''}
                onChange={e => onField(row._id, 'mathExpression', e.target.value || null)}
                placeholder="เช่น qty * unitPrice หรือ total_amt * 0.07"
                className="w-full font-mono text-[11.5px] px-2 py-1.5 bg-surface border border-violet-200 rounded text-textPrimary outline-none focus:border-violet-400"
              />
              <p className="text-[10px] text-textMuted mt-1">
                คำนวณหลังจาก resolve json/sql ทั้งหมดแล้ว — ใช้ชื่อ placeholder อื่นเป็นตัวแปรได้
              </p>
            </div>
          </td>
        </tr>
      )}

      {/* ── SQL expand row ── */}
      {isSql && sqlExpanded && (
        <tr className="border-b border-amber-100">
          <td colSpan={9} className="p-0">
            <div className="bg-amber-50/70 border-y border-amber-100 px-4 py-3">
              <div className="flex items-center gap-2 mb-3 text-[11px] font-semibold text-amber-700">
                <Database className="w-3.5 h-3.5" />
                <span>SQL Dataset — <span className="font-mono">{'{{' + (row.placeholder || '…') + '}}'}</span></span>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-[10.5px] font-semibold text-amber-600 block mb-1.5">Dataset</label>
                  <select
                    value={row.datasetAlias ?? ''}
                    onChange={e => onField(row._id, 'datasetAlias', e.target.value || null)}
                    className="w-full text-xs px-2 py-1.5 bg-surface border border-amber-200 rounded text-textPrimary outline-none focus:border-amber-400 cursor-pointer"
                  >
                    {aliasOpts.map(opt => <option key={opt.value} value={opt.value}>{opt.label}</option>)}
                  </select>
                  {templateDatasets.length === 0 && (
                    <p className="text-[10px] text-amber-600 mt-1">
                      ยังไม่มี Dataset ที่กำหนดให้ Template นี้ — ตั้งค่าได้ที่หน้า &ldquo;Dataset ของ Template&rdquo;
                    </p>
                  )}
                </div>
                <div>
                  <label className="text-[10.5px] font-semibold text-amber-600 block mb-1.5">
                    Result Path
                    <span className="ml-1.5 text-textMuted font-normal">(ไม่บังคับ)</span>
                  </label>
                  <input
                    type="text"
                    value={row.resultPath ?? ''}
                    onChange={e => onField(row._id, 'resultPath', e.target.value || null)}
                    placeholder="0.amount หรือ rows.0.total"
                    className="w-full font-mono text-[11px] px-2 py-1.5 bg-surface border border-amber-200 rounded text-textPrimary outline-none focus:border-amber-400"
                  />
                  <p className="text-[10px] text-textMuted mt-1">
                    dot-notation เข้าถึง JSON result — เว้นว่างเพื่อใช้ค่าแรกโดยอัตโนมัติ
                  </p>
                </div>
              </div>
            </div>
          </td>
        </tr>
      )}
    </Fragment>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// PreviewPanel
// ─────────────────────────────────────────────────────────────────────────────
interface PreviewPanelProps {
  rows: MappingRow[];
  onClose: () => void;
  onPdfPreview: () => void;
  previewing: boolean;
}

function PreviewPanel({ rows, onClose, onPdfPreview, previewing }: PreviewPanelProps) {
  const [jsonText, setJsonText] = useState('{\n  "contractNo": "SMK-2026-001",\n  "customerName": "นายสมชาย ใจดี",\n  "payment": { "subtotal": 2500000 }\n}');
  const [results, setResults] = useState<PreviewResult[]>([]);
  const [ran, setRan] = useState(false);

  const run = useCallback(() => {
    setResults(simulatePreview(rows, jsonText));
    setRan(true);
  }, [rows, jsonText]);

  // auto-run on open
  useEffect(() => { run(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const dsColor = (ds: DataSourceType) =>
    ds === 'json' ? 'text-teal-600' : 'text-amber-600';

  return (
    <aside className="w-72 flex-shrink-0 border-l border-border bg-surface flex flex-col overflow-hidden sticky top-0 max-h-screen">
      {/* Header */}
      <div className="px-3 py-2.5 border-b border-border flex items-center justify-between">
        <div className="flex items-center gap-2 text-xs font-semibold text-textPrimary">
          <Eye className="w-3.5 h-3.5 text-cyan-600" />
          <span>จำลองผลลัพธ์</span>
        </div>
        <button
          onClick={onClose}
          className="p-1 rounded text-textMuted hover:text-textPrimary hover:bg-surfaceSubtle transition-colors"
        >
          <EyeOff className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* JSON input */}
      <div className="px-3 pt-2.5 pb-2 border-b border-border/60">
        <label className="text-[10.5px] font-semibold text-textMuted block mb-1.5">ข้อมูลตัวอย่าง (JSON)</label>
        <textarea
          value={jsonText}
          onChange={e => setJsonText(e.target.value)}
          rows={5}
          spellCheck={false}
          className="w-full font-mono text-[10.5px] px-2 py-1.5 bg-surfaceSubtle border border-border rounded resize-none outline-none focus:border-primary text-textPrimary"
        />
        <div className="flex gap-2 mt-2">
          <button
            onClick={run}
            className="flex-1 flex items-center justify-center gap-1.5 text-[11px] font-medium py-1 rounded border border-border bg-surface hover:bg-surfaceSubtle transition-colors text-textPrimary"
          >
            <RefreshCw className="w-3 h-3" />
            รันจำลอง
          </button>
          <button
            onClick={onPdfPreview}
            disabled={previewing}
            className="flex-1 flex items-center justify-center gap-1.5 text-[11px] font-medium py-1 rounded bg-primary text-white hover:bg-primary/90 disabled:opacity-50 transition-colors"
          >
            {previewing
              ? <RefreshCw className="w-3 h-3 animate-spin" />
              : <Eye className="w-3 h-3" />}
            PDF
          </button>
        </div>
      </div>

      {/* Results list */}
      <div className="flex-1 overflow-y-auto divide-y divide-border/50">
        {!ran ? (
          <p className="text-[11px] text-textMuted text-center py-8">กด &ldquo;รันจำลอง&rdquo; เพื่อดูผลลัพธ์</p>
        ) : results.length === 0 ? (
          <p className="text-[11px] text-textMuted text-center py-8">ไม่มีฟิลด์</p>
        ) : results.map(r => {
          const ptCfg = PT_MAP[r.phType] ?? PT_MAP.text;
          const ok = r.value != null;
          return (
            <div key={r.ph} className="flex items-start gap-2 px-3 py-2">
              <span className="flex-shrink-0 mt-0.5">
                {ok
                  ? <CheckCircle className="w-3.5 h-3.5 text-emerald-500" />
                  : <XCircle className="w-3.5 h-3.5 text-textMuted" />}
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-1.5 flex-wrap">
                  <span className="font-mono text-[10px] text-textMuted">{`{{${r.ph}}}`}</span>
                  <span className="text-[9.5px] font-semibold" style={{ color: ptCfg.color }}>{ptCfg.label}</span>
                </div>
                {ok
                  ? <p className="text-[11px] font-medium text-textPrimary truncate mt-0.5">{r.value}</p>
                  : <p className="text-[11px] text-textMuted italic mt-0.5">ไม่พบค่า</p>}
              </div>
              <span className={cn('text-[9.5px] font-semibold mt-0.5 flex-shrink-0', dsColor(r.ds))}>{r.ds}</span>
            </div>
          );
        })}
      </div>
    </aside>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// FieldMappingView — main component
// ─────────────────────────────────────────────────────────────────────────────
export interface FieldMappingViewProps {
  initialTemplateId?: string;
}

export const FieldMappingView: React.FC<FieldMappingViewProps> = ({ initialTemplateId }) => {
  const { templates, loading: templatesLoading } = useTemplates();
  const [selectedTemplateId, setSelectedTemplateId] = useState(initialTemplateId ?? '');
  const [rows, setRows] = useState<MappingRow[]>([]);
  const [templateDatasets, setTemplateDatasets] = useState<TemplateDatasetDto[]>([]);
  const [expandedSql, setExpandedSql] = useState<Set<string>>(new Set());
  const [expandedFx, setExpandedFx] = useState<Set<string>>(new Set());
  const [showPreview, setShowPreview] = useState(true);
  const [isDirty, setIsDirty] = useState(false);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [savedOk, setSavedOk] = useState(false);
  const [previewing, setPreviewing] = useState(false);

  // @dnd-kit sensors
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  // Load template's alias assignments when template changes
  useEffect(() => {
    if (!selectedTemplateId) return;
    getTemplateDatasets(selectedTemplateId)
      .then(d => setTemplateDatasets(d ?? []))
      .catch(() => setTemplateDatasets([]));
  }, [selectedTemplateId]);

  // Select first template
  useEffect(() => {
    if (initialTemplateId) {
      setSelectedTemplateId(initialTemplateId);
    } else if (templates.length > 0 && !selectedTemplateId) {
      setSelectedTemplateId(templates[0].id);
    }
  }, [initialTemplateId, templates, selectedTemplateId]);

  // Load mappings
  const loadMappings = useCallback(async (templateId: string) => {
    if (!templateId) return;
    setLoading(true);
    setIsDirty(false);
    try {
      const data = await templatesApi.getTemplateMappings(templateId);
      setRows(data.map(m => ({
        _id: genId(),
        placeholder: m.placeholder,
        placeholderType: 'text' as PlaceholderType,
        sourcePath: m.sourcePath,
        label: m.label,
        required: m.required,
        defaultValue: m.defaultValue ?? null,
        transform: m.transform ?? null,
        sortOrder: m.sortOrder,
        dataSourceType: (m.dataSourceType ?? 'json') as DataSourceType,
        datasetAlias: m.datasetAlias ?? null,
        resultPath: m.resultPath ?? null,
        mathExpression: m.mathExpression ?? null,
      })));
    } catch {
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (selectedTemplateId) loadMappings(selectedTemplateId);
  }, [selectedTemplateId, loadMappings]);

  // ── Handlers ─────────────────────────────────────────────────────────────
  const handleField = useCallback((id: string, field: string, value: unknown) => {
    setRows(prev => prev.map(r => {
      if (r._id !== id) return r;
      const updated = { ...r, [field]: value };
      // Switching away from sql → collapse & clear sql fields
      if (field === 'dataSourceType' && value !== 'sql') {
        setExpandedSql(s => { const n = new Set(s); n.delete(id); return n; });
        updated.datasetAlias = null;
        updated.resultPath = null;
        updated.sourcePath = '';
      }
      if (field === 'dataSourceType' && value === 'sql') {
        setExpandedSql(s => new Set([...s, id]));
        updated.sourcePath = '';
      }
      // Switching phType to non-text → clear transform
      if (field === 'placeholderType' && value !== 'text') {
        updated.transform = null;
      }
      return updated;
    }));
    setIsDirty(true);
  }, []);

  const handleDelete = useCallback((id: string) => {
    setRows(prev => prev.filter(r => r._id !== id));
    setExpandedSql(s => { const n = new Set(s); n.delete(id); return n; });
    setExpandedFx(s => { const n = new Set(s); n.delete(id); return n; });
    setIsDirty(true);
  }, []);

  const handleToggleSql = useCallback((id: string) => {
    setExpandedSql(s => {
      const n = new Set(s);
      if (n.has(id)) n.delete(id); else n.add(id);
      return n;
    });
  }, []);

  const handleToggleFx = useCallback((id: string) => {
    setExpandedFx(s => {
      const n = new Set(s);
      if (n.has(id)) n.delete(id); else n.add(id);
      return n;
    });
  }, []);

  const handleDragEnd = useCallback((event: DragEndEvent) => {
    const { active, over } = event;
    if (over && active.id !== over.id) {
      setRows(prev => {
        const oldIdx = prev.findIndex(r => r._id === active.id);
        const newIdx = prev.findIndex(r => r._id === over.id);
        return arrayMove(prev, oldIdx, newIdx);
      });
      setIsDirty(true);
    }
  }, []);

  const handleAddRow = () => {
    const row = newRow(rows.length + 1);
    setRows(prev => [...prev, row]);
    setIsDirty(true);
  };

  const handleSave = async () => {
    if (!selectedTemplateId) return;
    setSaving(true);
    setSavedOk(false);
    try {
      const items: SaveFieldMappingItem[] = rows.map(({ _id, placeholderType, ...rest }, i) => ({
        ...rest,
        sortOrder: i,
      }));
      await templatesApi.saveTemplateMappings(selectedTemplateId, items);
      setIsDirty(false);
      setSavedOk(true);
      setTimeout(() => setSavedOk(false), 3000);
    } catch (err: unknown) {
      alert((err as Error).message || 'บันทึก Mapping ล้มเหลว');
    } finally {
      setSaving(false);
    }
  };

  const handlePdfPreview = async () => {
    if (!selectedTemplateId) return;
    setPreviewing(true);
    try {
      const blob = await templatesApi.previewMappings(selectedTemplateId, {});
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank', 'noopener,noreferrer');
      setTimeout(() => URL.revokeObjectURL(url), 10000);
    } catch { /* noop */ } finally {
      setPreviewing(false);
    }
  };

  // ── Derived ──────────────────────────────────────────────────────────────
  const unmapped = rows.filter(r =>
    (r.dataSourceType === 'sql' && !r.datasetAlias) ||
    (r.dataSourceType !== 'sql' && !r.sourcePath.trim())
  ).length;
  const templateOptions = templates.map(t => ({ value: t.id, label: `${t.name} (${t.slug})` }));
  const selectedTemplate = templates.find(t => t.id === selectedTemplateId);
  const isHtml = selectedTemplate?.fileFormat === 'html' || Boolean(selectedTemplate?.name?.toLowerCase().endsWith('.html'));

  // ── Render ───────────────────────────────────────────────────────────────
  return (
    <div className="space-y-0">
      {/* ── Toolbar ── */}
      <div className="bg-surface border border-border rounded-sm mb-3 p-3 flex items-center justify-between flex-wrap gap-2">
        {/* Left */}
        <div className="flex items-center gap-2.5 flex-wrap">
          <div className="flex items-center gap-2">
            <div className="w-7 h-7 rounded-sm bg-cyan-50 text-cyan-600 flex items-center justify-center border border-cyan-200 flex-shrink-0">
              <GitFork className="w-3.5 h-3.5" />
            </div>
            <div className="w-64">
              <Select
                options={templateOptions}
                value={selectedTemplateId}
                onChange={e => { setSelectedTemplateId(e.target.value); setIsDirty(false); }}
                disabled={templatesLoading}
              />
            </div>
          </div>

          {/* Unmapped badge */}
          {unmapped > 0 && (
            <div className="flex items-center gap-1 text-[11px] font-semibold text-amber-700 bg-amber-50 border border-amber-200 rounded-sm px-2.5 py-0.5">
              <AlertTriangle className="w-3 h-3" />
              {unmapped} ยังไม่มี Source
            </div>
          )}

          {/* Total badge */}
          <span className="text-[11px] text-textMuted bg-surfaceSubtle border border-border px-2 py-0.5 rounded-sm">
            {rows.length} ฟิลด์
          </span>
        </div>

        {/* Right */}
        <div className="flex items-center gap-2">
          {/* Saved indicator */}
          {savedOk && (
            <div className="flex items-center gap-1 text-[11px] font-semibold text-emerald-700">
              <CheckCircle className="w-3.5 h-3.5" />
              บันทึกแล้ว
            </div>
          )}

          {/* Refresh */}
          <button
            onClick={() => { if (selectedTemplateId) loadMappings(selectedTemplateId); }}
            className="p-1.5 rounded text-textMuted border border-border hover:bg-surfaceSubtle transition-colors"
            title="รีโหลด"
          >
            <RefreshCw className={cn('w-3.5 h-3.5', loading && 'animate-spin')} />
          </button>

          {/* Preview toggle */}
          <button
            onClick={() => setShowPreview(p => !p)}
            className={cn(
              'flex items-center gap-1.5 text-[11.5px] font-medium px-2.5 py-1.5 rounded border transition-colors',
              showPreview
                ? 'bg-cyan-50 text-cyan-700 border-cyan-200'
                : 'bg-surface text-textMuted border-border hover:bg-surfaceSubtle',
            )}
          >
            {showPreview ? <Eye className="w-3.5 h-3.5" /> : <EyeOff className="w-3.5 h-3.5" />}
            {showPreview ? 'ซ่อนผล' : 'จำลองผล'}
          </button>

          {/* Add row */}
          <Button variant="outline" size="sm" icon={Plus} onClick={handleAddRow}>
            เพิ่มฟิลด์
          </Button>

          {/* Save */}
          <button
            onClick={handleSave}
            disabled={saving}
            className={cn(
              'flex items-center gap-1.5 text-[11.5px] font-semibold px-3 py-1.5 rounded transition-colors',
              isDirty
                ? 'bg-emerald-600 hover:bg-emerald-700 text-white'
                : 'bg-primary hover:bg-primary/90 text-white',
              saving && 'opacity-60',
            )}
          >
            {saving
              ? <RefreshCw className="w-3.5 h-3.5 animate-spin" />
              : <Save className="w-3.5 h-3.5" />}
            {isDirty ? 'บันทึก *' : 'บันทึก'}
          </button>
        </div>
      </div>

      {/* HTML Direct Handlebars Notice */}
      {isHtml && (
        <div className="mb-3 px-3.5 py-2.5 bg-sky-50/90 border border-sky-200 rounded-sm text-sky-800 text-xs flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <span className="w-1.5 h-1.5 rounded-full bg-sky-500 shrink-0" />
            <span className="text-[11.5px] leading-relaxed">
              <strong className="text-sky-900">แม่แบบประเภท HTML (Direct Handlebars):</strong> แม่แบบนี้สามารถรับ JSON ตรงผ่าน API เข้าไปเรนเดอร์ได้ทันที การกำหนดฟิลด์ด้านล่างจำเป็นเฉพาะเมื่อต้องการเชื่อมกับ SQL Dataset เท่านั้น
            </span>
          </div>
          <span className="text-[10px] font-mono font-medium text-sky-700 bg-sky-100 px-2 py-0.5 rounded-[2px] shrink-0 border border-sky-200/60">
            Optional for HTML
          </span>
        </div>
      )}

      {/* ── Unmapped warning banner ── */}
      {unmapped > 0 && (
        <div className="flex items-center gap-2 px-3 py-2 bg-amber-50 border border-amber-200 rounded-sm mb-3 text-[11.5px] text-amber-800">
          <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0 text-amber-600" />
          <strong>{unmapped} ฟิลด์ยังไม่มี Source Path</strong>
          <span className="text-amber-700"> — ระบบจะใช้ค่าเริ่มต้นแทน ตรวจสอบก่อนบันทึก</span>
        </div>
      )}

      {/* ── Main: table + preview panel ── */}
      <div className="flex border border-border rounded-sm bg-surface overflow-hidden">

        {/* Table panel */}
        <div className="flex-1 min-w-0 flex flex-col">
          {loading ? (
            <div className="py-16 flex items-center justify-center gap-2 text-xs text-textMuted">
              <RefreshCw className="w-4 h-4 animate-spin" />
              กำลังโหลดฟิลด์...
            </div>
          ) : rows.length === 0 ? (
            <div className="py-16 flex flex-col items-center gap-3 text-center">
              <GitFork className="w-8 h-8 text-textMuted" />
              <div>
                <p className="text-xs font-semibold text-textPrimary">ยังไม่มี Field Mapping</p>
                <p className="text-[11px] text-textMuted mt-1">คลิก &ldquo;เพิ่มฟิลด์&rdquo; เพื่อเริ่มกำหนด Mapping</p>
              </div>
              <Button variant="outline" size="sm" icon={Plus} onClick={handleAddRow}>เพิ่มฟิลด์แรก</Button>
            </div>
          ) : (
            <>
              {/* Scrollable table */}
              <div className="overflow-x-auto">
                <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
                  <SortableContext items={rows.map(r => r._id)} strategy={verticalListSortingStrategy}>
                    <table className="w-full text-left border-collapse" style={{ minWidth: 860 }}>
                      <thead>
                        <tr className="border-b-2 border-border bg-surfaceSubtle sticky top-0 z-10">
                          <th className="pl-3 pr-1 py-2 w-7" />
                          <th className="px-1 py-2 w-8 text-center text-[10.5px] font-semibold uppercase tracking-wide text-textMuted">#</th>
                          <th className="px-2 py-2 w-44 text-[10.5px] font-semibold uppercase tracking-wide text-textMuted">ตัวแปร</th>
                          <th className="px-2 py-2 w-24 text-[10.5px] font-semibold uppercase tracking-wide text-textMuted">แหล่งข้อมูล</th>
                          <th className="px-2 py-2 w-52 text-[10.5px] font-semibold uppercase tracking-wide text-textMuted">ค่า / สูตร</th>
                          <th className="px-2 py-2 w-40 text-[10.5px] font-semibold uppercase tracking-wide text-textMuted">ชื่อฟิลด์</th>
                          <th className="px-2 py-2 w-44 text-[10.5px] font-semibold uppercase tracking-wide text-textMuted">แปลงค่า</th>
                          <th className="px-2 py-2 w-14 text-center text-[10.5px] font-semibold uppercase tracking-wide text-textMuted">จำเป็น</th>
                          <th className="pr-3 pl-1 w-10" />
                        </tr>
                      </thead>
                      <tbody>
                        {rows.map((row, idx) => (
                          <SortableMappingRow
                            key={row._id}
                            row={row}
                            idx={idx}
                            sqlExpanded={expandedSql.has(row._id)}
                            fxExpanded={expandedFx.has(row._id)}
                            templateDatasets={templateDatasets}
                            onField={handleField}
                            onDelete={handleDelete}
                            onToggleSql={handleToggleSql}
                            onToggleFx={handleToggleFx}
                          />
                        ))}
                      </tbody>
                    </table>
                  </SortableContext>
                </DndContext>
              </div>

              {/* Footer */}
              <div className="border-t border-border px-3 py-2 flex items-center gap-3 bg-surface">
                <button
                  onClick={handleAddRow}
                  className="inline-flex items-center gap-1.5 text-[12px] text-textMuted hover:text-primary hover:bg-primary/5 px-2 py-1 rounded transition-colors"
                >
                  <Plus className="w-3.5 h-3.5" />
                  เพิ่มฟิลด์
                </button>
                <span className="text-[11px] text-textMuted tabular-nums">{rows.length} ฟิลด์</span>
                <span className="ml-auto text-[10.5px] text-textMuted opacity-60">ลากแถวเพื่อเรียงลำดับ</span>
              </div>
            </>
          )}
        </div>

        {/* Preview panel */}
        {showPreview && rows.length > 0 && (
          <PreviewPanel
            rows={rows}
            onClose={() => setShowPreview(false)}
            onPdfPreview={handlePdfPreview}
            previewing={previewing}
          />
        )}
      </div>
    </div>
  );
};

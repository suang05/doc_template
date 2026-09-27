'use client';

import { Fragment } from 'react';
import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { GripVertical, Trash2, Database, ChevronDown, ChevronRight } from 'lucide-react';
import type { DataSourceType } from '@/types/api';
import type { TemplateDatasetDto } from '@/types/api';
import { type MappingRow, PH_TYPES, PT_MAP } from '@/lib/mapping/mappingMapper';
import { ThaiTransformTypes } from '@/types/api';
import { cn } from '@/utils/cn';

const DS_PLACEHOLDER: Record<DataSourceType, string> = {
  json: 'เช่น payment.amount',
  sql:  '',
};

function dsPillCls(ds: DataSourceType) {
  if (ds === 'json') return 'bg-teal-50 text-teal-700 border-teal-200 hover:border-teal-300';
  return 'bg-amber-50 text-amber-700 border-amber-200 hover:border-amber-300';
}

const TRANSFORM_OPTIONS = [
  { value: '', label: '— ไม่แปลง —' },
  ...ThaiTransformTypes.map(t => ({ value: t.value, label: t.label })),
];

export interface RowProps {
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

export function SortableMappingRow({
  row, idx, sqlExpanded, fxExpanded, templateDatasets, onField, onDelete, onToggleSql, onToggleFx,
}: RowProps) {
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
      <tr
        ref={setNodeRef}
        style={style}
        className={cn(
          'border-b border-border/50 group transition-colors',
          isDragging ? 'opacity-40 bg-primary/5 shadow-md' : 'hover:bg-slate-50/60',
        )}
      >
        <td className="pl-3 pr-1 w-7 align-middle">
          <span
            {...attributes}
            {...listeners}
            className="flex items-center text-textMuted opacity-0 group-hover:opacity-100 cursor-grab active:cursor-grabbing transition-opacity select-none"
          >
            <GripVertical className="w-3.5 h-3.5" />
          </span>
        </td>

        <td className="px-1 w-8 text-center text-[11px] text-textMuted tabular-nums align-middle">{idx + 1}</td>

        <td className="px-2 w-44 align-middle">
          <div className="flex flex-col gap-1">
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

        <td className="px-2 w-40 align-middle">
          <input
            type="text"
            value={row.label}
            placeholder="ชื่อฟิลด์ภาษาไทย"
            onChange={e => onField(row._id, 'label', e.target.value)}
            className="w-full text-xs px-2 py-1 bg-transparent border border-transparent rounded hover:border-border focus:border-primary focus:bg-surface focus:shadow-[0_0_0_2px_rgba(37,99,235,.08)] outline-none transition-colors text-textPrimary placeholder:text-textMuted"
          />
        </td>

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

'use client';

import { useEffect, useState } from 'react';
import type { TemplateVariable } from '@/types/api';

interface FormEditorProps {
  variables: TemplateVariable[];
  onChange: (data: Record<string, unknown>) => void;
  initialData?: Record<string, unknown>;
}

interface TableRow {
  [col: string]: string;
}

// Build empty data skeleton from schema variables
function buildSkeleton(variables: TemplateVariable[]): Record<string, unknown> {
  const replace: Record<string, string> = {};
  const table: Array<{ name: string; data: TableRow[] }> = [];
  const qrcode: Record<string, { text: string }> = {};
  const barcode: Record<string, { text: string; format: string }> = {};

  for (const v of variables) {
    switch (v.type) {
      case 'text':
      case 'number':
      case 'date':
        replace[v.key] = '';
        break;
      case 'table':
        table.push({
          name: v.key,
          data: [Object.fromEntries((v.columns ?? ['value']).map(c => [c, '']))],
        });
        break;
      case 'qrcode':
        qrcode[v.key] = { text: '' };
        break;
      case 'barcode':
        barcode[v.key] = { text: '', format: 'Code128' };
        break;
    }
  }

  return {
    replace,
    ...(table.length > 0 ? { table } : {}),
    ...(Object.keys(qrcode).length > 0 ? { qrcode } : {}),
    ...(Object.keys(barcode).length > 0 ? { barcode } : {}),
  };
}

export function FormEditor({ variables, onChange, initialData }: FormEditorProps) {
  const [replace, setReplace] = useState<Record<string, string>>({});
  const [tables, setTables] = useState<Array<{ name: string; data: TableRow[] }>>([]);
  const [qrcodes, setQrcodes] = useState<Record<string, string>>({});
  const [barcodes, setBarcodes] = useState<Record<string, { text: string; format: string }>>({});

  // Re-init when variables change (new template selected)
  useEffect(() => {
    const skeleton = buildSkeleton(variables);
    const init = initialData ?? skeleton;

    const r = (init.replace as Record<string, string>) ?? {};
    const t = (init.table as Array<{ name: string; data: TableRow[] }>) ?? [];
    const q = init.qrcode as Record<string, { text: string }> | undefined;
    const b = init.barcode as Record<string, { text: string; format: string }> | undefined;

    setReplace(r);
    setTables(t.length > 0 ? t : (skeleton.table as typeof t) ?? []);
    setQrcodes(Object.fromEntries(Object.entries(q ?? {}).map(([k, v]) => [k, v.text])));
    setBarcodes(b ?? {});
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [variables]);

  // Emit consolidated data upward whenever any field changes
  useEffect(() => {
    const data: Record<string, unknown> = { replace };
    if (tables.length > 0) data.table = tables;
    if (Object.keys(qrcodes).length > 0)
      data.qrcode = Object.fromEntries(Object.entries(qrcodes).map(([k, v]) => [k, { text: v }]));
    if (Object.keys(barcodes).length > 0) data.barcode = barcodes;
    onChange(data);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [replace, tables, qrcodes, barcodes]);

  const textVars = variables.filter(v => ['text', 'number', 'date'].includes(v.type));
  const tableVars = variables.filter(v => v.type === 'table');
  const qrVars = variables.filter(v => v.type === 'qrcode');
  const barVars = variables.filter(v => v.type === 'barcode');

  if (variables.length === 0) {
    return (
      <div className="flex items-center justify-center h-32 text-[var(--t3)] text-xs">
        เลือก template เพื่อดู fields
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-5 p-4 overflow-y-auto h-full">
      {/* Text / Number / Date variables */}
      {textVars.length > 0 && (
        <section>
          <p className="text-[10px] font-semibold text-[var(--t3)] uppercase tracking-wide mb-2">ตัวแปรข้อความ</p>
          <div className="flex flex-col gap-2">
            {textVars.map(v => (
              <div key={v.key} className="flex items-center gap-2">
                <label className="text-xs text-[var(--t2)] w-36 flex-none font-mono truncate" title={`{{${v.key}}}`}>
                  {`{{${v.key}}}`}
                </label>
                <input
                  type={v.type === 'number' ? 'number' : v.type === 'date' ? 'date' : 'text'}
                  value={replace[v.key] ?? ''}
                  onChange={e => setReplace(prev => ({ ...prev, [v.key]: e.target.value }))}
                  placeholder={v.label ?? v.key}
                  className="flex-1 text-xs px-2 py-1.5 rounded border border-[var(--border)] bg-[var(--surf)] text-[var(--t1)] focus:outline-none focus:border-[var(--accent)]"
                />
              </div>
            ))}
          </div>
        </section>
      )}

      {/* Table variables */}
      {tableVars.map(v => {
        const tIdx = tables.findIndex(t => t.name === v.key);
        const tbl = tables[tIdx] ?? { name: v.key, data: [] };
        const cols = v.columns ?? (tbl.data[0] ? Object.keys(tbl.data[0]) : ['value']);

        const updateRow = (rowIdx: number, col: string, val: string) => {
          setTables(prev => {
            const next = prev.map(t => t.name !== v.key ? t : {
              ...t,
              data: t.data.map((r, i) => i === rowIdx ? { ...r, [col]: val } : r),
            });
            return tIdx === -1 ? [...next, { name: v.key, data: [{ [col]: val }] }] : next;
          });
        };

        const addRow = () => setTables(prev => {
          const empty = Object.fromEntries(cols.map(c => [c, '']));
          if (tIdx === -1) return [...prev, { name: v.key, data: [empty] }];
          return prev.map(t => t.name !== v.key ? t : { ...t, data: [...t.data, empty] });
        });

        const removeRow = (rowIdx: number) => setTables(prev =>
          prev.map(t => t.name !== v.key ? t : { ...t, data: t.data.filter((_, i) => i !== rowIdx) })
        );

        return (
          <section key={v.key}>
            <div className="flex items-center justify-between mb-2">
              <p className="text-[10px] font-semibold text-[var(--t3)] uppercase tracking-wide">
                ตาราง: <span className="font-mono text-[var(--accent)]">{`{{row:${v.key}}}`}</span>
              </p>
              <button
                onClick={addRow}
                className="text-[10px] px-2 py-0.5 rounded border border-[var(--border)] text-[var(--t2)] hover:text-[var(--t1)] transition-colors"
              >
                + เพิ่มแถว
              </button>
            </div>
            <div className="overflow-x-auto rounded border border-[var(--border)]">
              <table className="w-full text-xs">
                <thead>
                  <tr className="bg-[var(--surf)]">
                    {cols.map(c => (
                      <th key={c} className="px-2 py-1.5 text-left font-medium text-[var(--t2)] border-b border-[var(--border)]">{c}</th>
                    ))}
                    <th className="w-8 border-b border-[var(--border)]" />
                  </tr>
                </thead>
                <tbody>
                  {tbl.data.length === 0 ? (
                    <tr>
                      <td colSpan={cols.length + 1} className="px-2 py-3 text-center text-[var(--t3)]">
                        ไม่มีข้อมูล — กด + เพิ่มแถว
                      </td>
                    </tr>
                  ) : tbl.data.map((row, rIdx) => (
                    <tr key={rIdx} className="border-b border-[var(--border)] last:border-0">
                      {cols.map(c => (
                        <td key={c} className="px-1 py-1">
                          <input
                            value={row[c] ?? ''}
                            onChange={e => updateRow(rIdx, c, e.target.value)}
                            className="w-full text-xs px-1.5 py-1 rounded border border-transparent hover:border-[var(--border)] focus:border-[var(--accent)] bg-transparent focus:bg-[var(--surf)] focus:outline-none"
                          />
                        </td>
                      ))}
                      <td className="px-1 py-1 text-center">
                        <button
                          onClick={() => removeRow(rIdx)}
                          className="text-[var(--t3)] hover:text-[var(--rose)] text-xs px-1"
                        >×</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        );
      })}

      {/* QR Code variables */}
      {qrVars.length > 0 && (
        <section>
          <p className="text-[10px] font-semibold text-[var(--t3)] uppercase tracking-wide mb-2">QR Code</p>
          <div className="flex flex-col gap-2">
            {qrVars.map(v => (
              <div key={v.key} className="flex items-center gap-2">
                <label className="text-xs text-[var(--t2)] w-36 flex-none font-mono truncate">{`{{qrcode:${v.key}}}`}</label>
                <input
                  value={qrcodes[v.key] ?? ''}
                  onChange={e => setQrcodes(prev => ({ ...prev, [v.key]: e.target.value }))}
                  placeholder="ข้อความหรือ URL"
                  className="flex-1 text-xs px-2 py-1.5 rounded border border-[var(--border)] bg-[var(--surf)] text-[var(--t1)] focus:outline-none focus:border-[var(--accent)]"
                />
              </div>
            ))}
          </div>
        </section>
      )}

      {/* Barcode variables */}
      {barVars.length > 0 && (
        <section>
          <p className="text-[10px] font-semibold text-[var(--t3)] uppercase tracking-wide mb-2">Barcode</p>
          <div className="flex flex-col gap-2">
            {barVars.map(v => (
              <div key={v.key} className="flex items-center gap-2">
                <label className="text-xs text-[var(--t2)] w-36 flex-none font-mono truncate">{`{{barcode:${v.key}}}`}</label>
                <input
                  value={barcodes[v.key]?.text ?? ''}
                  onChange={e => setBarcodes(prev => ({ ...prev, [v.key]: { ...prev[v.key], text: e.target.value, format: prev[v.key]?.format ?? 'Code128' } }))}
                  placeholder="ข้อความสำหรับ barcode"
                  className="flex-1 text-xs px-2 py-1.5 rounded border border-[var(--border)] bg-[var(--surf)] text-[var(--t1)] focus:outline-none focus:border-[var(--accent)]"
                />
                <select
                  value={barcodes[v.key]?.format ?? 'Code128'}
                  onChange={e => setBarcodes(prev => ({ ...prev, [v.key]: { ...prev[v.key], format: e.target.value } }))}
                  className="text-xs px-1.5 py-1.5 rounded border border-[var(--border)] bg-[var(--surf)] text-[var(--t1)] focus:outline-none"
                >
                  <option>Code128</option>
                  <option>Code39</option>
                  <option>EAN13</option>
                </select>
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

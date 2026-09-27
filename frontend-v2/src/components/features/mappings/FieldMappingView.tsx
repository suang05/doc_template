'use client';

import React, { useState } from 'react';
import {
  DndContext, closestCenter,
  KeyboardSensor, PointerSensor,
  useSensor, useSensors,
} from '@dnd-kit/core';
import { SortableContext, verticalListSortingStrategy, sortableKeyboardCoordinates } from '@dnd-kit/sortable';
import {
  Plus, Save, Eye, EyeOff,
  RefreshCw, CheckCircle, GitFork, AlertTriangle,
} from 'lucide-react';
import { useTemplates } from '@/hooks/useTemplates';
import { useFieldMapping } from '@/hooks/useFieldMapping';
import { useAutoSelectFirst } from '@/hooks/useAutoSelectFirst';
import { Button, Select } from '@/components/ui';
import { SortableMappingRow } from './SortableMappingRow';
import { MappingPreviewPanel, MAPPING_PREVIEW_DEFAULT_SAMPLE } from './MappingPreviewPanel';
import { cn } from '@/utils/cn';

export interface FieldMappingViewProps {
  initialTemplateId?: string;
}

export const FieldMappingView: React.FC<FieldMappingViewProps> = ({ initialTemplateId }) => {
  const { templates, loading: templatesLoading } = useTemplates();
  const [selectedTemplateId, setSelectedTemplateId] = useAutoSelectFirst(
    templates,
    (t) => t.id,
    initialTemplateId
  );
  const [showPreview, setShowPreview] = useState(true);
  const [previewJson, setPreviewJson] = useState(MAPPING_PREVIEW_DEFAULT_SAMPLE);

  const mapping = useFieldMapping(selectedTemplateId);

  // @dnd-kit sensors
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 5 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const selectedTemplate = templates.find(t => t.id === selectedTemplateId);
  const isHtml = selectedTemplate?.fileFormat === 'html' ||
    Boolean(selectedTemplate?.name?.toLowerCase().endsWith('.html'));
  const templateOptions = templates.map(t => ({ value: t.id, label: `${t.name} (${t.slug})` }));

  const handleSave = async () => {
    try {
      await mapping.handleSave(selectedTemplateId);
    } catch (err: unknown) {
      alert((err as Error).message || 'บันทึก Mapping ล้มเหลว');
    }
  };

  const handlePdfPreview = () => {
    let sampleData: Record<string, unknown> = {};
    try { sampleData = JSON.parse(previewJson); } catch { /* noop */ }
    mapping.handlePdfPreview(selectedTemplateId, sampleData);
  };

  return (
    <div className="space-y-0">
      {/* ── Toolbar ── */}
      <div className="bg-surface border border-border rounded-sm mb-3 p-3 flex items-center justify-between flex-wrap gap-2">
        <div className="flex items-center gap-2.5 flex-wrap">
          <div className="flex items-center gap-2">
            <div className="w-7 h-7 rounded-sm bg-cyan-50 text-cyan-600 flex items-center justify-center border border-cyan-200 flex-shrink-0">
              <GitFork className="w-3.5 h-3.5" />
            </div>
            <div className="w-64">
              <Select
                options={templateOptions}
                value={selectedTemplateId}
                onChange={e => { setSelectedTemplateId(e.target.value); }}
                disabled={templatesLoading}
              />
            </div>
          </div>

          {mapping.unmapped > 0 && (
            <div className="flex items-center gap-1 text-[11px] font-semibold text-amber-700 bg-amber-50 border border-amber-200 rounded-sm px-2.5 py-0.5">
              <AlertTriangle className="w-3 h-3" />
              {mapping.unmapped} ยังไม่มี Source
            </div>
          )}

          <span className="text-[11px] text-textMuted bg-surfaceSubtle border border-border px-2 py-0.5 rounded-sm">
            {mapping.rows.length} ฟิลด์
          </span>
        </div>

        <div className="flex items-center gap-2">
          {mapping.savedOk && (
            <div className="flex items-center gap-1 text-[11px] font-semibold text-emerald-700">
              <CheckCircle className="w-3.5 h-3.5" />
              บันทึกแล้ว
            </div>
          )}

          <button
            onClick={() => mapping.loadMappings(selectedTemplateId)}
            className="p-1.5 rounded text-textMuted border border-border hover:bg-surfaceSubtle transition-colors"
            title="รีโหลด"
          >
            <RefreshCw className={cn('w-3.5 h-3.5', mapping.loading && 'animate-spin')} />
          </button>

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

          <Button variant="outline" size="sm" icon={Plus} onClick={mapping.handleAddRow}>
            เพิ่มฟิลด์
          </Button>

          <button
            onClick={handleSave}
            disabled={mapping.saving}
            className={cn(
              'flex items-center gap-1.5 text-[11.5px] font-semibold px-3 py-1.5 rounded transition-colors',
              mapping.isDirty
                ? 'bg-emerald-600 hover:bg-emerald-700 text-white'
                : 'bg-primary hover:bg-primary/90 text-white',
              mapping.saving && 'opacity-60',
            )}
          >
            {mapping.saving
              ? <RefreshCw className="w-3.5 h-3.5 animate-spin" />
              : <Save className="w-3.5 h-3.5" />}
            {mapping.isDirty ? 'บันทึก *' : 'บันทึก'}
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

      {mapping.unmapped > 0 && (
        <div className="flex items-center gap-2 px-3 py-2 bg-amber-50 border border-amber-200 rounded-sm mb-3 text-[11.5px] text-amber-800">
          <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0 text-amber-600" />
          <strong>{mapping.unmapped} ฟิลด์ยังไม่มี Source Path</strong>
          <span className="text-amber-700"> — ระบบจะใช้ค่าเริ่มต้นแทน ตรวจสอบก่อนบันทึก</span>
        </div>
      )}

      {/* ── Main: table + preview panel ── */}
      <div className="flex border border-border rounded-sm bg-surface overflow-hidden">

        {/* Table panel */}
        <div className="flex-1 min-w-0 flex flex-col">
          {mapping.loading ? (
            <div className="py-16 flex items-center justify-center gap-2 text-xs text-textMuted">
              <RefreshCw className="w-4 h-4 animate-spin" />
              กำลังโหลดฟิลด์...
            </div>
          ) : mapping.rows.length === 0 ? (
            <div className="py-16 flex flex-col items-center gap-3 text-center">
              <GitFork className="w-8 h-8 text-textMuted" />
              <div>
                <p className="text-xs font-semibold text-textPrimary">ยังไม่มี Field Mapping</p>
                <p className="text-[11px] text-textMuted mt-1">คลิก &ldquo;เพิ่มฟิลด์&rdquo; เพื่อเริ่มกำหนด Mapping</p>
              </div>
              <Button variant="outline" size="sm" icon={Plus} onClick={mapping.handleAddRow}>เพิ่มฟิลด์แรก</Button>
            </div>
          ) : (
            <>
              <div className="overflow-x-auto">
                <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={mapping.handleDragEnd}>
                  <SortableContext items={mapping.rows.map(r => r._id)} strategy={verticalListSortingStrategy}>
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
                        {mapping.rows.map((row, idx) => (
                          <SortableMappingRow
                            key={row._id}
                            row={row}
                            idx={idx}
                            sqlExpanded={mapping.expandedSql.has(row._id)}
                            fxExpanded={mapping.expandedFx.has(row._id)}
                            templateDatasets={mapping.templateDatasets}
                            onField={mapping.handleField}
                            onDelete={mapping.handleDelete}
                            onToggleSql={mapping.handleToggleSql}
                            onToggleFx={mapping.handleToggleFx}
                          />
                        ))}
                      </tbody>
                    </table>
                  </SortableContext>
                </DndContext>
              </div>

              <div className="border-t border-border px-3 py-2 flex items-center gap-3 bg-surface">
                <button
                  onClick={mapping.handleAddRow}
                  className="inline-flex items-center gap-1.5 text-[12px] text-textMuted hover:text-primary hover:bg-primary/5 px-2 py-1 rounded transition-colors"
                >
                  <Plus className="w-3.5 h-3.5" />
                  เพิ่มฟิลด์
                </button>
                <span className="text-[11px] text-textMuted tabular-nums">{mapping.rows.length} ฟิลด์</span>
                <span className="ml-auto text-[10.5px] text-textMuted opacity-60">ลากแถวเพื่อเรียงลำดับ</span>
              </div>
            </>
          )}
        </div>

        {showPreview && mapping.rows.length > 0 && (
          <MappingPreviewPanel
            rows={mapping.rows}
            jsonText={previewJson}
            onJsonTextChange={setPreviewJson}
            previewing={mapping.previewing}
            onClose={() => setShowPreview(false)}
            onPdfPreview={handlePdfPreview}
          />
        )}
      </div>
    </div>
  );
};

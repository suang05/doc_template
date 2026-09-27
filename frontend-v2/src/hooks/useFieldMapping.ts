'use client';

import { useState, useCallback, useEffect } from 'react';
import { type DragEndEvent } from '@dnd-kit/core';
import { arrayMove } from '@dnd-kit/sortable';
import { templatesApi } from '@/lib/api/templates.api';
import { getTemplateDatasets } from '@/lib/api/templateDatasets.api';
import type { TemplateDatasetDto, DataSourceType } from '@/types/api';
import { type MappingRow, type PlaceholderType, newRow, dtoToRow, rowToSaveItem } from '@/lib/mapping/mappingMapper';
import { TOAST_AUTO_DISMISS_MS, BLOB_URL_REVOKE_DELAY_MS } from '@/constants/timeouts';

export type { MappingRow };

interface UseFieldMappingReturn {
  rows: MappingRow[];
  expandedSql: Set<string>;
  expandedFx: Set<string>;
  templateDatasets: TemplateDatasetDto[];
  isDirty: boolean;
  loading: boolean;
  saving: boolean;
  savedOk: boolean;
  previewing: boolean;
  unmapped: number;
  loadMappings: (templateId: string) => Promise<void>;
  handleField: (id: string, field: string, value: unknown) => void;
  handleDelete: (id: string) => void;
  handleToggleSql: (id: string) => void;
  handleToggleFx: (id: string) => void;
  handleDragEnd: (event: DragEndEvent) => void;
  handleAddRow: () => void;
  /** Throws on API error — caller is responsible for showing the error. */
  handleSave: (templateId: string) => Promise<void>;
  /** Fixes the empty-sampleData bug: caller must pass the current sample JSON. */
  handlePdfPreview: (templateId: string, sampleData: Record<string, unknown>) => Promise<void>;
}

export function useFieldMapping(selectedTemplateId: string): UseFieldMappingReturn {
  const [rows, setRows] = useState<MappingRow[]>([]);
  const [expandedSql, setExpandedSql] = useState<Set<string>>(new Set());
  const [expandedFx, setExpandedFx] = useState<Set<string>>(new Set());
  const [templateDatasets, setTemplateDatasets] = useState<TemplateDatasetDto[]>([]);
  const [isDirty, setIsDirty] = useState(false);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [savedOk, setSavedOk] = useState(false);
  const [previewing, setPreviewing] = useState(false);

  // Load template datasets when template changes
  useEffect(() => {
    if (!selectedTemplateId) return;
    getTemplateDatasets(selectedTemplateId)
      .then(d => setTemplateDatasets(d ?? []))
      .catch(() => setTemplateDatasets([]));
  }, [selectedTemplateId]);

  const loadMappings = useCallback(async (templateId: string) => {
    if (!templateId) return;
    setLoading(true);
    setIsDirty(false);
    try {
      const data = await templatesApi.getTemplateMappings(templateId);
      setRows(data.map(dtoToRow));
    } catch {
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  // Reload mappings whenever the selected template changes
  useEffect(() => {
    if (selectedTemplateId) loadMappings(selectedTemplateId);
  }, [selectedTemplateId, loadMappings]);

  const handleField = useCallback((id: string, field: string, value: unknown) => {
    setRows(prev => prev.map(r => {
      if (r._id !== id) return r;
      const updated = { ...r, [field]: value };
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
      if (field === 'placeholderType' && value !== 'text') {
        updated.transform = null;
      }
      return updated as MappingRow;
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

  const handleAddRow = useCallback(() => {
    setRows(prev => {
      const row = newRow(prev.length + 1);
      return [...prev, row];
    });
    setIsDirty(true);
  }, []);

  const handleSave = useCallback(async (templateId: string) => {
    if (!templateId) throw new Error('ไม่พบ Template ID');
    setSaving(true);
    setSavedOk(false);
    try {
      const items = rows.map(rowToSaveItem);
      await templatesApi.saveTemplateMappings(templateId, items);
      setIsDirty(false);
      setSavedOk(true);
      setTimeout(() => setSavedOk(false), TOAST_AUTO_DISMISS_MS);
    } finally {
      setSaving(false);
    }
  }, [rows]);

  const handlePdfPreview = useCallback(async (templateId: string, sampleData: Record<string, unknown>) => {
    if (!templateId) return;
    setPreviewing(true);
    try {
      const blob = await templatesApi.previewMappings(templateId, sampleData);
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank', 'noopener,noreferrer');
      setTimeout(() => URL.revokeObjectURL(url), BLOB_URL_REVOKE_DELAY_MS);
    } catch { /* noop */ } finally {
      setPreviewing(false);
    }
  }, []);

  const unmapped = rows.filter(r =>
    (r.dataSourceType === 'sql' && !r.datasetAlias) ||
    (r.dataSourceType !== 'sql' && !r.sourcePath.trim()),
  ).length;

  return {
    rows, expandedSql, expandedFx, templateDatasets,
    isDirty, loading, saving, savedOk, previewing, unmapped,
    loadMappings, handleField, handleDelete, handleToggleSql, handleToggleFx,
    handleDragEnd, handleAddRow, handleSave, handlePdfPreview,
  };
}

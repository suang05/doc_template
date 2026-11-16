'use client';

import { useState, useCallback, useEffect, useRef } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { type DragEndEvent } from '@dnd-kit/core';
import { arrayMove } from '@dnd-kit/sortable';
import { templatesApi } from '@/features/templates/api/templates.api';
import { getTemplateDatasets } from '@/features/datasources/api/templateDatasets.api';
import { extractErrorMessage } from '@/lib/api/client';
import { withTimeoutSignal } from '@/lib/api/withTimeoutSignal';
import type { TemplateDatasetDto } from '@/lib/api/types';
import { type ConflictState, useConflictRecovery } from '@/hooks/useConflictRecovery';
import { type MappingRow, newRow, dtoToRow, rowToSaveItem } from '../domain/mappingMapper';
import { TOAST_AUTO_DISMISS_MS, BLOB_URL_REVOKE_DELAY_MS } from '@/constants/timeouts';

async function fetchMappingsBundle(
  templateId: string,
  signal?: AbortSignal,
): Promise<{ rows: MappingRow[]; etag: string | null }> {
  try {
    const [data, templateMeta] = await Promise.all([
      templatesApi.getTemplateMappings(templateId, { signal }),
      templatesApi.getTemplateWithETag(templateId, { signal }).catch(() => null),
    ]);
    const safeData = Array.isArray(data) ? data : [];
    return {
      rows: safeData.map(dtoToRow),
      etag: templateMeta?.etag ?? null,
    };
  } catch {
    return { rows: [], etag: null };
  }
}

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
  templateEtag: string | null;
  conflict: ConflictState | null;
  clearConflict: () => void;
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
  const queryClient = useQueryClient();
  const [rows, setRows] = useState<MappingRow[]>([]);
  const [expandedSql, setExpandedSql] = useState<Set<string>>(new Set());
  const [expandedFx, setExpandedFx] = useState<Set<string>>(new Set());
  const [isDirty, setIsDirty] = useState(false);
  const [savedOk, setSavedOk] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  const [templateEtag, setTemplateEtag] = useState<string | null>(null);
  const { conflict, captureConflict, clearConflict } = useConflictRecovery();
  const lastSyncedRef = useRef<{ templateId: string; dataUpdatedAt: number } | null>(null);

  const datasetsQuery = useQuery<TemplateDatasetDto[], Error>({
    queryKey: ['template-datasets', selectedTemplateId],
    enabled: Boolean(selectedTemplateId),
    queryFn: ({ signal }) =>
      withTimeoutSignal(
        (timedSignal) => getTemplateDatasets(selectedTemplateId, { signal: timedSignal }),
        signal,
      ).catch(() => []),
  });

  const templateDatasets = selectedTemplateId ? datasetsQuery.data ?? [] : [];

  const mappingsQuery = useQuery<{ rows: MappingRow[]; etag: string | null }, Error>({
    queryKey: ['template-mappings', selectedTemplateId],
    enabled: Boolean(selectedTemplateId),
    refetchOnWindowFocus: false,
    queryFn: ({ signal }) =>
      withTimeoutSignal(
        (timedSignal) => fetchMappingsBundle(selectedTemplateId, timedSignal),
        signal,
      ),
  });

  // Synchronize server query snapshot into local editable rows when template changes or clean reload completes
  useEffect(() => {
    if (!selectedTemplateId) {
      lastSyncedRef.current = null;
      setRows([]);
      setTemplateEtag(null);
      setIsDirty(false);
      clearConflict();
      return;
    }

    const templateChanged = lastSyncedRef.current?.templateId !== selectedTemplateId;
    if (
      mappingsQuery.data &&
      (templateChanged ||
        (!isDirty && lastSyncedRef.current?.dataUpdatedAt !== mappingsQuery.dataUpdatedAt))
    ) {
      setRows(mappingsQuery.data.rows);
      setTemplateEtag(mappingsQuery.data.etag);
      if (templateChanged) {
        setIsDirty(false);
        clearConflict();
      }
      lastSyncedRef.current = {
        templateId: selectedTemplateId,
        dataUpdatedAt: mappingsQuery.dataUpdatedAt,
      };
    } else if (templateChanged) {
      setRows([]);
      setTemplateEtag(null);
      setIsDirty(false);
      clearConflict();
    }
  }, [
    selectedTemplateId,
    mappingsQuery.data,
    mappingsQuery.dataUpdatedAt,
    isDirty,
    clearConflict,
  ]);

  const loadMappings = useCallback(
    async (templateId: string) => {
      if (!templateId) return;
      setIsDirty(false);
      clearConflict();
      if (templateId === selectedTemplateId) {
        await mappingsQuery.refetch();
      } else {
        await queryClient.invalidateQueries({ queryKey: ['template-mappings', templateId] });
      }
    },
    [clearConflict, mappingsQuery, queryClient, selectedTemplateId],
  );

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

  const saveMutation = useMutation({
    mutationFn: async ({
      templateId,
      items,
      ifMatch,
    }: {
      templateId: string;
      items: ReturnType<typeof rowToSaveItem>[];
      ifMatch: string | null;
    }) => {
      await templatesApi.saveTemplateMappings(templateId, items, ifMatch);
      const refreshed = await templatesApi.getTemplateWithETag(templateId).catch(() => null);
      return refreshed?.etag ?? null;
    },
    onSuccess: async (nextEtag, variables) => {
      if (nextEtag) {
        setTemplateEtag(nextEtag);
      }
      setIsDirty(false);
      setSavedOk(true);
      setTimeout(() => setSavedOk(false), TOAST_AUTO_DISMISS_MS);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['template-mappings', variables.templateId] }),
        queryClient.invalidateQueries({ queryKey: ['generator-form', variables.templateId] }),
        queryClient.invalidateQueries({ queryKey: ['template-studio', variables.templateId] }),
      ]);
    },
  });

  const handleSave = useCallback(async (templateId: string) => {
    if (!templateId) throw new Error('ไม่พบ Template ID');
    setSavedOk(false);
    clearConflict();
    try {
      const items = rows.map((row, idx) => rowToSaveItem(row, idx));
      await saveMutation.mutateAsync({ templateId, items, ifMatch: templateEtag });
    } catch (err: unknown) {
      if (!captureConflict(err)) {
        throw new Error(extractErrorMessage(err, 'บันทึก Mapping ล้มเหลว'));
      }
      throw err;
    }
  }, [rows, templateEtag, saveMutation, captureConflict, clearConflict]);

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

  const loading = Boolean(selectedTemplateId) && (mappingsQuery.isLoading || mappingsQuery.isRefetching);
  const saving = saveMutation.isPending;

  return {
    rows, expandedSql, expandedFx, templateDatasets,
    isDirty, loading, saving, savedOk, previewing, unmapped,
    templateEtag, conflict, clearConflict,
    loadMappings, handleField, handleDelete, handleToggleSql, handleToggleFx,
    handleDragEnd, handleAddRow, handleSave, handlePdfPreview,
  };
}

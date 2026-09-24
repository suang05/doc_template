"use client";
import { useState, useCallback } from "react";
import { api } from "@/lib/api-client";
import type {
  FieldMappingItem,
  FieldMappingPreviewItem,
} from "@/types/api";

export function useFieldMapping(fileName: string) {
  const [mappings, setMappings] = useState<FieldMappingItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [previewResult, setPreviewResult] = useState<FieldMappingPreviewItem[]>([]);
  const [previewing, setPreviewing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.getTemplateMappings(fileName);
      setMappings(res.mappings);
    } catch (e) {
      setError(e instanceof Error ? e.message : "โหลด mapping ไม่สำเร็จ");
    } finally {
      setLoading(false);
    }
  }, [fileName]);

  const save = useCallback(
    async (items: FieldMappingItem[]): Promise<boolean> => {
      setSaving(true);
      setError(null);
      try {
        await api.saveTemplateMappings(fileName, items);
        setMappings(items);
        return true;
      } catch (e) {
        setError(e instanceof Error ? e.message : "บันทึกไม่สำเร็จ");
        return false;
      } finally {
        setSaving(false);
      }
    },
    [fileName]
  );

  const preview = useCallback(
    async (items: FieldMappingItem[], sourceJson: string): Promise<void> => {
      setPreviewing(true);
      setError(null);
      try {
        let sampleData: Record<string, unknown> = {};
        try { sampleData = JSON.parse(sourceJson); } catch {}
        const res = await api.previewTemplateMappings(fileName, items, sampleData);
        setPreviewResult(res.preview);
      } catch (e) {
        setError(e instanceof Error ? e.message : "จำลองไม่สำเร็จ");
      } finally {
        setPreviewing(false);
      }
    },
    [fileName]
  );

  return {
    mappings,
    loading,
    saving,
    previewResult,
    previewing,
    error,
    load,
    save,
    preview,
  };
}

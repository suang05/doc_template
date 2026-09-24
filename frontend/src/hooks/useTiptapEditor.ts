"use client";
import { useState, useCallback } from "react";
import { api } from "@/lib/api-client";

export function useTiptapEditor(apiKey?: string) {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = useCallback(async (fileName: string, content: string, isGlobal = false): Promise<boolean> => {
    if (apiKey) api.setApiKey(apiKey);
    setSaving(true);
    setError(null);
    try {
      await api.saveTiptapTemplate({ fileName, content, isGlobal });
      return true;
    } catch (e) {
      setError(e instanceof Error ? e.message : "บันทึกไม่สำเร็จ");
      return false;
    } finally {
      setSaving(false);
    }
  }, []);

  return { saving, error, save };
}

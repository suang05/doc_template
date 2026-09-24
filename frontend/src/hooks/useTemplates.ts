"use client";
import { useState, useCallback } from "react";
import { api } from "@/lib/api-client";
import type { TemplateDetail } from "@/types/api";

interface UseTemplatesOptions {
  projectId?: string;
}

export function useTemplates({ projectId }: UseTemplatesOptions = {}) {
  const [templates, setTemplates] = useState<TemplateDetail[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.getTemplates(projectId);
      setTemplates(res.details ?? []);
    } catch (e) {
      setError(e instanceof Error ? e.message : "โหลดข้อมูลไม่สำเร็จ");
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  const upload = useCallback(
    async (file: File, isGlobal: boolean): Promise<TemplateDetail | null> => {
      try {
        const res = await api.uploadTemplate(file, isGlobal);
        await refresh();
        return res;
      } catch (e) {
        setError(e instanceof Error ? e.message : "อัปโหลดไม่สำเร็จ");
        return null;
      }
    },
    [refresh]
  );

  const deleteTemplate = useCallback(
    async (fileName: string): Promise<boolean> => {
      try {
        await api.deleteTemplate(fileName);
        setTemplates((prev) => prev.filter((t) => t.fileName !== fileName));
        return true;
      } catch (e) {
        setError(e instanceof Error ? e.message : "ลบไม่สำเร็จ");
        return false;
      }
    },
    []
  );

  return { templates, loading, error, refresh, upload, deleteTemplate };
}

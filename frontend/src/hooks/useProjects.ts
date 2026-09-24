"use client";
import { useState, useCallback } from "react";
import { api } from "@/lib/api-client";
import type { Project } from "@/types/api";

export function useProjects() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await api.getProjects();
      setProjects(list);
    } catch (e) {
      setError(e instanceof Error ? e.message : "โหลดโปรเจกต์ไม่สำเร็จ");
    } finally {
      setLoading(false);
    }
  }, []);

  const createProject = useCallback(
    async (data: { code: string; name: string; description?: string }): Promise<Project | null> => {
      try {
        const p = await api.createProject(data);
        await refresh();
        return p;
      } catch (e) {
        setError(e instanceof Error ? e.message : "สร้างโปรเจกต์ไม่สำเร็จ");
        return null;
      }
    },
    [refresh]
  );

  const createApiKey = useCallback(
    async (projectId: string, data: { name: string; expiresAt?: string }): Promise<string | null> => {
      try {
        const key = await api.createApiKey(projectId, data);
        await refresh();
        return key.keySecret ?? null;
      } catch (e) {
        setError(e instanceof Error ? e.message : "สร้าง API Key ไม่สำเร็จ");
        return null;
      }
    },
    [refresh]
  );

  const revokeApiKey = useCallback(
    async (keyId: string): Promise<boolean> => {
      try {
        await api.revokeApiKey(keyId);
        await refresh();
        return true;
      } catch (e) {
        setError(e instanceof Error ? e.message : "ระงับ API Key ไม่สำเร็จ");
        return false;
      }
    },
    [refresh]
  );

  return { projects, loading, error, refresh, createProject, createApiKey, revokeApiKey };
}

"use client";
import { useState, useCallback } from "react";
import { api } from "@/lib/api-client";
import type {
  GenerationLog,
  MetricSummary,
  AuditFilters,
} from "@/types/api";

const DEFAULT_FILTERS: AuditFilters = {
  projectId: "",
  status: "",
  page: 1,
  pageSize: 20,
};

export function useAudit() {
  const [logs, setLogs] = useState<GenerationLog[]>([]);
  const [metrics, setMetrics] = useState<MetricSummary | null>(null);
  const [filters, setFilters] = useState<AuditFilters>(DEFAULT_FILTERS);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(
    async (overrideFilters?: Partial<AuditFilters>) => {
      const f = { ...filters, ...overrideFilters };
      setLoading(true);
      setError(null);
      try {
        const [logsRes, metricsRes] = await Promise.all([
          api.getLogs({
            page: f.page,
            pageSize: f.pageSize,
            projectId: f.projectId || undefined,
            status: f.status || undefined,
          }),
          api.getMetrics(),
        ]);
        setLogs(logsRes.logs);
        setTotalPages(logsRes.totalPages);
        setTotalCount(logsRes.totalCount);
        setMetrics(metricsRes);
      } catch (e) {
        setError(e instanceof Error ? e.message : "โหลดข้อมูลไม่สำเร็จ");
      } finally {
        setLoading(false);
      }
    },
    [filters]
  );

  const updateFilters = useCallback((patch: Partial<AuditFilters>) => {
    setFilters((prev) => ({ ...prev, ...patch, page: 1 }));
  }, []);

  return {
    logs,
    metrics,
    filters,
    updateFilters,
    totalPages,
    totalCount,
    loading,
    error,
    refresh,
  };
}

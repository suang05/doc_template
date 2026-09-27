'use client';

import { useState, useEffect, useCallback } from 'react';
import { GenerationLogDto } from '@/types/api';
import { logsApi } from '@/lib/api/logs.api';

export function useGenerationLogs() {
  const [logs, setLogs] = useState<GenerationLogDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [limit, setLimit] = useState(50);
  const [appFilter, setAppFilter] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await logsApi.listLogs(page, limit, appFilter);
      setLogs(res.logs);
      setTotal(res.total);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'ไม่สามารถโหลดประวัติการสร้างเอกสารได้');
    } finally {
      setLoading(false);
    }
  }, [page, limit, appFilter]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  return {
    logs,
    total,
    page,
    setPage,
    limit,
    setLimit,
    appFilter,
    setAppFilter,
    loading,
    error,
    refresh: fetchLogs,
  };
}

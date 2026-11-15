'use client';

import { useState, useEffect, useCallback } from 'react';
import { extractErrorMessage } from '@/lib/api/client';
import type { GenerationLogDto } from '@/lib/api/types';
import { logsApi } from '../api/logs.api';

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
      setLogs(res.data);
      setTotal(res.pagination.totalCount);
    } catch (err) {
      setError(extractErrorMessage(err, 'ไม่สามารถโหลดประวัติการสร้างเอกสารได้'));
    } finally {
      setLoading(false);
    }
  }, [page, limit, appFilter]);

  useEffect(() => {
    void fetchLogs();
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

'use client';

import { useState, useCallback } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { extractErrorMessage } from '@/lib/api/client';
import {
  normalizePagination,
  type GenerationLogDto,
  type PagedEnvelope,
} from '@/lib/api/types';
import { logsApi } from '../api/logs.api';

export const generationLogsQueryKey = (page: number, limit: number, appFilter: string) =>
  ['logs', { page, limit, appFilter }] as const;

export function useGenerationLogs() {
  const [page, setPage] = useState(1);
  const [limit, setLimit] = useState(50);
  const [appFilter, setAppFilter] = useState<string>('');

  const normalizedApp = appFilter.trim();

  const {
    data,
    isLoading,
    isFetching,
    error: rawError,
    refetch,
  } = useQuery<PagedEnvelope<GenerationLogDto>, Error>({
    queryKey: generationLogsQueryKey(page, limit, normalizedApp),
    queryFn: () => logsApi.listLogs(page, limit, normalizedApp || undefined, true),
    placeholderData: keepPreviousData,
  });

  const logs = data?.data ?? [];
  const total = normalizePagination(data?.pagination, limit, page).totalCount;
  const loading = isLoading || isFetching;
  const error = rawError
    ? extractErrorMessage(rawError, 'ไม่สามารถโหลดประวัติการสร้างเอกสารได้')
    : null;

  const refresh = useCallback(async () => {
    await refetch();
  }, [refetch]);

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
    refresh,
  };
}

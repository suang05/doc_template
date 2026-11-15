'use client';

import { useMemo, useCallback } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useGenerationLogs } from '@/features/logs/hooks/useGenerationLogs';
import { logsApi } from '@/features/logs/api/logs.api';
import { useTemplates } from '@/features/templates/hooks/useTemplates';
import { isSuccessGenerationStatus } from '@/constants/domain';
import type { GenerationLogDto, LogMetricsDto } from '@/lib/api/types';

export interface AnalyticsSummary {
  totalGenerated: number;
  successRate: number;
  avgDurationMs: number;
  activeTemplatesCount: number;
  formatCounts: {
    pdf: number;
    docx: number;
    xlsx: number;
  };
  callerAppCounts: Record<string, number>;
  recentLogs: GenerationLogDto[];
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

export function useAnalytics(): AnalyticsSummary {
  const { logs, total, loading: logsLoading, error: logsError, refresh: refreshLogs } =
    useGenerationLogs();
  const { templates, loading: templatesLoading, refresh: refreshTemplates } = useTemplates();
  const {
    data: metrics,
    isLoading: metricsLoading,
    refetch: refetchMetrics,
  } = useQuery<LogMetricsDto | null>({
    queryKey: ['logs', 'metrics'],
    queryFn: async () => {
      try {
        return await logsApi.getMetrics();
      } catch {
        return null;
      }
    },
  });

  const loading = logsLoading || templatesLoading || metricsLoading;
  const error = logsError;

  const refresh = useCallback(() => {
    void refreshLogs();
    void refreshTemplates();
    void refetchMetrics();
  }, [refetchMetrics, refreshLogs, refreshTemplates]);

  const summary = useMemo(() => {
    const totalCount =
      metrics && metrics.totalGenerations >= 0
        ? metrics.totalGenerations
        : total > 0
          ? total
          : logs.length;

    const successLogsCount = logs.filter((l) => isSuccessGenerationStatus(l.status)).length;
    const successRate =
      metrics && metrics.totalGenerations > 0
        ? Math.round((metrics.successfulGenerations / metrics.totalGenerations) * 1000) / 10
        : logs.length > 0
          ? Math.round((successLogsCount / logs.length) * 1000) / 10
          : 100;

    const durations = logs.map((l) => l.durationMs || 0).filter((d) => d > 0);
    const avgDurationMs =
      metrics && metrics.averageDurationMs > 0
        ? Math.round(metrics.averageDurationMs)
        : durations.length > 0
          ? Math.round(durations.reduce((a, b) => a + b, 0) / durations.length)
          : 0;

    const activeTemplatesCount = templates.filter((t) => t.isActive !== false).length;

    const formatCounts = {
      pdf: logs.filter((l) => (l.outputFormat?.toLowerCase() || 'pdf') === 'pdf').length,
      docx: logs.filter((l) => l.outputFormat?.toLowerCase() === 'docx').length,
      xlsx: logs.filter((l) => l.outputFormat?.toLowerCase() === 'xlsx').length,
    };

    const callerAppCounts: Record<string, number> = {};
    if (metrics && metrics.topCallerApps.length > 0) {
      for (const entry of metrics.topCallerApps) {
        callerAppCounts[entry.callerApp || 'direct-portal'] = entry.generationCount;
      }
    } else {
      for (const log of logs) {
        const app = log.callerApp || 'direct-portal';
        callerAppCounts[app] = (callerAppCounts[app] || 0) + 1;
      }
    }

    return {
      totalGenerated: totalCount,
      successRate,
      avgDurationMs,
      activeTemplatesCount,
      formatCounts,
      callerAppCounts,
      recentLogs: logs.slice(0, 10),
    };
  }, [logs, total, templates, metrics]);

  return {
    ...summary,
    loading,
    error,
    refresh,
  };
}

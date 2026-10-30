'use client';

import { useMemo } from 'react';
import { useGenerationLogs } from '@/features/logs/hooks/useGenerationLogs';
import { useTemplates } from '@/features/templates/hooks/useTemplates';
import { GenerationLogDto } from '@/types/api';

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
  const { logs, total, loading: logsLoading, error: logsError, refresh: refreshLogs } = useGenerationLogs();
  const { templates, loading: templatesLoading, refresh: refreshTemplates } = useTemplates();

  const loading = logsLoading || templatesLoading;
  const error = logsError;

  const refresh = () => {
    refreshLogs();
    refreshTemplates();
  };

  const summary = useMemo(() => {
    const totalCount = total > 0 ? total : logs.length;
    const successLogs = logs.filter((l) => l.status === 'success');
    const successRate = logs.length > 0 ? Math.round((successLogs.length / logs.length) * 1000) / 10 : 100;

    const durations = logs.map((l) => l.durationMs || 0).filter((d) => d > 0);
    const avgDurationMs =
      durations.length > 0 ? Math.round(durations.reduce((a, b) => a + b, 0) / durations.length) : 0;

    const activeTemplatesCount = templates.filter((t) => t.isActive !== false).length;

    const formatCounts = {
      pdf: logs.filter((l) => (l.outputFormat?.toLowerCase() || 'pdf') === 'pdf').length,
      docx: logs.filter((l) => l.outputFormat?.toLowerCase() === 'docx').length,
      xlsx: logs.filter((l) => l.outputFormat?.toLowerCase() === 'xlsx').length,
    };

    const callerAppCounts: Record<string, number> = {};
    for (const log of logs) {
      const app = log.callerApp || 'direct-portal';
      callerAppCounts[app] = (callerAppCounts[app] || 0) + 1;
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
  }, [logs, total, templates]);

  return {
    ...summary,
    loading,
    error,
    refresh,
  };
}


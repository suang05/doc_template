import { apiClient } from '@/lib/api/client';
import type {
  ApiEnvelope,
  GenerationLogDto,
  LogMetricsDto,
  LogPayloadResultDto,
  PagedEnvelope,
} from '@/lib/api/types';

export const logsApi = {
  /**
   * List generation logs with pagination and optional caller app filter (SSoT: GET /api/v1/logs)
   */
  async listLogs(
    page = 1,
    limit = 50,
    app?: string,
    includeTotal = true,
  ): Promise<PagedEnvelope<GenerationLogDto>> {
    const params = new URLSearchParams({
      page: page.toString(),
      limit: limit.toString(),
      includeTotal: String(includeTotal),
    });
    if (app && app.trim()) {
      params.set('app', app.trim());
    }

    return apiClient<PagedEnvelope<GenerationLogDto>>(`/api/v1/logs?${params.toString()}`, {
      method: 'GET',
    });
  },

  /**
   * Get aggregate performance metrics (SSoT: GET /api/v1/logs/metrics)
   */
  async getMetrics(startDate?: string, endDate?: string): Promise<LogMetricsDto> {
    const params = new URLSearchParams();
    if (startDate) params.set('startDate', startDate);
    if (endDate) params.set('endDate', endDate);
    const qs = params.toString();
    const res = await apiClient<ApiEnvelope<LogMetricsDto>>(
      `/api/v1/logs/metrics${qs ? `?${qs}` : ''}`,
      { method: 'GET' },
    );
    return res.data;
  },

  /**
   * Get full input payload for an audit log entry (SSoT: GET /api/v1/logs/{id}/payload)
   */
  async getPayload(id: string): Promise<LogPayloadResultDto> {
    const res = await apiClient<ApiEnvelope<LogPayloadResultDto>>(`/api/v1/logs/${id}/payload`, {
      method: 'GET',
    });
    return res.data;
  },
};

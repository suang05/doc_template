import { apiClient } from '@/lib/api/client';
import { PagedLogsResponse, LogPayloadResultDto, ApiResponse } from '@/types/api';

export const logsApi = {
  /**
   * List generation logs with pagination and optional caller app filter
   */
  async listLogs(page = 1, limit = 50, app?: string): Promise<PagedLogsResponse> {
    const params = new URLSearchParams({
      page: page.toString(),
      limit: limit.toString(),
    });
    if (app && app.trim()) {
      params.set('app', app.trim());
    }

    return apiClient<PagedLogsResponse>(`/api/logs?${params.toString()}`, {
      method: 'GET',
    });
  },

  /**
   * Get full input payload for an audit log entry (transparently resolving offloaded payloads)
   */
  async getPayload(id: string): Promise<ApiResponse<LogPayloadResultDto>> {
    return apiClient<ApiResponse<LogPayloadResultDto>>(`/api/logs/${id}/payload`, {
      method: 'GET',
    });
  },
};

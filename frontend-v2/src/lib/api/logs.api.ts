import { apiClient } from './client';
import { PagedLogsResponse } from '@/types/api';

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
};

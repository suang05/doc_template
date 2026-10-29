import type { HealthReport } from '@/types/api';
import { apiClient } from '@/lib/api/client';

export async function checkHealth(): Promise<HealthReport> {
  return apiClient<HealthReport>('/health');
}

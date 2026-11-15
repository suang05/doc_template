import { apiClient } from '@/lib/api/client';
import type { HealthReport } from '../schemas/health.schema';

export async function checkHealth(): Promise<HealthReport> {
  return apiClient<HealthReport>('/api/health');
}

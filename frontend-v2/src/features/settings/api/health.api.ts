import { apiClient } from '@/lib/api/client';

export type HealthStatus = 'healthy' | 'degraded' | 'unhealthy';

export interface HealthCheck {
  name: string;
  status: HealthStatus;
  durationMs: number;
  description?: string | null;
}

export interface HealthReport {
  status: HealthStatus;
  totalDurationMs: number;
  checks: HealthCheck[];
}

export async function checkHealth(): Promise<HealthReport> {
  return apiClient<HealthReport>('/api/health');
}

import type { HealthReport } from '@/types/api';

const getApiUrl = () =>
  (typeof window !== 'undefined'
    ? localStorage.getItem('smk_api_url') || ''
    : '') || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

export const healthApi = {
  check: async (): Promise<HealthReport> => {
    const res = await fetch(`${getApiUrl()}/health`);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.json() as Promise<HealthReport>;
  },
};

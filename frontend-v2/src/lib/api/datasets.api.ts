import { apiClient } from './client';
import type { Dataset, CreateDatasetRequest, UpdateDatasetRequest } from '@/types/api';

export const datasetsApi = {
  getAll: () =>
    apiClient<Dataset[]>('/api/datasets', { method: 'GET' }),

  getById: (id: string) =>
    apiClient<Dataset>(`/api/datasets/${id}`, { method: 'GET' }),

  create: (data: CreateDatasetRequest) =>
    apiClient<Dataset>('/api/datasets', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  update: (id: string, data: UpdateDatasetRequest) =>
    apiClient<Dataset>(`/api/datasets/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  delete: (id: string) =>
    apiClient<void>(`/api/datasets/${id}`, { method: 'DELETE' }),
};

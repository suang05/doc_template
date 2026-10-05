import { apiClient } from './client';
import type { Dataset, CreateDatasetRequest, UpdateDatasetRequest } from '@/types/api';

export const datasetsApi = {
  getAll: () =>
    apiClient<any>('/api/datasets', { method: 'GET' }).then(res => {
      if (Array.isArray(res)) return res as Dataset[];
      if (res && Array.isArray(res.data)) return res.data as Dataset[];
      return [] as Dataset[];
    }),

  getById: (id: string) =>
    apiClient<any>(`/api/datasets/${id}`, { method: 'GET' }).then(res => (res?.data ?? res) as Dataset),

  create: (data: CreateDatasetRequest) =>
    apiClient<any>('/api/datasets', {
      method: 'POST',
      body: JSON.stringify(data),
    }).then(res => (res?.data ?? res) as Dataset),

  update: (id: string, data: UpdateDatasetRequest) =>
    apiClient<any>(`/api/datasets/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }).then(res => (res?.data ?? res) as Dataset),

  delete: (id: string) =>
    apiClient<void>(`/api/datasets/${id}`, { method: 'DELETE' }),
};

import { apiClient } from '@/lib/api/client';
import { ApiResponse, Dataset, CreateDatasetRequest, UpdateDatasetRequest } from '@/types/api';

export const datasetsApi = {
  getAll: async (): Promise<Dataset[]> => {
    const res = await apiClient<ApiResponse<Dataset[]>>('/api/v1/datasets', { method: 'GET' });
    return res.data;
  },

  getById: async (id: string): Promise<Dataset> => {
    const res = await apiClient<ApiResponse<Dataset>>(`/api/v1/datasets/${id}`, { method: 'GET' });
    return res.data;
  },

  create: async (data: CreateDatasetRequest): Promise<Dataset> => {
    const res = await apiClient<ApiResponse<Dataset>>('/api/v1/datasets', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    return res.data;
  },

  update: async (id: string, data: UpdateDatasetRequest): Promise<Dataset> => {
    const res = await apiClient<ApiResponse<Dataset>>(`/api/v1/datasets/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
    return res.data;
  },

  delete: async (id: string): Promise<void> => {
    await apiClient<void>(`/api/v1/datasets/${id}`, { method: 'DELETE' });
  },
};

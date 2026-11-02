import { apiClient } from '@/lib/api/client';
import { ApiResponse } from '@/types/api';
import {
  DataConnection,
  CreateDataConnectionRequest,
  UpdateDataConnectionRequest,
  TestDataConnectionRequest,
  TestDataConnectionResponse,
} from '../schemas/dataconnection.schema';

export const dataconnectionsApi = {
  getAll: async (): Promise<DataConnection[]> => {
    const res = await apiClient<ApiResponse<DataConnection[]>>('/api/v1/data-connections', { method: 'GET' });
    return res.data;
  },

  getById: async (id: string): Promise<DataConnection> => {
    const res = await apiClient<ApiResponse<DataConnection>>(`/api/v1/data-connections/${id}`, { method: 'GET' });
    return res.data;
  },

  create: async (data: CreateDataConnectionRequest): Promise<DataConnection> => {
    const res = await apiClient<ApiResponse<DataConnection>>('/api/v1/data-connections', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    return res.data;
  },

  update: async (id: string, data: UpdateDataConnectionRequest): Promise<DataConnection> => {
    const res = await apiClient<ApiResponse<DataConnection>>(`/api/v1/data-connections/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
    return res.data;
  },

  delete: async (id: string): Promise<void> => {
    await apiClient<void>(`/api/v1/data-connections/${id}`, { method: 'DELETE' });
  },

  test: async (data: TestDataConnectionRequest): Promise<TestDataConnectionResponse> => {
    const res = await apiClient<ApiResponse<TestDataConnectionResponse>>('/api/v1/data-connections/test', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    return res.data;
  },
};

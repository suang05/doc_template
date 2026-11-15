import { apiClient, apiRequestWithMeta } from '@/lib/api/client';
import type {
  ApiEnvelope,
  CreateDataConnectionRequest,
  DataConnectionDto,
  ResourceWithETag,
  TestDataConnectionRequest,
  TestDataConnectionResultDto,
  UpdateDataConnectionRequest,
} from '@/lib/api/types';

export const dataconnectionsApi = {
  getAll: async (): Promise<DataConnectionDto[]> => {
    const res = await apiClient<ApiEnvelope<DataConnectionDto[]>>('/api/v1/data-connections', {
      method: 'GET',
    });
    return res.data;
  },

  getById: async (id: string): Promise<DataConnectionDto> => {
    const res = await apiClient<ApiEnvelope<DataConnectionDto>>(`/api/v1/data-connections/${id}`, {
      method: 'GET',
    });
    return res.data;
  },

  getWithETag: async (id: string): Promise<ResourceWithETag<DataConnectionDto>> => {
    const res = await apiRequestWithMeta<ApiEnvelope<DataConnectionDto>>(
      `/api/v1/data-connections/${id}`,
      { method: 'GET' },
    );
    return { ...res, data: res.data.data };
  },

  create: async (data: CreateDataConnectionRequest): Promise<DataConnectionDto> => {
    const res = await apiClient<ApiEnvelope<DataConnectionDto>>('/api/v1/data-connections', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    return res.data;
  },

  update: async (
    id: string,
    data: UpdateDataConnectionRequest,
    ifMatch?: string | null,
  ): Promise<DataConnectionDto> => {
    const res = await apiClient<ApiEnvelope<DataConnectionDto>>(
      `/api/v1/data-connections/${id}`,
      {
        method: 'PUT',
        body: JSON.stringify(data),
        ifMatch,
      },
    );
    return res.data;
  },

  delete: async (id: string, ifMatch?: string | null): Promise<void> => {
    await apiClient<void>(`/api/v1/data-connections/${id}`, {
      method: 'DELETE',
      ifMatch,
    });
  },

  test: async (data: TestDataConnectionRequest): Promise<TestDataConnectionResultDto> => {
    const res = await apiClient<ApiEnvelope<TestDataConnectionResultDto>>(
      '/api/v1/data-connections/connectivity-checks',
      {
        method: 'POST',
        body: JSON.stringify(data),
      },
    );
    return res.data;
  },

  checkConnectivity: async (id: string): Promise<TestDataConnectionResultDto> => {
    const res = await apiClient<ApiEnvelope<TestDataConnectionResultDto>>(
      `/api/v1/data-connections/${id}/connectivity-checks`,
      {
        method: 'POST',
      },
    );
    return res.data;
  },
};

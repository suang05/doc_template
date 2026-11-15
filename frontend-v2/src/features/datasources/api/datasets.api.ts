import { apiClient, apiRequestWithMeta } from '@/lib/api/client';
import type {
  ApiEnvelope,
  CreateDatasetRequest,
  DatasetDto,
  DryRunDatasetRequest,
  DryRunDatasetResultDto,
  ResourceWithETag,
  UpdateDatasetRequest,
} from '@/lib/api/types';

export type DryRunDatasetPayload = Omit<DryRunDatasetRequest, 'parameters'> & {
  parameters?: Record<string, unknown> | null;
};

export const datasetsApi = {
  getAll: async (): Promise<DatasetDto[]> => {
    const res = await apiClient<ApiEnvelope<DatasetDto[]>>('/api/v1/datasets', {
      method: 'GET',
    });
    return res.data;
  },

  getById: async (id: string): Promise<DatasetDto> => {
    const res = await apiClient<ApiEnvelope<DatasetDto>>(`/api/v1/datasets/${id}`, {
      method: 'GET',
    });
    return res.data;
  },

  getWithETag: async (id: string): Promise<ResourceWithETag<DatasetDto>> => {
    const res = await apiRequestWithMeta<ApiEnvelope<DatasetDto>>(`/api/v1/datasets/${id}`, {
      method: 'GET',
    });
    return { ...res, data: res.data.data };
  },

  create: async (data: CreateDatasetRequest): Promise<DatasetDto> => {
    const res = await apiClient<ApiEnvelope<DatasetDto>>('/api/v1/datasets', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    return res.data;
  },

  update: async (
    id: string,
    data: UpdateDatasetRequest,
    ifMatch?: string | null,
  ): Promise<DatasetDto> => {
    const res = await apiClient<ApiEnvelope<DatasetDto>>(`/api/v1/datasets/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
      ifMatch,
    });
    return res.data;
  },

  delete: async (id: string, ifMatch?: string | null): Promise<void> => {
    await apiClient<void>(`/api/v1/datasets/${id}`, {
      method: 'DELETE',
      ifMatch,
    });
  },

  dryRun: async (data: DryRunDatasetPayload): Promise<DryRunDatasetResultDto> => {
    const res = await apiClient<ApiEnvelope<DryRunDatasetResultDto>>('/api/v1/datasets/dry-run', {
      method: 'POST',
      body: JSON.stringify(data),
    });
    return res.data;
  },
};

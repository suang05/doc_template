import { apiClient } from '@/lib/api/client';
import type {
  ApiEnvelope,
  SaveTemplateDatasetItem,
  TemplateDatasetDto,
} from '@/lib/api/types';

export const getTemplateDatasets = async (
  templateId: string,
  options?: RequestInit,
): Promise<TemplateDatasetDto[]> => {
  const res = await apiClient<ApiEnvelope<TemplateDatasetDto[]>>(
    `/api/v1/templates/${templateId}/datasets`,
    options,
  );
  return res.data;
};

export const saveTemplateDatasets = async (
  templateId: string,
  datasets: SaveTemplateDatasetItem[],
  ifMatch?: string | null,
): Promise<TemplateDatasetDto[]> => {
  const res = await apiClient<ApiEnvelope<TemplateDatasetDto[]>>(
    `/api/v1/templates/${templateId}/datasets`,
    {
      method: 'PUT',
      body: JSON.stringify({ datasets }),
      ifMatch,
    },
  );
  return res.data;
};

export const templateDatasetsApi = {
  getTemplateDatasets,
  saveTemplateDatasets,
};

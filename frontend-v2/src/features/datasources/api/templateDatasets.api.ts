import { apiClient } from '@/lib/api/client';
import { ApiResponse, TemplateDatasetDto, SaveTemplateDatasetItem } from '@/types/api';

export async function getTemplateDatasets(templateId: string): Promise<TemplateDatasetDto[]> {
  const res = await apiClient<ApiResponse<TemplateDatasetDto[]>>(`/api/v1/templates/${templateId}/datasets`);
  return res.data;
}

export async function saveTemplateDatasets(templateId: string, items: SaveTemplateDatasetItem[]): Promise<TemplateDatasetDto[]> {
  const res = await apiClient<ApiResponse<TemplateDatasetDto[]>>(`/api/v1/templates/${templateId}/datasets`, {
    method: 'PUT',
    body: JSON.stringify({ datasets: items }),
  });
  return res.data;
}

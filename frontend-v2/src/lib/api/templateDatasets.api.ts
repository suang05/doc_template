import { apiClient } from './client';
import type { TemplateDatasetDto, SaveTemplateDatasetItem } from '@/types/api';

export async function getTemplateDatasets(templateId: string): Promise<TemplateDatasetDto[]> {
  const res = await apiClient<{ data?: TemplateDatasetDto[]; datasets?: TemplateDatasetDto[] } | TemplateDatasetDto[]>(`/api/templates/${templateId}/datasets`);
  if (Array.isArray(res)) return res;
  if ('data' in res && Array.isArray(res.data)) return res.data;
  if ('datasets' in res && Array.isArray(res.datasets)) return res.datasets;
  return [];
}

export async function saveTemplateDatasets(templateId: string, items: SaveTemplateDatasetItem[]): Promise<TemplateDatasetDto[]> {
  const res = await apiClient<{ data?: TemplateDatasetDto[] } | TemplateDatasetDto[]>(`/api/templates/${templateId}/datasets`, {
    method: 'PUT',
    body: JSON.stringify({ datasets: items }),
  });
  if (Array.isArray(res)) return res;
  if ('data' in res && Array.isArray(res.data)) return res.data;
  return [];
}

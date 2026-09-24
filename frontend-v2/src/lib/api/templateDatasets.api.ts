import { apiClient } from './client';
import type { TemplateDatasetDto, SaveTemplateDatasetItem } from '@/types/api';

export async function getTemplateDatasets(templateId: string): Promise<TemplateDatasetDto[]> {
  const res = await apiClient<{ datasets: TemplateDatasetDto[] }>(`/api/templates/${templateId}/datasets`);
  return res.datasets ?? [];
}

export async function saveTemplateDatasets(templateId: string, items: SaveTemplateDatasetItem[]): Promise<void> {
  await apiClient<{ success: boolean }>(`/api/templates/${templateId}/datasets`, {
    method: 'PUT',
    body: JSON.stringify(items),
  });
}

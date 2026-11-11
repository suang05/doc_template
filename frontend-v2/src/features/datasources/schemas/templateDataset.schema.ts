import { z } from 'zod';

export const TemplateDatasetResultDtoSchema = z.object({
  id: z.string().uuid(),
  templateId: z.string().uuid(),
  datasetId: z.string().uuid(),
  datasetName: z.string(),
  alias: z.string().max(50),
  sortOrder: z.number().int(),
});
export type TemplateDatasetResultDto = z.infer<typeof TemplateDatasetResultDtoSchema>;

// Backward-compatible alias
export const TemplateDatasetDtoSchema = TemplateDatasetResultDtoSchema;
export type TemplateDatasetDto = TemplateDatasetResultDto;

export const SaveTemplateDatasetItemSchema = z.object({
  datasetId: z.string().uuid(),
  alias: z.string().min(1).max(50),
  sortOrder: z.number().int().default(0),
});
export type SaveTemplateDatasetItem = z.infer<typeof SaveTemplateDatasetItemSchema>;

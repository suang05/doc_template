import { z } from 'zod';

export const DatasetSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  description: z.string().nullable().optional(),
  dataConnectionId: z.string().uuid(),
  dataConnectionName: z.string(),
  sqlQuery: z.string(),
  cacheSeconds: z.number().int().default(0),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});
export type Dataset = z.infer<typeof DatasetSchema>;

export const CreateDatasetSchema = z.object({
  name: z.string().min(1, 'ต้องระบุชื่อ Dataset'),
  description: z.string().nullable().optional(),
  dataConnectionId: z.string().uuid('ต้องเลือก Data Connection'),
  sqlQuery: z.string().min(1, 'ต้องระบุ SQL Query'),
  cacheSeconds: z.number().int().min(0).default(0),
});
export type CreateDatasetRequest = z.infer<typeof CreateDatasetSchema>;

export const UpdateDatasetSchema = CreateDatasetSchema;
export type UpdateDatasetRequest = z.infer<typeof UpdateDatasetSchema>;

export const DatasetColumnSchema = z.object({
  name: z.string(),
  dataType: z.string(),
  isNullable: z.boolean().default(true),
});

export const DryRunDatasetResponseSchema = z.object({
  success: z.boolean(),
  columns: z.array(DatasetColumnSchema),
  sampleRows: z.array(z.record(z.string(), z.unknown())),
  sampleCount: z.number().int(),
  executionTimeMs: z.number(),
  error: z.string().nullable().optional(),
});
export type DryRunDatasetResponse = z.infer<typeof DryRunDatasetResponseSchema>;

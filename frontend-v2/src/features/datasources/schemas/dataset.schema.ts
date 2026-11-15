import { z } from 'zod';

export const CreateDatasetSchema = z.object({
  name: z.string().trim().min(1, 'ต้องระบุชื่อ Dataset'),
  description: z.string().nullable().optional(),
  dataConnectionId: z.string().uuid('ต้องเลือก Data Connection'),
  sqlQuery: z.string().trim().min(1, 'ต้องระบุ SQL Query'),
  cacheSeconds: z.number().int().min(0).default(0),
});
export type CreateDatasetRequest = z.infer<typeof CreateDatasetSchema>;

export const UpdateDatasetSchema = CreateDatasetSchema;
export type UpdateDatasetRequest = z.infer<typeof UpdateDatasetSchema>;

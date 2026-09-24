import { z } from 'zod';

export const GenerationLogDtoSchema = z.object({
  id: z.string().uuid(),
  templateId: z.string().uuid().nullable().optional(),
  templateVer: z.number().int().nullable().optional(),
  apiKeyId: z.string().uuid().nullable().optional(),
  callerApp: z.string().nullable().optional(),
  inputData: z.string().nullable().optional(),
  outputKey: z.string().nullable().optional(),
  outputFormat: z.string().nullable().optional(),
  durationMs: z.number().int().nullable().optional(),
  status: z.string(),
  errorMsg: z.string().nullable().optional(),
  createdAt: z.string(),
});
export type GenerationLogDto = z.infer<typeof GenerationLogDtoSchema>;

export const PagedLogsResponseSchema = z.object({
  logs: z.array(GenerationLogDtoSchema),
  total: z.number().int(),
  page: z.number().int(),
  limit: z.number().int(),
});
export type PagedLogsResponse = z.infer<typeof PagedLogsResponseSchema>;

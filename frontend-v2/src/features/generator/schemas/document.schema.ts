import { z } from 'zod';
import { outputFormats } from '@/constants/domain';

export const OutputFormatSchema = z.enum(outputFormats);

export const GenerateDocumentRequestSchema = z.object({
  payload: z.record(z.string(), z.unknown()).default({}),
  outputFormat: OutputFormatSchema.default('pdf'),
  documentRef: z.string().optional().nullable(),
  changeNote: z.string().optional().nullable(),
});
export type GenerateDocumentFormRequest = z.infer<typeof GenerateDocumentRequestSchema>;

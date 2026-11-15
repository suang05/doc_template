import { z } from 'zod';
import { outputFormats, type OutputFormat } from '@/constants/domain';

export { type OutputFormat };
export const OutputFormatSchema = z.enum(outputFormats);

export const GenerateDocumentRequestSchema = z.object({
  payload: z.record(z.string(), z.unknown()).default({}),
  outputFormat: OutputFormatSchema.default('pdf'),
  documentRef: z.string().optional().nullable(),
  changeNote: z.string().optional().nullable(),
});
export type GenerateDocumentFormRequest = z.infer<typeof GenerateDocumentRequestSchema>;

export const GenerateDocumentResponseSchema = z.object({
  generationId: z.string().uuid(),
  documentRef: z.string().optional().nullable(),
  outputFormat: z.string(),
  fileSizeBytes: z.number().optional().default(0),
  sha256: z.string().optional().default(''),
  url: z.string().min(1),
  expiresAt: z.string(),
});
export type GenerateDocumentResponse = z.infer<typeof GenerateDocumentResponseSchema>;

export const PreviewDocumentRequestSchema = z.object({
  payload: z.record(z.string(), z.unknown()).default({}),
  html: z.string().optional().nullable(),
});
export type PreviewDocumentRequest = z.infer<typeof PreviewDocumentRequestSchema>;

export const DocumentVersionDtoSchema = z.object({
  id: z.string().uuid(),
  documentId: z.string().uuid(),
  documentRef: z.string(),
  version: z.number().int().positive(),
  templateVersionId: z.string().uuid().nullable().optional(),
  generationLogId: z.string().uuid().nullable().optional(),
  changeNote: z.string().nullable().optional(),
  createdBy: z.string().nullable().optional(),
  createdAt: z.string(),
});
export type DocumentVersionRecord = z.infer<typeof DocumentVersionDtoSchema>;

export const DocumentJobDtoSchema = z.object({
  jobId: z.string().uuid(),
  templateSlug: z.string(),
  status: z.string(),
  outputFormat: z.string(),
  createdAt: z.string(),
  startedAt: z.string().nullable().optional(),
  completedAt: z.string().nullable().optional(),
  downloadUrl: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
  errorCode: z.string().nullable().optional(),
});
export type DocumentJobRecord = z.infer<typeof DocumentJobDtoSchema>;

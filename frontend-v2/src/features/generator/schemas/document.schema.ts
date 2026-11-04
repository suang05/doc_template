import { z } from 'zod';

export const OutputFormatSchema = z.enum(['pdf', 'docx', 'xlsx']);
export type OutputFormat = z.infer<typeof OutputFormatSchema>;

export const GenerateDocumentRequestSchema = z.object({
  payload: z.record(z.any()).default({}),
  outputFormat: OutputFormatSchema.default('pdf'),
  documentRef: z.string().optional().nullable(),
  changeNote: z.string().optional().nullable(),
});
export type GenerateDocumentRequest = z.infer<typeof GenerateDocumentRequestSchema>;

export const GenerateDocumentResponseSchema = z.object({
  generationId: z.string().uuid(),
  documentRef: z.string().optional().nullable(),
  outputFormat: z.string(),
  fileSizeBytes: z.number().optional().default(0),
  sha256: z.string().optional().default(''),
  url: z.string().url(),
  expiresAt: z.string(),
});
export type GenerateDocumentResponse = z.infer<typeof GenerateDocumentResponseSchema>;

export const PreviewDocumentRequestSchema = z.object({
  payload: z.record(z.any()).default({}),
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
export type DocumentVersionDto = z.infer<typeof DocumentVersionDtoSchema>;

export const DocumentJobDtoSchema = z.object({
  jobId: z.string().uuid(),
  templateSlug: z.string(),
  status: z.string(),
  enqueuedAt: z.string(),
  startedAt: z.string().nullable().optional(),
  completedAt: z.string().nullable().optional(),
  statusUrl: z.string(),
  downloadUrl: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
  errorCode: z.string().nullable().optional(),
});
export type DocumentJobDto = z.infer<typeof DocumentJobDtoSchema>;

/** Generic API response envelope — used by adapters that unwrap `data` fields. */
export interface ApiResponse<T> {
  data?: T;
  error?: string;
  success?: boolean;
}

/** Request shape for the stateless HTML-to-PDF render endpoint. */
export interface RenderHtmlToPdfRequest {
  htmlContent: string;
  headerHtml?: string;
  footerHtml?: string;
}

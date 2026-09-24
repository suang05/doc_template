import { z } from 'zod';

export const TemplateDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1, 'ต้องระบุชื่อแม่แบบ'),
  slug: z.string().min(1, 'ต้องระบุ Slug ภาษาอังกฤษ').regex(/^[a-z0-9-_]+$/, 'Slug ต้องเป็นตัวพิมพ์เล็ก ตัวเลข หรือขีดกลางเท่านั้น'),
  category: z.string().nullable().optional(),
  isActive: z.boolean().default(true),
  currentVersionId: z.string().uuid().nullable().optional(),
  fileFormat: z.enum(['html', 'docx', 'xlsx', 'pdf']).nullable().optional(),
  createdAt: z.string(),
  updatedAt: z.string(),
});
export type TemplateDto = z.infer<typeof TemplateDtoSchema>;

export const CreateTemplateRequestSchema = z.object({
  name: z.string().min(1, 'ต้องระบุชื่อแม่แบบ'),
  slug: z.string().min(1, 'ต้องระบุ Slug ภาษาอังกฤษ').regex(/^[a-z0-9-_]+$/, 'Slug ต้องเป็นตัวพิมพ์เล็ก ตัวเลข หรือขีดกลางเท่านั้น'),
  category: z.string().optional().nullable(),
  htmlContent: z.string().optional().nullable(),
});
export type CreateTemplateRequest = z.infer<typeof CreateTemplateRequestSchema>;

export const SaveTemplateHtmlRequestSchema = z.object({
  html: z.string().min(1, 'เนื้อหา HTML ต้องไม่ว่างเปล่า'),
  samplePayload: z.string().optional().nullable(),
  changeNote: z.string().optional().nullable(),
});
export type SaveTemplateHtmlRequest = z.infer<typeof SaveTemplateHtmlRequestSchema>;

export const TemplateStudioDtoSchema = z.object({
  html: z.string(),
  samplePayload: z.string().nullable().optional(),
  dataSchema: z.string().nullable().optional(),
  version: z.number().int(),
});
export type TemplateStudioDto = z.infer<typeof TemplateStudioDtoSchema>;

export const TemplateSchemaDtoSchema = z.object({
  templateId: z.string().uuid(),
  slug: z.string(),
  format: z.string().nullable().optional(),
  version: z.number().int(),
  dataSchema: z.string().nullable().optional(),
  samplePayload: z.string().nullable().optional(),
});
export type TemplateSchemaDto = z.infer<typeof TemplateSchemaDtoSchema>;


export const UpdateTemplateRequestSchema = z.object({
  name: z.string().optional().nullable(),
  category: z.string().optional().nullable(),
  isActive: z.boolean().optional().nullable(),
});
export type UpdateTemplateRequest = z.infer<typeof UpdateTemplateRequestSchema>;

export const TemplateVersionDtoSchema = z.object({
  id: z.string().uuid(),
  templateId: z.string().uuid(),
  version: z.number().int(),
  storageKey: z.string(),
  status: z.number().int().optional(),
  fileFormat: z.string().nullable().optional(),
  commitMessage: z.string().nullable().optional(),
  createdBy: z.string().nullable().optional(),
  createdAt: z.string(),
});
export type TemplateVersionDto = z.infer<typeof TemplateVersionDtoSchema>;

export const TemplateValidationResultSchema = z.object({
  valid: z.boolean(),
  errors: z.array(z.string()).default([]),
  fields: z.array(z.string()).default([]),
});
export type TemplateValidationResult = z.infer<typeof TemplateValidationResultSchema>;

import { z } from 'zod';

export const slugSchema = z
  .string()
  .min(3, 'Slug ต้องมีความยาวอย่างน้อย 3 ตัวอักษร')
  .max(64, 'Slug ต้องไม่เกิน 64 ตัวอักษร')
  .regex(/^[a-z0-9-]+$/, 'Slug ต้องใช้ตัวอักษรพิมพ์เล็ก ตัวเลข และขีดกลาง (-) เท่านั้น');

export const TemplateDtoSchema = z.object({
  id: z.string().uuid(),
  projectId: z.string().uuid().optional(),
  name: z.string(),
  slug: z.string(),
  category: z.string().nullable(),
  isActive: z.boolean(),
  currentVersionId: z.string().uuid().nullable().optional(),
  fileFormat: z.string().nullable(),
  createdAt: z.string(),
  updatedAt: z.string().nullable(),
});
export type TemplateDto = z.infer<typeof TemplateDtoSchema>;

export const CreateTemplateRequestSchema = z.object({
  name: z.string().min(2, 'กรุณาระบุชื่อแม่แบบอย่างน้อย 2 ตัวอักษร'),
  slug: slugSchema,
  category: z.string().default('contract'),
  html: z.string().optional(),
});
export type CreateTemplateRequest = z.infer<typeof CreateTemplateRequestSchema>;

export const SaveTemplateHtmlRequestSchema = z.object({
  html: z.string().min(10, 'โค้ด HTML ต้องมีความยาวอย่างน้อย 10 ตัวอักษร'),
  samplePayload: z.string().nullable().optional(),
  changeNote: z.string().max(200, 'คำอธิบายไม่ควรเกิน 200 ตัวอักษร').nullable().optional(),
});
export type SaveTemplateHtmlRequest = z.infer<typeof SaveTemplateHtmlRequestSchema>;

export const TemplateStudioDtoSchema = z.object({
  html: z.string(),
  samplePayload: z.string().nullable(),
  dataSchema: z.string().nullable().optional(),
  version: z.number(),
});
export type TemplateStudioDto = z.infer<typeof TemplateStudioDtoSchema>;

export const TemplateSchemaDtoSchema = z.object({
  templateId: z.string().uuid(),
  slug: z.string(),
  format: z.unknown().nullable().optional(),
  version: z.number(),
  dataSchema: z.string().nullable(),
  samplePayload: z.string().nullable(),
});
export type TemplateSchemaDto = z.infer<typeof TemplateSchemaDtoSchema>;

export const UpdateTemplateRequestSchema = z.object({
  name: z.string().min(2, 'กรุณาระบุชื่อแม่แบบ'),
  category: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
});
export type UpdateTemplateRequest = z.infer<typeof UpdateTemplateRequestSchema>;

export const TemplateVersionDtoSchema = z.object({
  id: z.string().uuid(),
  templateId: z.string().uuid(),
  version: z.number(),
  storageKey: z.string().optional(),
  status: z.string(),
  fileFormat: z.string().nullable().optional(),
  dataSchema: z.string().nullable().optional(),
  samplePayload: z.string().nullable().optional(),
  mappingsSnapshot: z.string().nullable().optional(),
  commitMessage: z.string().nullable().optional(),
  createdBy: z.string().nullable().optional(),
  createdAt: z.string(),
  updatedAt: z.string().nullable().optional(),
});
export type TemplateVersionDto = z.infer<typeof TemplateVersionDtoSchema>;

export const TemplateValidationResultSchema = z.object({
  valid: z.boolean(),
  fields: z.array(z.string()),
  errors: z.array(z.string()),
});
export type TemplateValidationResult = z.infer<typeof TemplateValidationResultSchema>;

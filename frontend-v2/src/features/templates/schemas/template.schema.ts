import { z } from 'zod';

export const slugSchema = z
  .string()
  .min(3, 'Slug ต้องมีความยาวอย่างน้อย 3 ตัวอักษร')
  .max(64, 'Slug ต้องไม่เกิน 64 ตัวอักษร')
  .regex(/^[a-z0-9-]+$/, 'Slug ต้องใช้ตัวอักษรพิมพ์เล็ก ตัวเลข และขีดกลาง (-) เท่านั้น');

export const CreateTemplateRequestSchema = z.object({
  name: z.string().trim().min(2, 'กรุณาระบุชื่อแม่แบบอย่างน้อย 2 ตัวอักษร'),
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

export const UpdateTemplateRequestSchema = z.object({
  name: z.string().min(2, 'กรุณาระบุชื่อแม่แบบ'),
  category: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
});
export type UpdateTemplateRequest = z.infer<typeof UpdateTemplateRequestSchema>;

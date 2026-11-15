import { z } from 'zod';
import { outputFormats } from '@/constants/domain';

export const CreateDocumentJobFormSchema = z.object({
  slug: z.string().min(1, 'กรุณาเลือกแม่แบบเอกสาร'),
  outputFormat: z.enum(outputFormats).default('pdf'),
  documentRef: z.string().nullable().optional(),
  maxAttempts: z.number().int().min(1).max(10).default(3),
  webhookUrl: z
    .string()
    .trim()
    .url('Webhook URL ไม่ถูกต้อง')
    .or(z.literal(''))
    .optional(),
});

export type CreateDocumentJobForm = z.infer<typeof CreateDocumentJobFormSchema>;

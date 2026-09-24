import { z } from 'zod';

export const ThaiTransformTypes = [
  { value: 'thai_baht_text', label: 'บาทข้อความ (BahtText)', example: 'หนึ่งล้านสองแสนบาทถ้วน' },
  { value: 'thai_date', label: 'วันที่ไทย (DD/MM/BBBB)', example: '15 กันยายน 2569' },
  { value: 'thai_currency', label: 'จำนวนเงิน (Currency)', example: '1,200,000.00' },
  { value: 'thai_id_card', label: 'เลขบัตรประชาชน', example: '1-2345-67890-12-3' },
  { value: 'thai_phone', label: 'เบอร์โทรศัพท์', example: '081-234-5678' },
  { value: 'uppercase', label: 'ตัวพิมพ์ใหญ่ (UPPER)', example: 'SAMMAKORN' },
  { value: 'lowercase', label: 'ตัวพิมพ์เล็ก (lower)', example: 'sammakorn' },
] as const;

export const DataSourceTypes = ['json', 'sql'] as const;
export type DataSourceType = (typeof DataSourceTypes)[number];

export const FieldMappingDtoSchema = z.object({
  id: z.string().uuid(),
  templateId: z.string().uuid(),
  placeholder: z.string().min(1, 'ต้องระบุ Placeholder'),
  sourcePath: z.string(),
  label: z.string().min(1, 'ต้องระบุ Label คำอธิบาย'),
  required: z.boolean().default(false),
  defaultValue: z.string().nullable().optional(),
  transform: z.string().nullable().optional(),
  sortOrder: z.number().int().default(0),
  dataSourceType: z.enum(DataSourceTypes).default('json'),
  datasetAlias: z.string().max(50).nullable().optional(),
  resultPath: z.string().nullable().optional(),
  mathExpression: z.string().nullable().optional(),
});
export type FieldMappingDto = z.infer<typeof FieldMappingDtoSchema>;

export const SaveFieldMappingItemSchema = z.object({
  placeholder: z.string().min(1, 'ต้องระบุ Placeholder'),
  sourcePath: z.string(),
  label: z.string().min(1, 'ต้องระบุ Label คำอธิบาย'),
  required: z.boolean().default(false),
  defaultValue: z.string().nullable().optional(),
  transform: z.string().nullable().optional(),
  sortOrder: z.number().int().default(0),
  dataSourceType: z.enum(DataSourceTypes).default('json'),
  datasetAlias: z.string().max(50).nullable().optional(),
  resultPath: z.string().nullable().optional(),
  mathExpression: z.string().nullable().optional(),
});
export type SaveFieldMappingItem = z.infer<typeof SaveFieldMappingItemSchema>;

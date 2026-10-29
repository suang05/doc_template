import { z } from 'zod';

export const ApiKeyItemSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1, 'ต้องระบุชื่อคีย์'),
  callerApp: z.string().min(1, 'ต้องระบุแอปพลิเคชันต้นทาง'),
  isActive: z.boolean().default(true),
  lastUsedAt: z.string().nullable().optional(),
  createdAt: z.string(),
});
export type ApiKeyItem = z.infer<typeof ApiKeyItemSchema>;

export const CreateApiKeyRequestSchema = z.object({
  name: z.string().min(1, 'ต้องระบุชื่อคีย์ เช่น Sales Production Key'),
  callerApp: z.string().min(1, 'ต้องระบุ Caller App เช่น sales-app, hr-app'),
});
export type CreateApiKeyRequest = z.infer<typeof CreateApiKeyRequestSchema>;

export const CreatedApiKeyResponseSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  callerApp: z.string(),
  key: z.string(),
});
export type CreatedApiKeyResponse = z.infer<typeof CreatedApiKeyResponseSchema>;

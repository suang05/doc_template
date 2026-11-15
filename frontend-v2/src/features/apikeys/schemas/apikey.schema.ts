import { z } from 'zod';
import { apiKeyScopes } from '@/constants/domain';

export const ApiKeyItemSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1, 'ต้องระบุชื่อคีย์'),
  callerApp: z.string().min(1, 'ต้องระบุแอปพลิเคชันต้นทาง'),
  isActive: z.boolean().default(true),
  lastUsedAt: z.string().nullable().optional(),
  createdAt: z.string(),
  scope: z.enum(apiKeyScopes).default('ReadWrite'),
  maskedKey: z.string().default(''),
});
export type ApiKeyItem = z.infer<typeof ApiKeyItemSchema>;

export const CreateApiKeyRequestSchema = z.object({
  name: z.string().min(1, 'ต้องระบุชื่อคีย์ เช่น Sales Production Key'),
  callerApp: z.string().min(1, 'ต้องระบุ Caller App เช่น sales-app, hr-app'),
  scope: z.enum(apiKeyScopes).default('ReadWrite'),
  expiresAt: z.string().nullable().optional(),
});
export type CreateApiKeyFormInput = z.infer<typeof CreateApiKeyRequestSchema>;

export const CreatedApiKeyResponseSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  callerApp: z.string(),
  plainTextKey: z.string(),
  scope: z.string().default('ReadWrite'),
  expiresAt: z.string().nullable().optional(),
  key: z.string().nullable().optional(),
});
export type CreatedApiKeyResponse = z.infer<typeof CreatedApiKeyResponseSchema>;

import { z } from 'zod';
import { apiKeyScopes } from '@/constants/domain';

export const CreateApiKeyRequestSchema = z.object({
  name: z.string().trim().min(1, 'ต้องระบุชื่อคีย์ เช่น Sales Production Key'),
  callerApp: z.string().trim().min(1, 'ต้องระบุ Caller App เช่น sales-app, hr-app'),
  scope: z.enum(apiKeyScopes).default('ReadWrite'),
  expiresAt: z.string().nullable().optional(),
});

export type CreateApiKeyFormInput = z.infer<typeof CreateApiKeyRequestSchema>;

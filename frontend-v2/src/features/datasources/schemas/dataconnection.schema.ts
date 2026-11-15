import { z } from 'zod';
import { databaseProviders } from '@/constants/domain';

export const createDataConnectionSchema = z.object({
  name: z.string().trim().min(1, 'ต้องระบุชื่อ Connection'),
  provider: z.enum(databaseProviders),
  connectionString: z.string().trim().min(1, 'ต้องระบุ Connection String'),
});
export type CreateDataConnectionRequest = z.infer<typeof createDataConnectionSchema>;

export const updateDataConnectionSchema = z.object({
  name: z.string().trim().min(1, 'ต้องระบุชื่อ Connection').optional(),
  provider: z.enum(databaseProviders).optional(),
  connectionString: z.string().optional(),
});
export type UpdateDataConnectionRequest = z.infer<typeof updateDataConnectionSchema>;

export const testDataConnectionSchema = z.object({
  provider: z.enum(databaseProviders),
  connectionString: z.string().trim().min(1, 'ต้องระบุ Connection String'),
});
export type TestDataConnectionRequest = z.infer<typeof testDataConnectionSchema>;

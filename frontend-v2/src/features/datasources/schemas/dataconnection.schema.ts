import { z } from 'zod';
import { databaseProviders } from '@/constants/domain';

export const dataConnectionSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1, 'Name is required'),
  provider: z.string().min(1, 'Provider is required'),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

export type DataConnection = z.infer<typeof dataConnectionSchema>;

export const createDataConnectionSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  provider: z.enum(databaseProviders),
  connectionString: z.string().min(1, 'Connection string is required'),
});

export type CreateDataConnectionRequest = z.infer<typeof createDataConnectionSchema>;

export const updateDataConnectionSchema = z.object({
  name: z.string().min(1, 'Name is required').optional(),
  provider: z.enum(databaseProviders).optional(),
  connectionString: z.string().optional(),
});

export type UpdateDataConnectionRequest = z.infer<typeof updateDataConnectionSchema>;

export const testDataConnectionSchema = z.object({
  provider: z.enum(databaseProviders),
  connectionString: z.string().min(1, 'Connection string is required'),
});

export type TestDataConnectionRequest = z.infer<typeof testDataConnectionSchema>;

export const testDataConnectionResponseSchema = z.object({
  success: z.boolean(),
  message: z.string().nullable().optional(),
  error: z.string().nullable().optional(),
});

export type TestDataConnectionResponse = z.infer<typeof testDataConnectionResponseSchema>;

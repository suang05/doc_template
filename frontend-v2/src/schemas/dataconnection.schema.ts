import { z } from 'zod';

export const dataConnectionSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1, 'Name is required'),
  provider: z.string().min(1, 'Provider is required'),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

export type DataConnection = z.infer<typeof dataConnectionSchema>;

export const createDataConnectionSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  provider: z.string().min(1, 'Provider is required'),
  connectionString: z.string().min(1, 'Connection string is required'),
});

export type CreateDataConnectionRequest = z.infer<typeof createDataConnectionSchema>;

export const updateDataConnectionSchema = z.object({
  name: z.string().min(1, 'Name is required').optional(),
  provider: z.string().min(1, 'Provider is required').optional(),
  connectionString: z.string().optional(),
});

export type UpdateDataConnectionRequest = z.infer<typeof updateDataConnectionSchema>;

export const testDataConnectionSchema = z.object({
  provider: z.string().min(1, 'Provider is required'),
  connectionString: z.string().min(1, 'Connection string is required'),
});

export type TestDataConnectionRequest = z.infer<typeof testDataConnectionSchema>;

export const testDataConnectionResponseSchema = z.object({
  success: z.boolean(),
  message: z.string(),
});

export type TestDataConnectionResponse = z.infer<typeof testDataConnectionResponseSchema>;

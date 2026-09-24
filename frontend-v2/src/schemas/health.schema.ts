import { z } from 'zod';

export const HealthStatusSchema = z.enum(['healthy', 'degraded', 'unhealthy']);
export type HealthStatus = z.infer<typeof HealthStatusSchema>;

export const HealthCheckSchema = z.object({
  name: z.string(),
  status: HealthStatusSchema,
  description: z.string().nullable(),
  durationMs: z.number().int(),
});
export type HealthCheck = z.infer<typeof HealthCheckSchema>;

export const HealthReportSchema = z.object({
  status: HealthStatusSchema,
  checks: z.array(HealthCheckSchema),
  totalDurationMs: z.number().int(),
});
export type HealthReport = z.infer<typeof HealthReportSchema>;

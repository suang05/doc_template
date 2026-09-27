import { z } from 'zod';

export const loginRequestSchema = z.object({
  email: z.string().email('อีเมลไม่ถูกต้อง'),
  password: z.string().min(1, 'กรุณากรอกรหัสผ่าน'),
});

export const loginResponseSchema = z.object({
  token: z.string(),
  expiresAt: z.string(),
  user: z.object({
    email: z.string(),
    firstName: z.string(),
    lastName: z.string(),
  }),
});

export type LoginRequest = z.infer<typeof loginRequestSchema>;
export type LoginResponse = z.infer<typeof loginResponseSchema>;
export type AuthUser = LoginResponse['user'];

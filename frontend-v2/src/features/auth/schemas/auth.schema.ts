import { z } from 'zod';

export const loginRequestSchema = z.object({
  email: z.string().email('อีเมลไม่ถูกต้อง'),
  password: z.string().min(1, 'กรุณากรอกรหัสผ่าน'),
});

export type LoginRequest = z.infer<typeof loginRequestSchema>;

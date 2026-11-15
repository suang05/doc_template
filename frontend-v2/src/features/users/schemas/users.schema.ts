import { z } from 'zod';
import { userRoles } from '@/constants/domain';

export const InviteUserRequestSchema = z.object({
  email: z.string().email('กรุณากรอกอีเมลที่ถูกต้อง'),
  password: z.string().min(8, 'รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร'),
  firstName: z.string().min(1, 'กรุณากรอกชื่อ'),
  lastName: z.string().min(1, 'กรุณากรอกนามสกุล'),
  role: z.enum(userRoles),
});

export type InviteUserFormInput = z.infer<typeof InviteUserRequestSchema>;

import { z } from 'zod';
import { userRoles, type UserRole } from '@/constants/domain';

export { userRoles, type UserRole };

export const UserListItemSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  firstName: z.string(),
  lastName: z.string(),
  role: z.enum(userRoles),
  isActive: z.boolean(),
  createdAt: z.string(),
});
export type UserListItem = z.infer<typeof UserListItemSchema>;

export const InviteUserRequestSchema = z.object({
  email: z.string().email('กรุณากรอกอีเมลที่ถูกต้อง'),
  password: z.string().min(8, 'รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร'),
  firstName: z.string().min(1, 'กรุณากรอกชื่อ'),
  lastName: z.string().min(1, 'กรุณากรอกนามสกุล'),
  role: z.enum(userRoles),
});
export type InviteUserFormInput = z.infer<typeof InviteUserRequestSchema>;

export const UpdateUserRoleRequestSchema = z.object({
  role: z.enum(userRoles),
});
export type UpdateUserRoleFormInput = z.infer<typeof UpdateUserRoleRequestSchema>;

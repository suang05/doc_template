import { z } from 'zod';

export const userRoles = ['Admin', 'Editor', 'Viewer'] as const;
export type UserRole = (typeof userRoles)[number];

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
export type InviteUserRequest = z.infer<typeof InviteUserRequestSchema>;

export const UpdateUserRoleRequestSchema = z.object({
  role: z.enum(userRoles),
});
export type UpdateUserRoleRequest = z.infer<typeof UpdateUserRoleRequestSchema>;

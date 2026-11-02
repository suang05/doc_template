import { apiClient } from '@/lib/api/client';
import { ApiResponse, UserListItem, InviteUserRequest, UpdateUserRoleRequest } from '@/types/api';

export async function listUsers(): Promise<UserListItem[]> {
  const res = await apiClient<ApiResponse<UserListItem[]>>('/api/v1/users');
  return res.data;
}

export async function inviteUser(req: InviteUserRequest): Promise<UserListItem> {
  const res = await apiClient<ApiResponse<UserListItem>>('/api/v1/users', {
    method: 'POST',
    body: JSON.stringify(req),
  });
  return res.data;
}

export function updateUserRole(userId: string, req: UpdateUserRoleRequest): Promise<void> {
  return apiClient<void>(`/api/v1/users/${userId}/role`, {
    method: 'PUT',
    body: JSON.stringify(req),
  });
}

export function removeUser(userId: string): Promise<void> {
  return apiClient<void>(`/api/v1/users/${userId}`, { method: 'DELETE' });
}

export function setUserStatus(userId: string, isActive: boolean): Promise<void> {
  return apiClient<void>(`/api/v1/users/${userId}/status`, {
    method: 'PATCH',
    body: JSON.stringify(isActive),
  });
}

import { apiClient } from './client';
import type { UserListItem, InviteUserRequest, UpdateUserRoleRequest } from '@/types/api';

export function listUsers(): Promise<UserListItem[]> {
  return apiClient<UserListItem[]>('/api/users');
}

export function inviteUser(req: InviteUserRequest): Promise<UserListItem> {
  return apiClient<UserListItem>('/api/users', {
    method: 'POST',
    body: JSON.stringify(req),
  });
}

export function updateUserRole(userId: string, req: UpdateUserRoleRequest): Promise<void> {
  return apiClient<void>(`/api/users/${userId}/role`, {
    method: 'PUT',
    body: JSON.stringify(req),
  });
}

export function removeUser(userId: string): Promise<void> {
  return apiClient<void>(`/api/users/${userId}`, { method: 'DELETE' });
}

export function setUserStatus(userId: string, isActive: boolean): Promise<void> {
  return apiClient<void>(`/api/users/${userId}/status`, {
    method: 'PATCH',
    body: JSON.stringify(isActive),
  });
}

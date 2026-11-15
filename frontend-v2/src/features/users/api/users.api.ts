import { apiClient, apiRequestWithMeta } from '@/lib/api/client';
import type {
  ApiEnvelope,
  InviteUserRequest,
  ResourceWithETag,
  SetUserStatusRequest,
  UpdateUserRoleRequest,
  UserDto,
} from '@/lib/api/types';

export async function listUsers(): Promise<UserDto[]> {
  const res = await apiClient<ApiEnvelope<UserDto[]>>('/api/v1/users');
  return res.data;
}

export async function getUserWithETag(userId: string): Promise<ResourceWithETag<UserDto>> {
  const res = await apiRequestWithMeta<ApiEnvelope<UserDto>>(`/api/v1/users/${userId}`);
  return { ...res, data: res.data.data };
}

export async function getManagementUserWithETag(
  userId: string,
): Promise<ResourceWithETag<UserDto>> {
  const res = await apiRequestWithMeta<ApiEnvelope<UserDto>>(
    `/api/v1/management/users/${userId}`,
  );
  return { ...res, data: res.data.data };
}

export async function inviteUser(
  req: InviteUserRequest,
  idempotencyKey?: string,
): Promise<UserDto> {
  const res = await apiClient<ApiEnvelope<UserDto>>('/api/v1/users', {
    method: 'POST',
    body: JSON.stringify(req),
    idempotencyKey,
  });
  return res.data;
}

export async function updateUserRole(
  userId: string,
  req: UpdateUserRoleRequest,
  ifMatch?: string | null,
): Promise<UserDto> {
  const res = await apiClient<ApiEnvelope<UserDto>>(`/api/v1/users/${userId}/role`, {
    method: 'PUT',
    body: JSON.stringify(req),
    ifMatch,
  });
  return res.data;
}

export function removeUser(userId: string, ifMatch?: string | null): Promise<void> {
  return apiClient<void>(`/api/v1/users/${userId}`, {
    method: 'DELETE',
    ifMatch,
  });
}

export async function setUserStatus(
  userId: string,
  req: SetUserStatusRequest,
  ifMatch?: string | null,
): Promise<UserDto> {
  const res = await apiClient<ApiEnvelope<UserDto>>(`/api/v1/management/users/${userId}/status`, {
    method: 'PATCH',
    body: JSON.stringify(req),
    ifMatch,
  });
  return res.data;
}

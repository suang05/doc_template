'use client';

import { useCallback } from 'react';
import { useQuery } from '@tanstack/react-query';
import { listUsers } from '@/lib/api/users.api';
import type { UserListItem } from '@/types/api';

export const USERS_QUERY_KEY = ['users'] as const;

export interface UseUsersReturn {
  users: UserListItem[];
  loading: boolean;
  error: string | null;
  reload: () => Promise<void>;
}

export function useUsers(): UseUsersReturn {
  const {
    data: users = [],
    isLoading: loading,
    error: rawError,
    refetch,
  } = useQuery<UserListItem[], Error>({
    queryKey: USERS_QUERY_KEY,
    queryFn: async () => {
      return await listUsers();
    },
  });

  const reload = useCallback(async () => {
    await refetch();
  }, [refetch]);

  const error = rawError ? (rawError.message || 'โหลดข้อมูลไม่สำเร็จ') : null;

  return { users, loading, error, reload };
}

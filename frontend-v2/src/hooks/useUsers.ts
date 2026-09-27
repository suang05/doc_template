'use client';

import { useState, useCallback, useEffect } from 'react';
import { listUsers } from '@/lib/api/users.api';
import type { UserListItem } from '@/types/api';

export interface UseUsersReturn {
  users: UserListItem[];
  loading: boolean;
  error: string | null;
  reload: () => Promise<void>;
}

export function useUsers(): UseUsersReturn {
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await listUsers();
      setUsers(data);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'โหลดข้อมูลไม่สำเร็จ');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return { users, loading, error, reload: load };
}

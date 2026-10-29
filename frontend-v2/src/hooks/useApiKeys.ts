'use client';

import { useState, useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiKeyItem, CreatedApiKeyResponse } from '@/types/api';
import { apiKeysApi } from '@/lib/api/apikeys.api';

export const API_KEYS_QUERY_KEY = ['apikeys'] as const;

export function useApiKeys() {
  const queryClient = useQueryClient();
  const [newlyCreatedKey, setNewlyCreatedKey] = useState<CreatedApiKeyResponse | null>(null);

  const {
    data: keys = [],
    isLoading: loading,
    error: rawError,
    refetch,
  } = useQuery<ApiKeyItem[], Error>({
    queryKey: API_KEYS_QUERY_KEY,
    queryFn: async () => {
      return await apiKeysApi.listKeys();
    },
  });

  const createMutation = useMutation({
    mutationFn: ({ name, callerApp }: { name: string; callerApp: string }) =>
      apiKeysApi.createKey({ name, callerApp }),
    onSuccess: (res) => {
      setNewlyCreatedKey(res);
      queryClient.invalidateQueries({ queryKey: API_KEYS_QUERY_KEY });
    },
  });

  const revokeMutation = useMutation({
    mutationFn: (id: string) => apiKeysApi.revokeKey(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: API_KEYS_QUERY_KEY });
    },
    onError: (err: unknown) => {
      alert(err instanceof Error ? err.message : 'เพิกถอน API Key ล้มเหลว');
    },
  });

  const createKey = useCallback(
    async (name: string, callerApp: string) => {
      return await createMutation.mutateAsync({ name, callerApp });
    },
    [createMutation]
  );

  const revokeKey = useCallback(
    async (id: string) => {
      await revokeMutation.mutateAsync(id);
    },
    [revokeMutation]
  );

  const refresh = useCallback(async () => {
    await refetch();
  }, [refetch]);

  const error = rawError ? (rawError.message || 'ไม่สามารถโหลดรายการ API Keys ได้') : null;

  return {
    keys,
    loading: loading || createMutation.isPending || revokeMutation.isPending,
    error,
    newlyCreatedKey,
    setNewlyCreatedKey,
    createKey,
    revokeKey,
    refresh,
  };
}

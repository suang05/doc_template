'use client';

import { useState, useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { ApiKeyDto, CreateApiKeyRequest, CreateApiKeyResultDto } from '@/lib/api/types';
import { apiKeysApi } from '../api/apikeys.api';

export const API_KEYS_QUERY_KEY = ['apikeys'] as const;

export function useApiKeys() {
  const queryClient = useQueryClient();
  const [newlyCreatedKey, setNewlyCreatedKey] = useState<CreateApiKeyResultDto | null>(null);

  const {
    data: keys = [],
    isLoading: loading,
    error: rawError,
    refetch,
  } = useQuery<ApiKeyDto[], Error>({
    queryKey: API_KEYS_QUERY_KEY,
    queryFn: () => apiKeysApi.listKeys(),
  });

  const createMutation = useMutation({
    mutationFn: ({
      request,
      idempotencyKey,
    }: {
      request: CreateApiKeyRequest;
      idempotencyKey: string;
    }) => apiKeysApi.createKey(request, idempotencyKey),
    onSuccess: (res) => {
      setNewlyCreatedKey(res);
      void queryClient.invalidateQueries({ queryKey: API_KEYS_QUERY_KEY });
    },
  });

  const revokeMutation = useMutation({
    mutationFn: ({ id, ifMatch }: { id: string; ifMatch?: string | null }) =>
      apiKeysApi.revokeKey(id, ifMatch),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: API_KEYS_QUERY_KEY });
    },
  });

  const createKey = useCallback(
    async (request: CreateApiKeyRequest, idempotencyKey?: string) => {
      return await createMutation.mutateAsync({
        request,
        idempotencyKey: idempotencyKey || crypto.randomUUID(),
      });
    },
    [createMutation],
  );

  const revokeKey = useCallback(
    async (id: string, ifMatch?: string | null) => {
      await revokeMutation.mutateAsync({ id, ifMatch });
    },
    [revokeMutation],
  );

  const refresh = useCallback(async () => {
    await refetch();
  }, [refetch]);

  const error = rawError ? rawError.message || 'ไม่สามารถโหลดรายการ API Keys ได้' : null;

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

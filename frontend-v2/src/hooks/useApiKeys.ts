'use client';

import { useState, useEffect, useCallback } from 'react';
import { ApiKeyItem, CreatedApiKeyResponse } from '@/types/api';
import { apiKeysApi } from '@/lib/api/apikeys.api';

export function useApiKeys() {
  const [keys, setKeys] = useState<ApiKeyItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [newlyCreatedKey, setNewlyCreatedKey] = useState<CreatedApiKeyResponse | null>(null);

  const fetchKeys = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await apiKeysApi.listKeys();
      setKeys(list);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'ไม่สามารถโหลดรายการ API Keys ได้');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchKeys();
  }, [fetchKeys]);

  const createKey = useCallback(async (name: string, callerApp: string) => {
    try {
      const res = await apiKeysApi.createKey({ name, callerApp });
      setNewlyCreatedKey(res);
      await fetchKeys();
      return res;
    } catch (err: unknown) {
      throw err;
    }
  }, [fetchKeys]);

  const revokeKey = useCallback(async (id: string) => {
    try {
      await apiKeysApi.revokeKey(id);
      await fetchKeys();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'เพิกถอน API Key ล้มเหลว');
    }
  }, [fetchKeys]);

  return {
    keys,
    loading,
    error,
    newlyCreatedKey,
    setNewlyCreatedKey,
    createKey,
    revokeKey,
    refresh: fetchKeys,
  };
}

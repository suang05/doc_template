'use client';

import { useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { dataconnectionsApi } from '@/lib/api/dataconnections.api';
import type {
  DataConnection,
  CreateDataConnectionRequest,
  UpdateDataConnectionRequest,
  TestDataConnectionRequest,
} from '@/types/api';

export const DATA_CONNECTIONS_QUERY_KEY = ['dataconnections'] as const;

export function useDataConnections() {
  const queryClient = useQueryClient();

  const {
    data: connections = [],
    isLoading,
    error: queryError,
    refetch,
  } = useQuery<DataConnection[], Error>({
    queryKey: DATA_CONNECTIONS_QUERY_KEY,
    queryFn: async () => {
      const data = await dataconnectionsApi.getAll();
      return Array.isArray(data) ? data : [];
    },
  });

  const createMutation = useMutation({
    mutationFn: (data: CreateDataConnectionRequest) => dataconnectionsApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: DATA_CONNECTIONS_QUERY_KEY });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateDataConnectionRequest }) =>
      dataconnectionsApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: DATA_CONNECTIONS_QUERY_KEY });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => dataconnectionsApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: DATA_CONNECTIONS_QUERY_KEY });
    },
  });

  const fetchConnections = useCallback(async () => {
    await refetch();
  }, [refetch]);

  const createConnection = useCallback(
    async (data: CreateDataConnectionRequest) => {
      return await createMutation.mutateAsync(data);
    },
    [createMutation]
  );

  const updateConnection = useCallback(
    async (id: string, data: UpdateDataConnectionRequest) => {
      return await updateMutation.mutateAsync({ id, data });
    },
    [updateMutation]
  );

  const deleteConnection = useCallback(
    async (id: string) => {
      await deleteMutation.mutateAsync(id);
    },
    [deleteMutation]
  );

  const testConnection = useCallback(async (data: TestDataConnectionRequest) => {
    return await dataconnectionsApi.test(data);
  }, []);

  const error =
    queryError?.message ||
    createMutation.error?.message ||
    updateMutation.error?.message ||
    deleteMutation.error?.message ||
    null;

  return {
    connections,
    isLoading: isLoading || createMutation.isPending || updateMutation.isPending || deleteMutation.isPending,
    error,
    fetchConnections,
    createConnection,
    updateConnection,
    deleteConnection,
    testConnection,
  };
}

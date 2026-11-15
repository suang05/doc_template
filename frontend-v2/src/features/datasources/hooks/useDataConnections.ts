'use client';

import { useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { dataconnectionsApi } from '../api/dataconnections.api';
import type {
  CreateDataConnectionRequest,
  DataConnectionDto,
  TestDataConnectionRequest,
  UpdateDataConnectionRequest,
} from '@/lib/api/types';

export const DATA_CONNECTIONS_QUERY_KEY = ['dataconnections'] as const;

export function useDataConnections() {
  const queryClient = useQueryClient();

  const {
    data: connections = [],
    isLoading,
    error: queryError,
    refetch,
  } = useQuery<DataConnectionDto[], Error>({
    queryKey: DATA_CONNECTIONS_QUERY_KEY,
    queryFn: async () => {
      const data = await dataconnectionsApi.getAll();
      return Array.isArray(data) ? data : [];
    },
  });

  const createMutation = useMutation({
    mutationFn: (data: CreateDataConnectionRequest) => dataconnectionsApi.create(data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: DATA_CONNECTIONS_QUERY_KEY });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({
      id,
      data,
      ifMatch,
    }: {
      id: string;
      data: UpdateDataConnectionRequest;
      ifMatch?: string | null;
    }) => dataconnectionsApi.update(id, data, ifMatch),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: DATA_CONNECTIONS_QUERY_KEY });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: ({ id, ifMatch }: { id: string; ifMatch?: string | null }) =>
      dataconnectionsApi.delete(id, ifMatch),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: DATA_CONNECTIONS_QUERY_KEY });
    },
  });

  const fetchConnections = useCallback(async () => {
    await refetch();
  }, [refetch]);

  const createConnection = useCallback(
    async (data: CreateDataConnectionRequest) => {
      return await createMutation.mutateAsync(data);
    },
    [createMutation],
  );

  const updateConnection = useCallback(
    async (id: string, data: UpdateDataConnectionRequest, ifMatch?: string | null) => {
      return await updateMutation.mutateAsync({ id, data, ifMatch });
    },
    [updateMutation],
  );

  const deleteConnection = useCallback(
    async (id: string, ifMatch?: string | null) => {
      await deleteMutation.mutateAsync({ id, ifMatch });
    },
    [deleteMutation],
  );

  const testConnection = useCallback(async (data: TestDataConnectionRequest) => {
    return await dataconnectionsApi.test(data);
  }, []);

  const checkSavedConnection = useCallback(async (id: string) => {
    return await dataconnectionsApi.checkConnectivity(id);
  }, []);

  const error =
    queryError?.message ||
    createMutation.error?.message ||
    updateMutation.error?.message ||
    deleteMutation.error?.message ||
    null;

  return {
    connections,
    isLoading:
      isLoading ||
      createMutation.isPending ||
      updateMutation.isPending ||
      deleteMutation.isPending,
    error,
    fetchConnections,
    createConnection,
    updateConnection,
    deleteConnection,
    testConnection,
    checkSavedConnection,
  };
}

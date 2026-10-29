'use client';

import { useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { datasetsApi } from '@/lib/api/datasets.api';
import type { Dataset, CreateDatasetRequest } from '@/types/api';

export const DATASETS_QUERY_KEY = ['datasets'] as const;

export function useDatasets() {
  const queryClient = useQueryClient();

  const {
    data: datasets = [],
    isLoading,
    error: queryError,
    refetch,
  } = useQuery<Dataset[], Error>({
    queryKey: DATASETS_QUERY_KEY,
    queryFn: async () => {
      const data = await datasetsApi.getAll();
      return data ?? [];
    },
  });

  const createMutation = useMutation({
    mutationFn: (data: CreateDatasetRequest) => datasetsApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: DATASETS_QUERY_KEY });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: CreateDatasetRequest }) =>
      datasetsApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: DATASETS_QUERY_KEY });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => datasetsApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: DATASETS_QUERY_KEY });
    },
  });

  const fetchDatasets = useCallback(async () => {
    await refetch();
  }, [refetch]);

  const createDataset = useCallback(
    async (data: CreateDatasetRequest) => {
      return await createMutation.mutateAsync(data);
    },
    [createMutation]
  );

  const updateDataset = useCallback(
    async (id: string, data: CreateDatasetRequest) => {
      return await updateMutation.mutateAsync({ id, data });
    },
    [updateMutation]
  );

  const deleteDataset = useCallback(
    async (id: string) => {
      await deleteMutation.mutateAsync(id);
    },
    [deleteMutation]
  );

  const error =
    queryError?.message ||
    createMutation.error?.message ||
    updateMutation.error?.message ||
    deleteMutation.error?.message ||
    null;

  return {
    datasets,
    isLoading: isLoading || createMutation.isPending || updateMutation.isPending || deleteMutation.isPending,
    error,
    fetchDatasets,
    createDataset,
    updateDataset,
    deleteDataset,
  };
}

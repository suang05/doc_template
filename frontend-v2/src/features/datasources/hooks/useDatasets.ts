'use client';

import { useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { datasetsApi, type DryRunDatasetPayload } from '../api/datasets.api';
import type {
  CreateDatasetRequest,
  DatasetDto,
  UpdateDatasetRequest,
} from '@/lib/api/types';

export const DATASETS_QUERY_KEY = ['datasets'] as const;

export function useDatasets() {
  const queryClient = useQueryClient();

  const {
    data: datasets = [],
    isLoading,
    error: queryError,
    refetch,
  } = useQuery<DatasetDto[], Error>({
    queryKey: DATASETS_QUERY_KEY,
    queryFn: async () => {
      const data = await datasetsApi.getAll();
      return data ?? [];
    },
  });

  const createMutation = useMutation({
    mutationFn: (data: CreateDatasetRequest) => datasetsApi.create(data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: DATASETS_QUERY_KEY });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({
      id,
      data,
      ifMatch,
    }: {
      id: string;
      data: UpdateDatasetRequest;
      ifMatch?: string | null;
    }) => datasetsApi.update(id, data, ifMatch),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: DATASETS_QUERY_KEY });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: ({ id, ifMatch }: { id: string; ifMatch?: string | null }) =>
      datasetsApi.delete(id, ifMatch),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: DATASETS_QUERY_KEY });
    },
  });

  const fetchDatasets = useCallback(async () => {
    await refetch();
  }, [refetch]);

  const createDataset = useCallback(
    async (data: CreateDatasetRequest) => {
      return await createMutation.mutateAsync(data);
    },
    [createMutation],
  );

  const updateDataset = useCallback(
    async (id: string, data: UpdateDatasetRequest, ifMatch?: string | null) => {
      return await updateMutation.mutateAsync({ id, data, ifMatch });
    },
    [updateMutation],
  );

  const deleteDataset = useCallback(
    async (id: string, ifMatch?: string | null) => {
      await deleteMutation.mutateAsync({ id, ifMatch });
    },
    [deleteMutation],
  );

  const dryRunDataset = useCallback(async (data: DryRunDatasetPayload) => {
    return await datasetsApi.dryRun(data);
  }, []);

  const error =
    queryError?.message ||
    createMutation.error?.message ||
    updateMutation.error?.message ||
    deleteMutation.error?.message ||
    null;

  return {
    datasets,
    isLoading:
      isLoading ||
      createMutation.isPending ||
      updateMutation.isPending ||
      deleteMutation.isPending,
    error,
    fetchDatasets,
    createDataset,
    updateDataset,
    deleteDataset,
    dryRunDataset,
  };
}

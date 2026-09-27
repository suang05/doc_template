import { useState, useCallback } from 'react';
import { datasetsApi } from '@/lib/api/datasets.api';
import type { Dataset, CreateDatasetRequest } from '@/types/api';

export function useDatasets() {
  const [datasets, setDatasets] = useState<Dataset[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchDatasets = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await datasetsApi.getAll();
      setDatasets(data ?? []);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to fetch datasets');
      setDatasets([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const createDataset = async (data: CreateDatasetRequest) => {
    setError(null);
    try {
      const created = await datasetsApi.create(data);
      setDatasets((prev) => [...prev, created]);
      return created;
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create dataset');
      throw err;
    }
  };

  const updateDataset = async (id: string, data: CreateDatasetRequest) => {
    setError(null);
    try {
      const updated = await datasetsApi.update(id, data);
      setDatasets((prev) => prev.map((d) => (d.id === id ? updated : d)));
      return updated;
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update dataset');
      throw err;
    }
  };

  const deleteDataset = async (id: string) => {
    setError(null);
    try {
      await datasetsApi.delete(id);
      setDatasets((prev) => prev.filter((d) => d.id !== id));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to delete dataset');
      throw err;
    }
  };

  return {
    datasets,
    isLoading,
    error,
    fetchDatasets,
    createDataset,
    updateDataset,
    deleteDataset,
  };
}

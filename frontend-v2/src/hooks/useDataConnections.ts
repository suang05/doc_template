import { useState, useCallback, useEffect } from 'react';
import { dataconnectionsApi } from '../lib/api/dataconnections.api';
import type {
  DataConnection,
  CreateDataConnectionRequest,
  UpdateDataConnectionRequest,
  TestDataConnectionRequest,
} from '@/types/api';

export function useDataConnections() {
  const [connections, setConnections] = useState<DataConnection[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchConnections = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await dataconnectionsApi.getAll();
      setConnections(Array.isArray(data) ? data : []);
    } catch (err: any) {
      setError(err.message || 'Failed to fetch connections');
    } finally {
      setIsLoading(false);
    }
  }, []);

  const createConnection = async (data: CreateDataConnectionRequest) => {
    setIsLoading(true);
    setError(null);
    try {
      const newConnection = await dataconnectionsApi.create(data);
      setConnections((prev) => [...prev, newConnection]);
      return newConnection;
    } catch (err: any) {
      setError(err.message || 'Failed to create connection');
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const updateConnection = async (id: string, data: UpdateDataConnectionRequest) => {
    setIsLoading(true);
    setError(null);
    try {
      const updated = await dataconnectionsApi.update(id, data);
      setConnections((prev) =>
        prev.map((c) => (c.id === id ? updated : c))
      );
      return updated;
    } catch (err: any) {
      setError(err.message || 'Failed to update connection');
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const deleteConnection = async (id: string) => {
    setIsLoading(true);
    setError(null);
    try {
      await dataconnectionsApi.delete(id);
      setConnections((prev) => prev.filter((c) => c.id !== id));
    } catch (err: any) {
      setError(err.message || 'Failed to delete connection');
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const testConnection = async (data: TestDataConnectionRequest) => {
    try {
      const res = await dataconnectionsApi.test(data);
      return res;
    } catch (err: any) {
      throw err;
    }
  };

  return {
    connections,
    isLoading,
    error,
    fetchConnections,
    createConnection,
    updateConnection,
    deleteConnection,
    testConnection,
  };
}

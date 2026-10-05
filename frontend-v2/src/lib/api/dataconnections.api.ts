import { apiClient } from './client';
import {
  DataConnection,
  CreateDataConnectionRequest,
  UpdateDataConnectionRequest,
  TestDataConnectionRequest,
  TestDataConnectionResponse,
} from '../../schemas/dataconnection.schema';

export const dataconnectionsApi = {
  getAll: () =>
    apiClient<any>('/api/data-connections', { method: 'GET' }).then(res => {
      if (Array.isArray(res)) return res as DataConnection[];
      if (res && Array.isArray(res.data)) return res.data as DataConnection[];
      return [] as DataConnection[];
    }),

  getById: (id: string) =>
    apiClient<any>(`/api/data-connections/${id}`, { method: 'GET' }).then(res => {
      if (res && res.data) return res.data as DataConnection;
      return res as DataConnection;
    }),

  create: (data: CreateDataConnectionRequest) =>
    apiClient<any>('/api/data-connections', {
      method: 'POST',
      body: JSON.stringify(data),
    }).then(res => {
      if (res && res.data) return res.data as DataConnection;
      return res as DataConnection;
    }),

  update: (id: string, data: UpdateDataConnectionRequest) =>
    apiClient<any>(`/api/data-connections/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }).then(res => {
      if (res && res.data) return res.data as DataConnection;
      return res as DataConnection;
    }),

  delete: (id: string) =>
    apiClient<void>(`/api/data-connections/${id}`, { method: 'DELETE' }),

  test: (data: TestDataConnectionRequest) =>
    apiClient<any>('/api/data-connections/test', {
      method: 'POST',
      body: JSON.stringify(data),
    }).then(res => (res?.data ?? res) as TestDataConnectionResponse),
};

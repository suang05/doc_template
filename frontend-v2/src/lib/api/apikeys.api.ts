import { apiClient } from './client';
import {
  ApiKeyItem,
  CreateApiKeyRequest,
  CreatedApiKeyResponse,
} from '@/types/api';

export const apiKeysApi = {
  /**
   * List all API keys
   */
  async listKeys(): Promise<ApiKeyItem[]> {
    const res = await apiClient<{ keys: ApiKeyItem[] }>('/api/api-keys', {
      method: 'GET',
    });
    return res.keys;
  },

  /**
   * Create new API key
   */
  async createKey(request: CreateApiKeyRequest): Promise<CreatedApiKeyResponse> {
    return apiClient<CreatedApiKeyResponse>('/api/api-keys', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  /**
   * Revoke an API key
   */
  async revokeKey(id: string): Promise<{ success: boolean }> {
    return apiClient<{ success: boolean }>(`/api/api-keys/${id}`, {
      method: 'DELETE',
    });
  },
};

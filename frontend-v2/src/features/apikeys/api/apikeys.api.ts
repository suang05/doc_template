import { apiClient } from '@/lib/api/client';
import {
  ApiResponse,
  ApiKeyItem,
  CreateApiKeyRequest,
  CreatedApiKeyResponse,
} from '@/types/api';

export const apiKeysApi = {
  /**
   * List all API keys (SSoT: GET /api/v1/api-keys)
   */
  async listKeys(): Promise<ApiKeyItem[]> {
    const res = await apiClient<ApiResponse<ApiKeyItem[]>>('/api/v1/api-keys', {
      method: 'GET',
    });
    return res.data;
  },

  /**
   * Create new API key (SSoT: POST /api/v1/api-keys)
   */
  async createKey(request: CreateApiKeyRequest): Promise<CreatedApiKeyResponse> {
    const res = await apiClient<ApiResponse<CreatedApiKeyResponse>>('/api/v1/api-keys', {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return res.data;
  },

  /**
   * Revoke an API key (SSoT: DELETE /api/v1/api-keys/{id})
   */
  async revokeKey(id: string): Promise<{ success: boolean }> {
    await apiClient<void>(`/api/v1/api-keys/${id}`, {
      method: 'DELETE',
    });
    return { success: true };
  },
};

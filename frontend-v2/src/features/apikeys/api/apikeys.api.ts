import { apiClient, apiRequestWithMeta } from '@/lib/api/client';
import type {
  ApiEnvelope,
  ApiKeyDto,
  CreateApiKeyRequest,
  CreateApiKeyResultDto,
  ResourceWithETag,
} from '@/lib/api/types';

export const apiKeysApi = {
  /**
   * List all API keys (SSoT: GET /api/v1/api-keys)
   */
  async listKeys(): Promise<ApiKeyDto[]> {
    const res = await apiClient<ApiEnvelope<ApiKeyDto[]>>('/api/v1/api-keys', {
      method: 'GET',
    });
    return res.data;
  },

  /**
   * Get single API key with ETag (SSoT: GET /api/v1/api-keys/{id})
   */
  async getWithETag(id: string): Promise<ResourceWithETag<ApiKeyDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<ApiKeyDto>>(`/api/v1/api-keys/${id}`, {
      method: 'GET',
    });
    return { ...res, data: res.data.data };
  },

  /**
   * Create new API key (SSoT: POST /api/v1/api-keys, idempotent)
   */
  async createKey(
    request: CreateApiKeyRequest,
    idempotencyKey?: string,
  ): Promise<CreateApiKeyResultDto> {
    const res = await apiClient<ApiEnvelope<CreateApiKeyResultDto>>('/api/v1/api-keys', {
      method: 'POST',
      body: JSON.stringify(request),
      idempotencyKey,
    });
    return res.data;
  },

  /**
   * Revoke an API key with optimistic concurrency (SSoT: DELETE /api/v1/api-keys/{id})
   */
  async revokeKey(id: string, ifMatch?: string | null): Promise<{ success: boolean }> {
    await apiClient<void>(`/api/v1/api-keys/${id}`, {
      method: 'DELETE',
      ifMatch,
    });
    return { success: true };
  },
};

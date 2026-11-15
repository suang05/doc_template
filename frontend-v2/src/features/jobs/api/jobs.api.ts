import { apiClient, apiClientBlob, apiRequestWithMeta } from '@/lib/api/client';
import { parseRetryAfterSeconds } from '@/lib/api/problemDetails';
import type {
  ApiEnvelope,
  CreateDocumentJobRequest,
  DocumentJobItemDto,
  DocumentJobListResult,
  DocumentJobResultDto,
  ResourceWithETag,
} from '@/lib/api/types';

export interface DocumentJobResource extends ResourceWithETag<DocumentJobResultDto> {
  retryAfterSeconds?: number;
}

export const jobsApi = {
  async listJobs(params?: {
    status?: string;
    cursor?: string | null;
    limit?: number;
  }): Promise<DocumentJobListResult> {
    const qs = new URLSearchParams();
    if (params?.status) qs.set('status', params.status);
    if (params?.cursor) qs.set('cursor', params.cursor);
    if (params?.limit) qs.set('limit', String(params.limit));
    const query = qs.toString();
    const res = await apiClient<ApiEnvelope<DocumentJobListResult>>(
      `/api/v1/document-jobs${query ? `?${query}` : ''}`,
      { method: 'GET' },
    );
    return res.data;
  },

  async getJobWithETag(jobId: string): Promise<DocumentJobResource> {
    const res = await apiRequestWithMeta<ApiEnvelope<DocumentJobResultDto>>(
      `/api/v1/document-jobs/${jobId}`,
      { method: 'GET' },
    );
    return {
      ...res,
      data: res.data.data,
      retryAfterSeconds: parseRetryAfterSeconds(res.headers.get('retry-after')),
    };
  },

  async createJob(
    request: CreateDocumentJobRequest,
    idempotencyKey?: string,
  ): Promise<DocumentJobResource> {
    const res = await apiRequestWithMeta<ApiEnvelope<DocumentJobResultDto>>(
      '/api/v1/document-jobs',
      {
        method: 'POST',
        body: JSON.stringify(request),
        idempotencyKey: idempotencyKey || crypto.randomUUID(),
      },
    );
    return {
      ...res,
      data: res.data.data,
      retryAfterSeconds: parseRetryAfterSeconds(res.headers.get('retry-after')),
    };
  },

  async listJobItems(jobId: string): Promise<DocumentJobItemDto[]> {
    const res = await apiClient<ApiEnvelope<DocumentJobItemDto[]>>(
      `/api/v1/document-jobs/${jobId}/items`,
      { method: 'GET' },
    );
    return res.data;
  },

  async downloadResult(jobId: string): Promise<{ blob: Blob; fileName?: string | null }> {
    return apiClientBlob(`/api/v1/document-jobs/${jobId}/result`, {
      method: 'GET',
    });
  },

  async cancelJob(jobId: string, ifMatch?: string | null): Promise<DocumentJobResource> {
    const res = await apiRequestWithMeta<ApiEnvelope<DocumentJobResultDto>>(
      `/api/v1/document-jobs/${jobId}:cancel`,
      {
        method: 'POST',
        ifMatch,
      },
    );
    return { ...res, data: res.data.data };
  },

  async retryJob(jobId: string, ifMatch?: string | null): Promise<DocumentJobResource> {
    const res = await apiRequestWithMeta<ApiEnvelope<DocumentJobResultDto>>(
      `/api/v1/document-jobs/${jobId}:retry`,
      {
        method: 'POST',
        ifMatch,
      },
    );
    return { ...res, data: res.data.data };
  },
};

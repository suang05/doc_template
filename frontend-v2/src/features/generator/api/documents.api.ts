import { apiClient, apiClientBlob } from '@/lib/api/client';
import type {
  ApiEnvelope,
  DocumentVersionDto,
  GenerateDocumentResultDto,
  PreviewDocumentRequest,
} from '@/lib/api/types';
import type { GenerateDocumentFormRequest } from '../schemas/document.schema';

export const documentsApi = {
  /**
   * Generate production document synchronously (SSoT: POST /api/v1/documents, idempotent)
   */
  async generateDocument(
    slug: string,
    request: GenerateDocumentFormRequest,
    idempotencyKey?: string,
  ): Promise<GenerateDocumentResultDto> {
    const res = await apiClient<ApiEnvelope<GenerateDocumentResultDto>>('/api/v1/documents', {
      method: 'POST',
      body: JSON.stringify({
        slug,
        payload: request.payload,
        outputFormat: request.outputFormat,
        documentRef: request.documentRef ?? null,
        changeNote: request.changeNote ?? null,
      }),
      idempotencyKey: idempotencyKey || crypto.randomUUID(),
    });
    return {
      ...res.data,
      documentRef: res.data.documentRef ?? null,
    };
  },

  /**
   * Stream live preview PDF (SSoT: POST /api/v1/documents/preview/{slug?})
   */
  async previewDocument(
    slug?: string,
    request: PreviewDocumentRequest = { payload: {} },
    options?: { signal?: AbortSignal },
  ): Promise<Blob> {
    const endpoint = slug ? `/api/v1/documents/preview/${slug}` : '/api/v1/documents/preview';
    const { blob } = await apiClientBlob(endpoint, {
      method: 'POST',
      body: JSON.stringify(request),
      signal: options?.signal,
    });
    return blob;
  },

  /**
   * Get legal audit versions of a document (SSoT: GET /api/v1/document-versions/{documentRef}/versions)
   */
  async getDocumentVersions(documentRef: string): Promise<DocumentVersionDto[]> {
    const res = await apiClient<ApiEnvelope<DocumentVersionDto[]>>(
      `/api/v1/document-versions/${encodeURIComponent(documentRef)}/versions`,
      {
        method: 'GET',
      },
    );
    return res.data;
  },

  /**
   * Download historical document artifact (SSoT: GET /api/v1/document-versions/{documentRef}/versions/{version}/download)
   */
  async downloadDocumentVersion(
    documentRef: string,
    version: number,
  ): Promise<{ blob: Blob; fileName?: string | null }> {
    return apiClientBlob(
      `/api/v1/document-versions/${encodeURIComponent(documentRef)}/versions/${version}/download`,
      {
        method: 'GET',
      },
    );
  },

  async getDownloadUrlByLogId(
    logId: string,
  ): Promise<{ url: string; expiresInSeconds: number }> {
    const res = await apiClient<ApiEnvelope<{ url: string; expiresInSeconds: number }>>(
      `/api/v1/generation-logs/${logId}/artifact/url`,
      {
        method: 'GET',
      },
    );
    return res.data;
  },

  async downloadArtifactFileByLogId(
    logId: string,
  ): Promise<{ blob: Blob; fileName?: string | null }> {
    return apiClientBlob(`/api/v1/generation-logs/${logId}/artifact/file`, {
      method: 'GET',
    });
  },

  /**
   * Render any document to PDF statelessly from file and JSON (SSoT: POST /api/v1/documents/render/stateless)
   */
  async renderStatelessDocument(file: File, jsonData: string): Promise<Blob> {
    const formData = new FormData();
    formData.append('file', file);
    if (jsonData) {
      formData.append('jsonData', jsonData);
    }

    const { blob } = await apiClientBlob('/api/v1/documents/render/stateless', {
      method: 'POST',
      body: formData,
    });
    return blob;
  },
};

import { apiClient, apiClientBlob } from '@/lib/api/client';
import {
  ApiResponse,
  GenerateDocumentRequest,
  GenerateDocumentResponse,
  GenerateDocumentResponseSchema,
  PreviewDocumentRequest,
  DocumentVersionDto,
} from '@/types/api';

export const documentsApi = {
  /**
   * Generate production document (SSoT: POST /api/v1/documents/generate/{slug})
   */
  async generateDocument(slug: string, request: GenerateDocumentRequest): Promise<GenerateDocumentResponse> {
    const res = await apiClient<ApiResponse<GenerateDocumentResponse>>(`/api/v1/documents/generate/${slug}`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return GenerateDocumentResponseSchema.parse(res.data);
  },

  /**
   * Stream live preview PDF (SSoT: POST /api/v1/documents/preview)
   */
  async previewDocument(
    slug?: string,
    request: PreviewDocumentRequest = { payload: {} },
    options?: { signal?: AbortSignal }
  ): Promise<Blob> {
    const endpoint = slug ? `/api/v1/documents/preview/${slug}` : `/api/v1/documents/preview`;
    const { blob } = await apiClientBlob(endpoint, {
      method: 'POST',
      body: JSON.stringify(request),
      signal: options?.signal,
    });
    return blob;
  },

  /**
   * Get legal audit versions of a document (SSoT: GET /api/v1/documents/{documentRef}/versions)
   */
  async getDocumentVersions(documentRef: string): Promise<DocumentVersionDto[]> {
    const res = await apiClient<ApiResponse<DocumentVersionDto[]>>(
      `/api/v1/documents/${documentRef}/versions`,
      {
        method: 'GET',
      }
    );
    return res.data;
  },

  /**
   * Download historical document artifact (SSoT: GET /api/v1/documents/{documentRef}/versions/{version}/download)
   */
  async downloadDocumentVersion(documentRef: string, version: number): Promise<{ blob: Blob; fileName?: string }> {
    return apiClientBlob(`/api/v1/documents/${documentRef}/versions/${version}/download`, {
      method: 'GET',
    });
  },

  async getDownloadUrlByLogId(logId: string): Promise<{ url: string; expiresInSeconds: number }> {
    const res = await apiClient<ApiResponse<{ url: string; expiresInSeconds: number }>>(`/api/v1/documents/download/${logId}`, {
      method: 'GET',
    });
    return res.data;
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

    const { blob } = await apiClientBlob(`/api/v1/documents/render/stateless`, {
      method: 'POST',
      body: formData,
    });
    return blob;
  },
};

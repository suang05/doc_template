import { apiClient, apiClientBlob } from './client';
import {
  GenerateDocumentRequest,
  GenerateDocumentResponse,
  GenerateDocumentResponseSchema,
  PreviewDocumentRequest,
  DocumentVersionDto,
} from '@/types/api';

export const documentsApi = {
  /**
   * Generate production document
   */
  async generateDocument(slug: string, request: GenerateDocumentRequest): Promise<GenerateDocumentResponse> {
    const data = await apiClient<GenerateDocumentResponse>(`/api/documents/generate/${slug}`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return GenerateDocumentResponseSchema.parse(data);
  },

  /**
   * Stream live preview PDF (ephemeral, zero side-effects)
   */
  async previewDocument(
    slug?: string,
    request: PreviewDocumentRequest = { payload: {} },
    options?: { signal?: AbortSignal }
  ): Promise<Blob> {
    const endpoint = slug ? `/api/documents/preview/${slug}` : `/api/documents/preview`;
    const { blob } = await apiClientBlob(endpoint, {
      method: 'POST',
      body: JSON.stringify(request),
      signal: options?.signal,
    });
    return blob;
  },

  /**
   * Get legal audit versions of a document
   */
  async getDocumentVersions(documentRef: string): Promise<DocumentVersionDto[]> {
    const res = await apiClient<{ versions: DocumentVersionDto[] }>(`/api/documents/${documentRef}/versions`, {
      method: 'GET',
    });
    return res.versions;
  },

  /**
   * Download historical document artifact
   */
  async downloadDocumentVersion(documentRef: string, version: number): Promise<{ blob: Blob; fileName?: string }> {
    return apiClientBlob(`/api/documents/${documentRef}/versions/${version}/download`, {
      method: 'GET',
    });
  },

  async getDownloadUrlByLogId(logId: string): Promise<{ url: string; expiresInSeconds: number }> {
    return apiClient<{ url: string; expiresInSeconds: number }>(`/api/documents/download/${logId}`, {
      method: 'GET',
    });
  },

  /**
   * Render any document to PDF statelessly from file and JSON
   */
  async renderStatelessDocument(file: File, jsonData: string): Promise<Blob> {
    const formData = new FormData();
    formData.append('file', file);
    if (jsonData) {
      formData.append('jsonData', jsonData);
    }

    const { blob } = await apiClientBlob(`/api/documents/render/stateless`, {
      method: 'POST',
      body: formData,
    });
    return blob;
  },
};

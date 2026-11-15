import {
  apiClient,
  apiClientBlob,
  apiClientText,
  apiRequestWithMeta,
  ApiError,
} from '@/lib/api/client';
import type {
  ApiEnvelope,
  CommitDraftResultDto,
  FieldMappingDto,
  ResourceWithETag,
  RollbackVersionResultDto,
  SaveFieldMappingItem,
  SaveHtmlRequest,
  SaveHtmlResultDto,
  TemplateDto,
  TemplateSchemaDto,
  TemplateStudioDto,
  TemplateValidationResultDto,
  TemplateVersionDto,
  UpdateTemplateMetadataRequest,
} from '@/lib/api/types';

export const templatesApi = {
  /**
   * List all templates (SSoT: GET /api/v1/templates)
   */
  async listTemplates(options?: RequestInit): Promise<TemplateDto[]> {
    const res = await apiClient<ApiEnvelope<TemplateDto[]>>('/api/v1/templates', {
      method: 'GET',
      ...options,
    });
    return res.data;
  },

  /**
   * Fetch single template metadata along with its strong ETag for If-Match concurrency control.
   */
  async getTemplateWithETag(id: string): Promise<ResourceWithETag<TemplateDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<TemplateDto>>(`/api/v1/templates/${id}`, {
      method: 'GET',
    });
    return { ...res, data: res.data.data };
  },

  /**
   * Create a new template (multipart form upload with per-request Idempotency-Key)
   */
  async createTemplate(
    formData: FormData,
    idempotencyKey?: string,
  ): Promise<{ id: string; slug: string }> {
    const res = await apiClient<ApiEnvelope<{ id: string; slug: string }>>('/api/v1/templates', {
      method: 'POST',
      body: formData,
      idempotencyKey: idempotencyKey ?? crypto.randomUUID(),
    });
    return res.data;
  },

  /**
   * Get template raw HTML content
   */
  async getTemplateHtml(id: string): Promise<string> {
    return apiClientText(`/api/v1/templates/${id}/html`);
  },

  /**
   * Get full studio bundle along with strong ETag from GET /api/v1/templates/{id}/studio
   */
  async getTemplateStudioWithETag(id: string): Promise<ResourceWithETag<TemplateStudioDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<TemplateStudioDto>>(
      `/api/v1/templates/${id}/studio`,
      { method: 'GET' },
    );
    return { ...res, data: res.data.data };
  },

  /**
   * Get full studio bundle (HTML, persisted SamplePayload, DataSchema, Version)
   */
  async getTemplateStudio(id: string): Promise<TemplateStudioDto> {
    const res = await apiClient<ApiEnvelope<TemplateStudioDto>>(`/api/v1/templates/${id}/studio`);
    return res.data;
  },

  /**
   * Get template schema contract (DataSchema & SamplePayload) for any template format
   */
  async getTemplateSchema(id: string): Promise<TemplateSchemaDto> {
    const res = await apiClient<ApiEnvelope<TemplateSchemaDto>>(`/api/v1/templates/${id}/schema`);
    return res.data;
  },

  /**
   * Save template HTML with optimistic concurrency control returning updated ETag.
   */
  async saveTemplateHtmlWithETag(
    id: string,
    request: SaveHtmlRequest,
    ifMatch?: string | null,
  ): Promise<ResourceWithETag<SaveHtmlResultDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<SaveHtmlResultDto>>(
      `/api/v1/templates/${id}/html`,
      {
        method: 'PUT',
        body: JSON.stringify(request),
        ifMatch,
      },
    );
    return { ...res, data: res.data.data };
  },

  /**
   * Save template HTML and optional samplePayload (auto-increments version +1)
   */
  async saveTemplateHtml(
    id: string,
    request: SaveHtmlRequest,
    ifMatch?: string | null,
  ): Promise<{ version: number }> {
    const res = await apiClient<ApiEnvelope<SaveHtmlResultDto>>(`/api/v1/templates/${id}/html`, {
      method: 'PUT',
      body: JSON.stringify(request),
      ifMatch,
    });
    return res.data;
  },

  /**
   * Update template metadata (name, category, isActive) with optional If-Match ETag
   */
  async updateTemplate(
    id: string,
    request: UpdateTemplateMetadataRequest,
    ifMatch?: string | null,
  ): Promise<{ success: boolean }> {
    await apiClient<ApiEnvelope<TemplateDto>>(`/api/v1/templates/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
      ifMatch,
    });
    return { success: true };
  },

  /**
   * Deactivate template (soft delete) with optional If-Match ETag
   */
  async deactivateTemplate(id: string, ifMatch?: string | null): Promise<{ success: boolean }> {
    await apiClient<void>(`/api/v1/templates/${id}`, {
      method: 'DELETE',
      ifMatch,
    });
    return { success: true };
  },

  /**
   * Get field mappings for a template (SSoT: GET /api/v1/templates/{id}/mappings)
   */
  async getTemplateMappings(id: string): Promise<FieldMappingDto[]> {
    const res = await apiClient<ApiEnvelope<FieldMappingDto[]>>(`/api/v1/templates/${id}/mappings`, {
      method: 'GET',
    });
    return res.data;
  },

  /**
   * Save field mappings for a template with optional If-Match ETag
   */
  async saveTemplateMappings(
    id: string,
    mappings: SaveFieldMappingItem[],
    ifMatch?: string | null,
  ): Promise<FieldMappingDto[]> {
    const res = await apiClient<ApiEnvelope<FieldMappingDto[]>>(`/api/v1/templates/${id}/mappings`, {
      method: 'PUT',
      body: JSON.stringify({ mappings }),
      ifMatch,
    });
    return res.data;
  },

  /**
   * Validate HTML template markup and detect fields
   */
  async validateTemplate(id: string, html: string): Promise<TemplateValidationResultDto> {
    const res = await apiClient<ApiEnvelope<TemplateValidationResultDto>>(
      `/api/v1/templates/${id}/validate`,
      {
        method: 'POST',
        body: JSON.stringify({ html }),
      },
    );
    return res.data;
  },

  /**
   * List template revision history (SSoT: GET /api/v1/templates/{id}/versions)
   */
  async listTemplateVersions(id: string): Promise<TemplateVersionDto[]> {
    const res = await apiClient<ApiEnvelope<TemplateVersionDto[]>>(`/api/v1/templates/${id}/versions`, {
      method: 'GET',
    });
    return res.data;
  },

  /**
   * Download original template file
   */
  async downloadTemplate(id: string): Promise<{ blob: Blob; fileName: string | null }> {
    const { blob, fileName } = await apiClientBlob(`/api/v1/templates/${id}/download`, {
      method: 'GET',
    });
    return { blob, fileName };
  },

  async rollbackTemplateVersionWithETag(
    id: string,
    version: number,
    ifMatch?: string | null,
  ): Promise<ResourceWithETag<RollbackVersionResultDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<RollbackVersionResultDto>>(
      `/api/v1/templates/${id}/rollback/${version}`,
      {
        method: 'POST',
        ifMatch,
      },
    );
    return { ...res, data: res.data.data };
  },

  async rollbackTemplateVersion(
    id: string,
    version: number,
    ifMatch?: string | null,
  ): Promise<{ version: number }> {
    const res = await apiClient<ApiEnvelope<RollbackVersionResultDto>>(
      `/api/v1/templates/${id}/rollback/${version}`,
      {
        method: 'POST',
        ifMatch,
      },
    );
    return { version: res.data.version ?? version };
  },

  async scanTemplateFields(id: string): Promise<string[]> {
    const res = await apiClient<ApiEnvelope<{ placeholders: string[] }>>(
      `/api/v1/templates/${id}/scan-fields`,
      {
        method: 'GET',
      },
    );
    return res.data.placeholders;
  },

  async previewMappings(
    id: string,
    sampleData: Record<string, unknown>,
    options?: { signal?: AbortSignal },
  ): Promise<Blob> {
    const { blob } = await apiClientBlob(`/api/v1/templates/${id}/mappings/preview`, {
      method: 'POST',
      body: JSON.stringify({ sampleData }),
      signal: options?.signal,
    });
    return blob;
  },

  // ── Draft Upload Pipeline ─────────────────────────────────────────────────

  async parseDraft(file: File): Promise<{ draftId: string; placeholders: string[] }> {
    const form = new FormData();
    form.append('file', file);
    const res = await apiClient<ApiEnvelope<{ draftId: string; placeholders: string[] }>>(
      '/api/v1/template-drafts/parse',
      {
        method: 'POST',
        body: form,
      },
    );
    return res.data;
  },

  async previewDraft(draftId: string, dataJson: string): Promise<Blob> {
    try {
      const { blob } = await apiClientBlob(`/api/v1/template-drafts/${draftId}/preview`, {
        method: 'POST',
        body: JSON.stringify({ dataJson }),
      });
      return blob;
    } catch (e) {
      if (e instanceof ApiError && e.status === 410) {
        throw new Error('Draft หมดอายุ กรุณาอัปโหลดใหม่');
      }
      throw e;
    }
  },

  async commitDraft(
    draftId: string,
    request: { name: string; slug: string; category?: string | null; mappings: SaveFieldMappingItem[] },
    idempotencyKey?: string,
  ): Promise<CommitDraftResultDto> {
    const res = await apiClient<ApiEnvelope<CommitDraftResultDto>>(
      `/api/v1/template-drafts/${draftId}/commit`,
      {
        method: 'POST',
        body: JSON.stringify(request),
        idempotencyKey: idempotencyKey ?? crypto.randomUUID(),
      },
    );
    return res.data;
  },
};

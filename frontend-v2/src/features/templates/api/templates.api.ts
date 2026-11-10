import { apiClient, apiClientBlob, apiClientText, ApiError } from '@/lib/api/client';
import {
  ApiResponse,
  TemplateDto,
  SaveTemplateHtmlRequest,
  UpdateTemplateRequest,
  TemplateVersionDto,
  TemplateStudioDto,
  TemplateSchemaDto,
  TemplateValidationResult,
  FieldMappingDto,
  SaveFieldMappingItem,
} from '@/types/api';

export const templatesApi = {
  /**
   * List all templates (SSoT: GET /api/v1/templates)
   */
  async listTemplates(options?: RequestInit): Promise<TemplateDto[]> {
    const res = await apiClient<ApiResponse<TemplateDto[]>>('/api/v1/templates', {
      method: 'GET',
      ...options,
    });
    return res.data;
  },

  /**
   * Create new template (SSoT: POST /api/v1/templates)
   */
  async createTemplate(formData: FormData): Promise<{ id: string; slug: string }> {
    const res = await apiClient<ApiResponse<{ id: string; slug: string }>>('/api/v1/templates', {
      method: 'POST',
      body: formData,
    });
    return res.data;
  },

  /**
   * Get template HTML content for Monaco editor (SSoT: GET /api/v1/templates/{id}/html)
   */
  async getTemplateHtml(id: string): Promise<string> {
    return apiClientText(`/api/v1/templates/${id}/html`);
  },

  /**
   * Get full studio bundle (HTML, persisted SamplePayload, DataSchema, Version)
   */
  async getTemplateStudio(id: string): Promise<TemplateStudioDto> {
    const res = await apiClient<ApiResponse<TemplateStudioDto>>(`/api/v1/templates/${id}/studio`);
    return res.data;
  },

  /**
   * Get template schema contract (DataSchema & SamplePayload) for any template format
   */
  async getTemplateSchema(id: string): Promise<TemplateSchemaDto> {
    const res = await apiClient<ApiResponse<TemplateSchemaDto>>(`/api/v1/templates/${id}/schema`);
    return res.data;
  },

  /**
   * Save template HTML and optional samplePayload (auto-increments version +1)
   */
  async saveTemplateHtml(id: string, request: SaveTemplateHtmlRequest): Promise<{ version: number }> {
    const res = await apiClient<ApiResponse<{ version: number }>>(`/api/v1/templates/${id}/html`, {
      method: 'PUT',
      body: JSON.stringify(request),
    });
    return res.data;
  },

  /**
   * Update template metadata (name, category, isActive)
   */
  async updateTemplate(id: string, request: UpdateTemplateRequest): Promise<{ success: boolean }> {
    await apiClient<ApiResponse<TemplateDto>>(`/api/v1/templates/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    });
    return { success: true };
  },

  /**
   * Deactivate template (soft delete)
   */
  async deactivateTemplate(id: string): Promise<{ success: boolean }> {
    await apiClient<void>(`/api/v1/templates/${id}`, {
      method: 'DELETE',
    });
    return { success: true };
  },

  /**
   * Get field mappings for a template (SSoT: GET /api/v1/templates/{id}/mappings)
   */
  async getTemplateMappings(id: string): Promise<FieldMappingDto[]> {
    const res = await apiClient<ApiResponse<FieldMappingDto[]>>(`/api/v1/templates/${id}/mappings`, {
      method: 'GET',
    });
    return res.data;
  },

  /**
   * Save field mappings for a template (SSoT: PUT /api/v1/templates/{id}/mappings)
   */
  async saveTemplateMappings(id: string, mappings: SaveFieldMappingItem[]): Promise<FieldMappingDto[]> {
    const res = await apiClient<ApiResponse<FieldMappingDto[]>>(`/api/v1/templates/${id}/mappings`, {
      method: 'PUT',
      body: JSON.stringify({ mappings }),
    });
    return res.data;
  },

  /**
   * Validate HTML template markup and detect fields
   */
  async validateTemplate(id: string, html: string): Promise<TemplateValidationResult> {
    const res = await apiClient<ApiResponse<TemplateValidationResult>>(`/api/v1/templates/${id}/validate`, {
      method: 'POST',
      body: JSON.stringify({ html }),
    });
    return res.data;
  },

  /**
   * List template revision history (SSoT: GET /api/v1/templates/{id}/versions)
   */
  async listTemplateVersions(id: string): Promise<TemplateVersionDto[]> {
    const res = await apiClient<ApiResponse<TemplateVersionDto[]>>(`/api/v1/templates/${id}/versions`, {
      method: 'GET',
    });
    return res.data;
  },

  /**
   * Download original template file
   */
  async downloadTemplate(id: string): Promise<{ blob: Blob; fileName?: string }> {
    return apiClientBlob(`/api/v1/templates/${id}/download`, {
      method: 'GET',
    });
  },

  async rollbackTemplateVersion(id: string, version: number): Promise<{ version: number }> {
    const res = await apiClient<ApiResponse<{ newVersion: number; version?: number }>>(
      `/api/v1/templates/${id}/rollback/${version}`,
      { method: 'POST' }
    );
    return { version: res.data.newVersion ?? res.data.version ?? version };
  },

  async scanTemplateFields(id: string): Promise<string[]> {
    const res = await apiClient<ApiResponse<{ placeholders: string[] }>>(`/api/v1/templates/${id}/scan-fields`, {
      method: 'GET',
    });
    return res.data.placeholders;
  },

  async previewMappings(
    id: string,
    sampleData: Record<string, unknown>,
    options?: { signal?: AbortSignal }
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
    const res = await apiClient<ApiResponse<{ draftId: string; placeholders: string[] }>>('/api/v1/templates/draft/parse', {
      method: 'POST',
      body: form,
    });
    return res.data;
  },

  async previewDraft(draftId: string, dataJson: string): Promise<Blob> {
    try {
      const { blob } = await apiClientBlob(`/api/v1/templates/draft/${draftId}/preview`, {
        method: 'POST',
        body: JSON.stringify({ dataJson }),
      });
      return blob;
    } catch (e) {
      if (e instanceof ApiError && e.status === 410)
        throw new Error('Draft หมดอายุ กรุณาอัปโหลดใหม่');
      throw e;
    }
  },

  async commitDraft(
    draftId: string,
    request: { name: string; slug: string; category?: string; mappings: SaveFieldMappingItem[] }
  ): Promise<{ templateId: string }> {
    const res = await apiClient<ApiResponse<{ templateId: string }>>(`/api/v1/templates/draft/${draftId}/commit`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return res.data;
  },
};

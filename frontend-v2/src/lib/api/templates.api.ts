import { apiClient, apiClientBlob, apiClientText, ApiError } from './client';
import {
  TemplateDto,
  CreateTemplateRequest,
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
   * List all templates
   */
  async listTemplates(options?: RequestInit): Promise<TemplateDto[]> {
    const res = await apiClient<{ data: TemplateDto[] }>('/api/templates', {
      method: 'GET',
      ...options,
    });
    return res.data;
  },

  /**
   * Create new template
   */
  async createTemplate(formData: FormData): Promise<{ id: string; slug: string }> {
    return apiClient<{ id: string; slug: string }>('/api/templates', {
      method: 'POST',
      body: formData,
    });
  },

  /**
   * Get template HTML content for Monaco editor
   */
  async getTemplateHtml(id: string): Promise<string> {
    return apiClientText(`/api/templates/${id}/html`);
  },

  /**
   * Get full studio bundle (HTML, persisted SamplePayload, DataSchema, Version)
   */
  async getTemplateStudio(id: string): Promise<TemplateStudioDto> {
    return apiClient<TemplateStudioDto>(`/api/templates/${id}/studio`);
  },

  /**
   * Get template schema contract (DataSchema & SamplePayload) for any template format
   */
  async getTemplateSchema(id: string): Promise<TemplateSchemaDto> {
    return apiClient<TemplateSchemaDto>(`/api/templates/${id}/schema`);
  },

  /**
   * Save template HTML and optional samplePayload (auto-increments version +1)
   */
  async saveTemplateHtml(id: string, request: SaveTemplateHtmlRequest): Promise<{ version: number }> {
    return apiClient<{ version: number }>(`/api/templates/${id}/html`, {
      method: 'PUT',
      body: JSON.stringify(request),
    });
  },

  /**
   * Update template metadata (name, category, isActive)
   */
  async updateTemplate(id: string, request: UpdateTemplateRequest): Promise<{ success: boolean }> {
    return apiClient<{ success: boolean }>(`/api/templates/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    });
  },

  /**
   * Deactivate template (soft delete)
   */
  async deactivateTemplate(id: string): Promise<{ success: boolean }> {
    return apiClient<{ success: boolean }>(`/api/templates/${id}`, {
      method: 'DELETE',
    });
  },

  /**
   * Get field mappings for a template
   */
  async getTemplateMappings(id: string): Promise<FieldMappingDto[]> {
    const res = await apiClient<{ mappings: FieldMappingDto[] }>(`/api/templates/${id}/mappings`, {
      method: 'GET',
    });
    return res.mappings;
  },

  /**
   * Save field mappings for a template
   */
  async saveTemplateMappings(id: string, mappings: SaveFieldMappingItem[]): Promise<{ success: boolean }> {
    return apiClient<{ success: boolean }>(`/api/templates/${id}/mappings`, {
      method: 'PUT',
      body: JSON.stringify(mappings),
    });
  },

  /**
   * Validate HTML template markup and detect fields
   */
  async validateTemplate(id: string, html: string): Promise<TemplateValidationResult> {
    return apiClient<TemplateValidationResult>(`/api/templates/${id}/validate`, {
      method: 'POST',
      body: JSON.stringify({ html }),
    });
  },

  /**
   * List template revision history
   */
  async listTemplateVersions(id: string): Promise<TemplateVersionDto[]> {
    const res = await apiClient<{ versions: TemplateVersionDto[] }>(`/api/templates/${id}/versions`, {
      method: 'GET',
    });
    return res.versions;
  },

  /**
   * Download original template file
   */
  async downloadTemplate(id: string): Promise<{ blob: Blob; fileName?: string }> {
    return apiClientBlob(`/api/templates/${id}/download`, {
      method: 'GET',
    });
  },

  async rollbackTemplateVersion(id: string, version: number): Promise<{ version: number }> {
    return apiClient<{ version: number }>(`/api/templates/${id}/rollback/${version}`, { method: 'POST' });
  },

  async scanTemplateFields(id: string): Promise<string[]> {
    const res = await apiClient<{ placeholders: string[] }>(`/api/templates/${id}/scan-fields`, {
      method: 'GET',
    });
    return res.placeholders;
  },

  async scanFieldsFromFile(file: File): Promise<string[]> {
    const form = new FormData();
    form.append('file', file);
    const res = await apiClient<{ placeholders: string[] }>('/api/templates/scan-fields', {
      method: 'POST',
      body: form,
    });
    return res.placeholders;
  },

  async previewMappings(id: string, sampleData: Record<string, unknown>): Promise<Blob> {
    const { blob } = await apiClientBlob(`/api/templates/${id}/mappings/preview`, {
      method: 'POST',
      body: JSON.stringify({ sampleData }),
    });
    return blob;
  },

  // ── Draft Upload Pipeline ─────────────────────────────────────────────────

  async parseDraft(file: File): Promise<{ draftId: string; placeholders: string[] }> {
    const form = new FormData();
    form.append('file', file);
    return apiClient<{ draftId: string; placeholders: string[] }>('/api/templates/draft/parse', {
      method: 'POST',
      body: form,
    });
  },

  async previewDraft(draftId: string, dataJson: string): Promise<Blob> {
    try {
      const { blob } = await apiClientBlob(`/api/templates/draft/${draftId}/preview`, {
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
    return apiClient<{ templateId: string }>(`/api/templates/draft/${draftId}/commit`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },
};

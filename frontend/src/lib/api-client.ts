import { 
  Project, 
  ApiKey, 
  TemplatesResponse, 
  TemplateSchemaResponse,
  DocumentGenerationRequest, 
  DocumentGenerationResponse, 
  LogsResponse, 
  MetricSummary 
} from '@/types/api';

const getInitialBaseUrl = (): string => {
  if (process.env.NEXT_PUBLIC_API_URL) {
    return process.env.NEXT_PUBLIC_API_URL;
  }
  if (typeof window !== 'undefined') {
    // If running on port 3001 or 3000, target backend port 8080 on same host
    const frontendPorts = ['3000', '3001', '3002', '3003'];
    if (frontendPorts.includes(window.location.port)) {
      return `${window.location.protocol}//${window.location.hostname}:8080`;
    }
    return window.location.origin;
  }
  return 'http://localhost:8080';
};

export class ApiClient {
  private baseUrl: string;
  private apiKey: string;

  constructor() {
    this.baseUrl = getInitialBaseUrl();
    this.apiKey = '';

    if (typeof window !== 'undefined') {
      try {
        const savedUrl = localStorage.getItem('smk_api_url');
        if (savedUrl) this.baseUrl = savedUrl;
        const savedKey = localStorage.getItem('smk_api_key');
        if (savedKey) this.apiKey = savedKey;
      } catch (e) {
        // In case localStorage is disabled or restricted
      }
    }
  }

  public setApiKey(key: string) {
    this.apiKey = key;
    if (typeof window !== 'undefined') {
      try {
        localStorage.setItem('smk_api_key', key);
      } catch (e) {}
    }
  }

  public getApiKey(): string {
    return this.apiKey;
  }

  public setBaseUrl(url: string) {
    this.baseUrl = url;
    if (typeof window !== 'undefined') {
      try {
        localStorage.setItem('smk_api_url', url);
      } catch (e) {}
    }
  }

  public getBaseUrl(): string {
    return this.baseUrl;
  }

  private async fetchWithAuth<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const url = `${this.baseUrl}${endpoint}`;
    const headers = new Headers(options.headers || {});
    
    if (!headers.has('X-API-Key') && this.apiKey) {
      headers.set('X-API-Key', this.apiKey);
    }

    if (!headers.has('Content-Type') && !(options.body instanceof FormData)) {
      headers.set('Content-Type', 'application/json');
    }

    const res = await fetch(url, {
      ...options,
      headers,
    });

    if (!res.ok) {
      let errorDetail = `HTTP ${res.status} ${res.statusText}`;
      try {
        const text = await res.text();
        if (text) {
          try {
            const errorJson = JSON.parse(text);
            errorDetail = errorJson.message || errorJson.error || errorJson.title || errorDetail;
          } catch {
            errorDetail = text;
          }
        }
      } catch {}
      throw new Error(errorDetail);
    }

    return res.json() as Promise<T>;
  }

  // --- Templates ---
  public async getTemplates(projectId?: string): Promise<TemplatesResponse> {
    const query = projectId ? `?projectId=${projectId}` : '';
    return this.fetchWithAuth<TemplatesResponse>(`/api/Template${query}`);
  }

  public async getTemplateSchema(fileName: string): Promise<TemplateSchemaResponse> {
    return this.fetchWithAuth<TemplateSchemaResponse>(`/api/Template/${encodeURIComponent(fileName)}/schema`);
  }

  public getTemplateDownloadUrl(fileName: string): string {
    return `${this.baseUrl}/api/Template/${encodeURIComponent(fileName)}/download`;
  }

  public async uploadTemplate(file: File, isGlobal: boolean = false): Promise<any> {
    const formData = new FormData();
    formData.append('file', file);
    return this.fetchWithAuth(`/api/Template/upload?isGlobal=${isGlobal}`, {
      method: 'POST',
      body: formData,
    });
  }

  public async getTemplateVersions(fileName: string): Promise<{ fileName: string; versions: import('@/types/api').TemplateVersion[]; count: number }> {
    return this.fetchWithAuth(`/api/Template/${encodeURIComponent(fileName)}/versions`);
  }

  public async rollbackTemplateVersion(fileName: string, version: number): Promise<{ message: string; newVersion: number; fileName: string }> {
    return this.fetchWithAuth(`/api/Template/${encodeURIComponent(fileName)}/rollback/${version}`, {
      method: 'POST',
    });
  }

  public async deleteTemplate(fileName: string): Promise<{ message: string }> {
    return this.fetchWithAuth<{ message: string }>(`/api/Template/${encodeURIComponent(fileName)}`, {
      method: 'DELETE',
    });
  }

  // --- Field Mapping ---
  public async getTemplateMappings(fileName: string): Promise<import('@/types/api').FieldMappingsResponse> {
    return this.fetchWithAuth(`/api/Template/${encodeURIComponent(fileName)}/mappings`);
  }

  public async saveTemplateMappings(fileName: string, mappings: import('@/types/api').FieldMappingItem[]): Promise<any> {
    return this.fetchWithAuth(`/api/Template/${encodeURIComponent(fileName)}/mappings`, {
      method: 'PUT',
      body: JSON.stringify(mappings),
    });
  }

  public async previewTemplateMappings(fileName: string, mappings: import('@/types/api').FieldMappingItem[], sampleData: any): Promise<import('@/types/api').FieldMappingPreviewResponse> {
    return this.fetchWithAuth(`/api/Template/${encodeURIComponent(fileName)}/mappings/preview`, {
      method: 'POST',
      body: JSON.stringify({ mappings, sampleData }),
    });
  }

  // --- Document Generation ---

  /**
   * Downloads a previously generated document via the backend (avoids direct MinIO access from browser).
   * Triggers a browser save-file dialog.
   */
  public async downloadDocument(logId: string, suggestedFileName?: string): Promise<void> {
    const url = `${this.baseUrl}/api/Document/download/${logId}`;
    const headers: Record<string, string> = {};
    if (this.apiKey) headers['X-API-Key'] = this.apiKey;

    const res = await fetch(url, { headers });
    if (!res.ok) throw new Error(`Download failed: HTTP ${res.status}`);

    const blob = await res.blob();
    const objectUrl = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = objectUrl;
    a.download = suggestedFileName ?? 'document';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(objectUrl);
  }

  public async generateDocument(req: DocumentGenerationRequest): Promise<DocumentGenerationResponse> {
    return this.fetchWithAuth<DocumentGenerationResponse>('/api/Document/generate', {
      method: 'POST',
      body: JSON.stringify(req),
    });
  }

  public async saveTiptapTemplate(req: import('@/types/api').TiptapSaveRequest): Promise<import('@/types/api').TiptapSaveResponse> {
    return this.fetchWithAuth('/api/Template/tiptap', {
      method: 'POST',
      body: JSON.stringify(req),
    });
  }

  public async saveReportBroTemplate(req: import('@/types/api').ReportBroSaveRequest): Promise<import('@/types/api').ReportBroSaveResponse> {
    return this.fetchWithAuth('/api/Template/reportbro', {
      method: 'POST',
      body: JSON.stringify(req),
    });
  }

  // Converts raw HTML to PDF via Gotenberg Chromium — no template, no storage, no log (Rule 12)
  public async convertHtmlToPdf(
    htmlContent: string,
    headerHtml?: string,
    footerHtml?: string,
  ): Promise<{ blob: Blob; durationMs: number | null }> {
    const url = `${this.baseUrl}/api/Document/html-to-pdf`;
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (this.apiKey) headers['X-API-Key'] = this.apiKey;

    const res = await fetch(url, {
      method: 'POST',
      headers,
      body: JSON.stringify({ htmlContent, headerHtml, footerHtml }),
    });

    if (!res.ok) {
      let msg = `HTTP ${res.status}`;
      try { msg = (await res.json()).message ?? msg; } catch { /* ignore */ }
      throw new Error(msg);
    }

    const durationMs = res.headers.get('X-Duration-Ms')
      ? parseInt(res.headers.get('X-Duration-Ms')!, 10)
      : null;

    return { blob: await res.blob(), durationMs };
  }

  // Returns PDF Blob — caller creates objectURL and revokes on cleanup (Rule 12: no MinIO save)
  public async previewDocument(req: DocumentGenerationRequest): Promise<Blob> {
    const url = `${this.baseUrl}/api/Document/preview`;
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (this.apiKey) headers['X-API-Key'] = this.apiKey;

    const res = await fetch(url, { method: 'POST', headers, body: JSON.stringify(req) });
    if (!res.ok) {
      let msg = `HTTP ${res.status}`;
      try { msg = (await res.json()).message ?? msg; } catch { /* ignore */ }
      throw new Error(msg);
    }
    return res.blob();
  }

  // --- Projects & Multi-Tenant Keys ---
  public async getProjects(): Promise<Project[]> {
    return this.fetchWithAuth<Project[]>('/api/Project');
  }

  public async createProject(data: { code: string; name: string; description?: string }): Promise<Project> {
    return this.fetchWithAuth<Project>('/api/Project', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  public async createApiKey(projectId: string, data: { name: string; expiresAt?: string }): Promise<ApiKey> {
    return this.fetchWithAuth<ApiKey>(`/api/Project/${projectId}/keys`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  public async revokeApiKey(keyId: string): Promise<{ message: string }> {
    return this.fetchWithAuth<{ message: string }>(`/api/Project/keys/${keyId}`, {
      method: 'DELETE',
    });
  }

  // --- Audit & Metrics ---
  public async getLogs(params: { page?: number; pageSize?: number; projectId?: string; status?: string } = {}): Promise<LogsResponse> {
    const searchParams = new URLSearchParams();
    if (params.page) searchParams.set('page', params.page.toString());
    if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());
    if (params.projectId) searchParams.set('projectId', params.projectId);
    if (params.status) searchParams.set('status', params.status);

    const qs = searchParams.toString() ? `?${searchParams.toString()}` : '';
    return this.fetchWithAuth<LogsResponse>(`/api/Audit/logs${qs}`);
  }

  public async getMetrics(): Promise<MetricSummary> {
    return this.fetchWithAuth<MetricSummary>('/api/Audit/metrics');
  }
}

export const api = new ApiClient();

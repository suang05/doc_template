import { apiClient, apiRequestWithMeta } from '@/lib/api/client';
import type {
  ApiEnvelope,
  BreakGlassGrantDto,
  CreateProjectRequest,
  GrantBreakGlassAccessRequest,
  PagedEnvelope,
  PlatformFontDto,
  ProjectControlPlaneDto,
  ProjectDto,
  ProjectQuotaDto,
  ResourceWithETag,
  UpdateProjectEgressCidrsRequest,
  UpsertProjectQuotaRequest,
} from '@/lib/api/types';

export interface ListProjectsParams {
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface RegisterPlatformFontPayload {
  file: File;
  familyName?: string;
  weight?: string;
  style?: string;
}

function buildProjectsQuery(params?: ListProjectsParams): string {
  const qs = new URLSearchParams();
  if (params?.search?.trim()) qs.set('search', params.search.trim());
  if (params?.page) qs.set('page', String(params.page));
  if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
  const encoded = qs.toString();
  return encoded ? `?${encoded}` : '';
}

export const projectsApi = {
  /**
   * Lists workspaces accessible to the signed-in portal user.
   */
  async listAccessible(params?: ListProjectsParams): Promise<PagedEnvelope<ProjectDto>> {
    return apiClient<PagedEnvelope<ProjectDto>>(
      `/api/v1/portal/projects${buildProjectsQuery(params)}`,
      { method: 'GET' },
    );
  },

  /**
   * Lists all tenant projects in SuperAdmin Control Plane scope.
   */
  async listTenants(params?: ListProjectsParams): Promise<PagedEnvelope<ProjectDto>> {
    return apiClient<PagedEnvelope<ProjectDto>>(
      `/api/v1/management/tenants${buildProjectsQuery(params)}`,
      { method: 'GET' },
    );
  },

  async getProjectWithETag(projectId: string): Promise<ResourceWithETag<ProjectDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<ProjectDto>>(
      `/api/v1/management/projects/${projectId}`,
      { method: 'GET' },
    );
    return { ...res, data: res.data.data };
  },

  async createProject(
    request: CreateProjectRequest,
    idempotencyKey?: string,
  ): Promise<ResourceWithETag<ProjectDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<ProjectDto>>(
      '/api/v1/management/projects',
      {
        method: 'POST',
        body: JSON.stringify(request),
        idempotencyKey: idempotencyKey || crypto.randomUUID(),
      },
    );
    return { ...res, data: res.data.data };
  },

  async activateProject(
    projectId: string,
    ifMatch?: string | null,
  ): Promise<ResourceWithETag<ProjectControlPlaneDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<ProjectControlPlaneDto>>(
      `/api/v1/management/projects/${projectId}/activate`,
      {
        method: 'POST',
        ifMatch,
      },
    );
    return { ...res, data: res.data.data };
  },

  async deactivateProject(
    projectId: string,
    ifMatch?: string | null,
  ): Promise<ResourceWithETag<ProjectControlPlaneDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<ProjectControlPlaneDto>>(
      `/api/v1/management/projects/${projectId}/deactivate`,
      {
        method: 'POST',
        ifMatch,
      },
    );
    return { ...res, data: res.data.data };
  },

  async updateEgressCidrs(
    projectId: string,
    request: UpdateProjectEgressCidrsRequest,
    ifMatch?: string | null,
  ): Promise<ResourceWithETag<ProjectControlPlaneDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<ProjectControlPlaneDto>>(
      `/api/v1/management/projects/${projectId}/egress-cidrs`,
      {
        method: 'PUT',
        body: JSON.stringify(request),
        ifMatch,
      },
    );
    return { ...res, data: res.data.data };
  },

  async transferProjectOwner(
    projectId: string,
    newOwnerUserId: string,
    ifMatch?: string | null,
  ): Promise<void> {
    await apiClient<void>(`/api/v1/management/projects/${projectId}/transfer-owner`, {
      method: 'POST',
      body: JSON.stringify({ newOwnerUserId }),
      ifMatch,
    });
  },

  async getQuotaWithETag(projectId: string): Promise<ResourceWithETag<ProjectQuotaDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<ProjectQuotaDto>>(
      `/api/v1/management/projects/${projectId}/quota`,
      { method: 'GET' },
    );
    return { ...res, data: res.data.data };
  },

  async upsertQuota(
    projectId: string,
    request: UpsertProjectQuotaRequest,
    ifMatch?: string | null,
  ): Promise<ResourceWithETag<ProjectQuotaDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<ProjectQuotaDto>>(
      `/api/v1/management/projects/${projectId}/quota`,
      {
        method: 'PUT',
        body: JSON.stringify(request),
        ifMatch,
      },
    );
    return { ...res, data: res.data.data };
  },

  async listBreakGlassGrants(params?: {
    superAdminUserId?: string;
    targetProjectId?: string;
  }): Promise<BreakGlassGrantDto[]> {
    const qs = new URLSearchParams();
    if (params?.superAdminUserId) qs.set('superAdminUserId', params.superAdminUserId);
    if (params?.targetProjectId) qs.set('targetProjectId', params.targetProjectId);
    const query = qs.toString();
    const res = await apiClient<ApiEnvelope<BreakGlassGrantDto[]>>(
      `/api/v1/management/break-glass-grants${query ? `?${query}` : ''}`,
      { method: 'GET' },
    );
    return res.data;
  },

  async getBreakGlassGrantWithETag(
    grantId: string,
  ): Promise<ResourceWithETag<BreakGlassGrantDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<BreakGlassGrantDto>>(
      `/api/v1/management/break-glass-grants/${grantId}`,
      { method: 'GET' },
    );
    return { ...res, data: res.data.data };
  },

  async grantBreakGlassAccess(
    request: GrantBreakGlassAccessRequest,
    idempotencyKey?: string,
  ): Promise<ResourceWithETag<BreakGlassGrantDto>> {
    const res = await apiRequestWithMeta<ApiEnvelope<BreakGlassGrantDto>>(
      '/api/v1/management/break-glass-grants',
      {
        method: 'POST',
        body: JSON.stringify(request),
        idempotencyKey: idempotencyKey || crypto.randomUUID(),
      },
    );
    return { ...res, data: res.data.data };
  },

  async revokeBreakGlassAccess(grantId: string, ifMatch?: string | null): Promise<void> {
    await apiClient<void>(`/api/v1/management/break-glass-grants/${grantId}`, {
      method: 'DELETE',
      ifMatch,
    });
  },

  async listPlatformFonts(projectId?: string): Promise<PlatformFontDto[]> {
    const qs = projectId ? `?projectId=${encodeURIComponent(projectId)}` : '';
    const res = await apiClient<ApiEnvelope<PlatformFontDto[]>>(
      `/api/v1/management/fonts${qs}`,
      { method: 'GET' },
    );
    return res.data;
  },

  async registerPlatformFont(
    payload: RegisterPlatformFontPayload,
    idempotencyKey?: string,
  ): Promise<PlatformFontDto> {
    const formData = new FormData();
    formData.append('file', payload.file);
    if (payload.familyName?.trim()) {
      formData.append('familyName', payload.familyName.trim());
    }
    formData.append('weight', payload.weight || 'normal');
    formData.append('style', payload.style || 'normal');

    const res = await apiClient<ApiEnvelope<PlatformFontDto>>('/api/v1/management/fonts', {
      method: 'POST',
      body: formData,
      idempotencyKey: idempotencyKey || crypto.randomUUID(),
    });
    return res.data;
  },
};

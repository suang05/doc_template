'use client';

import { useState, useCallback, useEffect } from 'react';
import { extractErrorMessage } from '@/lib/api/client';
import type {
  BreakGlassGrantDto,
  CreateProjectRequest,
  GrantBreakGlassAccessRequest,
  PlatformFontDto,
  ProjectControlPlaneDto,
  ProjectDto,
  ProjectQuotaDto,
  UpsertProjectQuotaRequest,
} from '@/lib/api/types';
import { projectsApi, type RegisterPlatformFontPayload } from '../api/projects.api';

export interface TenantsPaginationState {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export function useProjectsControlPlane() {
  const [tenants, setTenants] = useState<ProjectDto[]>([]);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pagination, setPagination] = useState<TenantsPaginationState>({
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 1,
  });
  const [loadingTenants, setLoadingTenants] = useState(true);
  const [tenantsError, setTenantsError] = useState<string | null>(null);
  const [egressCidrsByProjectId, setEgressCidrsByProjectId] = useState<Record<string, string[]>>({});
  const [mutatingProjectId, setMutatingProjectId] = useState<string | null>(null);

  const [quotaProjectId, setQuotaProjectId] = useState<string>('');
  const [quota, setQuota] = useState<ProjectQuotaDto | null>(null);
  const [quotaEtag, setQuotaEtag] = useState<string | null>(null);
  const [loadingQuota, setLoadingQuota] = useState(false);
  const [savingQuota, setSavingQuota] = useState(false);

  const [grants, setGrants] = useState<BreakGlassGrantDto[]>([]);
  const [grantFilterProjectId, setGrantFilterProjectId] = useState<string>('');
  const [loadingGrants, setLoadingGrants] = useState(false);
  const [revokingGrantId, setRevokingGrantId] = useState<string | null>(null);

  const [fonts, setFonts] = useState<PlatformFontDto[]>([]);
  const [loadingFonts, setLoadingFonts] = useState(false);
  const [uploadingFont, setUploadingFont] = useState(false);

  const fetchTenants = useCallback(
    async (targetPage = page, targetSearch = search) => {
      setLoadingTenants(true);
      setTenantsError(null);
      try {
        const res = await projectsApi.listTenants({
          page: targetPage,
          pageSize: 20,
          search: targetSearch || undefined,
        });
        const items = res.data ?? [];
        setTenants(items);
        setPagination({
          page: res.pagination.page,
          pageSize: res.pagination.limit,
          totalCount: res.pagination.totalCount,
          totalPages: res.pagination.totalPages,
        });
        setQuotaProjectId((prev) => prev || items[0]?.id || '');
      } catch (err) {
        setTenantsError(extractErrorMessage(err, 'ไม่สามารถโหลดรายชื่อเทแนนต์และโปรเจกต์ได้'));
      } finally {
        setLoadingTenants(false);
      }
    },
    [page, search],
  );

  useEffect(() => {
    void fetchTenants(page, search);
  }, [fetchTenants, page, search]);

  const recordControlPlaneSnapshot = useCallback((snapshot: ProjectControlPlaneDto) => {
    setEgressCidrsByProjectId((prev) => ({
      ...prev,
      [snapshot.id]: snapshot.allowedEgressCidrs ?? [],
    }));
    setTenants((prev) =>
      prev.map((item) =>
        item.id === snapshot.id ? { ...item, isActive: snapshot.isActive } : item,
      ),
    );
  }, []);

  const createProject = useCallback(
    async (request: CreateProjectRequest, idempotencyKey: string): Promise<ProjectDto> => {
      const res = await projectsApi.createProject(request, idempotencyKey);
      await fetchTenants(1, search);
      setPage(1);
      return res.data;
    },
    [fetchTenants, search],
  );

  const fetchProjectForEdit = useCallback(async (projectId: string) => {
    return projectsApi.getProjectWithETag(projectId);
  }, []);

  const toggleProjectActive = useCallback(
    async (project: ProjectDto): Promise<ProjectControlPlaneDto> => {
      setMutatingProjectId(project.id);
      try {
        const current = await projectsApi.getProjectWithETag(project.id);
        const res = current.data.isActive
          ? await projectsApi.deactivateProject(project.id, current.etag)
          : await projectsApi.activateProject(project.id, current.etag);
        recordControlPlaneSnapshot(res.data);
        return res.data;
      } finally {
        setMutatingProjectId(null);
      }
    },
    [recordControlPlaneSnapshot],
  );

  const updateEgressCidrs = useCallback(
    async (
      projectId: string,
      allowedCidrs: string[],
      etag: string | null,
    ): Promise<ProjectControlPlaneDto> => {
      setMutatingProjectId(projectId);
      try {
        const res = await projectsApi.updateEgressCidrs(projectId, { allowedCidrs }, etag);
        recordControlPlaneSnapshot(res.data);
        return res.data;
      } finally {
        setMutatingProjectId(null);
      }
    },
    [recordControlPlaneSnapshot],
  );

  const transferOwner = useCallback(
    async (projectId: string, newOwnerUserId: string, etag: string | null): Promise<void> => {
      setMutatingProjectId(projectId);
      try {
        await projectsApi.transferProjectOwner(projectId, newOwnerUserId, etag);
        await fetchTenants(page, search);
      } finally {
        setMutatingProjectId(null);
      }
    },
    [fetchTenants, page, search],
  );

  const loadQuota = useCallback(async (projectId: string) => {
    if (!projectId) {
      setQuota(null);
      setQuotaEtag(null);
      return;
    }
    setLoadingQuota(true);
    try {
      const res = await projectsApi.getQuotaWithETag(projectId);
      setQuota(res.data);
      setQuotaEtag(res.etag);
    } finally {
      setLoadingQuota(false);
    }
  }, []);

  const saveQuota = useCallback(
    async (
      projectId: string,
      request: UpsertProjectQuotaRequest,
      ifMatch?: string | null,
    ): Promise<ProjectQuotaDto> => {
      setSavingQuota(true);
      try {
        const effectiveIfMatch = ifMatch !== undefined ? ifMatch : quotaEtag;
        const res = await projectsApi.upsertQuota(projectId, request, effectiveIfMatch);
        setQuota(res.data);
        setQuotaEtag(res.etag);
        return res.data;
      } finally {
        setSavingQuota(false);
      }
    },
    [quotaEtag],
  );

  const loadGrants = useCallback(
    async (targetProjectId = grantFilterProjectId) => {
      setLoadingGrants(true);
      try {
        const list = await projectsApi.listBreakGlassGrants(
          targetProjectId ? { targetProjectId } : undefined,
        );
        setGrants(list);
      } finally {
        setLoadingGrants(false);
      }
    },
    [grantFilterProjectId],
  );

  const issueGrant = useCallback(
    async (
      request: GrantBreakGlassAccessRequest,
      idempotencyKey: string,
    ): Promise<BreakGlassGrantDto> => {
      const res = await projectsApi.grantBreakGlassAccess(request, idempotencyKey);
      await loadGrants(grantFilterProjectId);
      return res.data;
    },
    [grantFilterProjectId, loadGrants],
  );

  const revokeGrant = useCallback(
    async (grantId: string): Promise<void> => {
      setRevokingGrantId(grantId);
      try {
        const current = await projectsApi.getBreakGlassGrantWithETag(grantId);
        await projectsApi.revokeBreakGlassAccess(grantId, current.etag);
        await loadGrants(grantFilterProjectId);
      } finally {
        setRevokingGrantId(null);
      }
    },
    [grantFilterProjectId, loadGrants],
  );

  const loadFonts = useCallback(async (projectId?: string) => {
    setLoadingFonts(true);
    try {
      const list = await projectsApi.listPlatformFonts(projectId);
      setFonts(list);
    } finally {
      setLoadingFonts(false);
    }
  }, []);

  const uploadFont = useCallback(
    async (
      payload: RegisterPlatformFontPayload,
      idempotencyKey: string,
    ): Promise<PlatformFontDto> => {
      setUploadingFont(true);
      try {
        const created = await projectsApi.registerPlatformFont(payload, idempotencyKey);
        await loadFonts();
        return created;
      } finally {
        setUploadingFont(false);
      }
    },
    [loadFonts],
  );

  return {
    tenants,
    search,
    setSearch,
    page,
    setPage,
    pagination,
    loadingTenants,
    tenantsError,
    egressCidrsByProjectId,
    mutatingProjectId,
    fetchTenants,
    createProject,
    fetchProjectForEdit,
    toggleProjectActive,
    updateEgressCidrs,
    transferOwner,
    quotaProjectId,
    setQuotaProjectId,
    quota,
    quotaEtag,
    loadingQuota,
    savingQuota,
    loadQuota,
    saveQuota,
    grants,
    grantFilterProjectId,
    setGrantFilterProjectId,
    loadingGrants,
    revokingGrantId,
    loadGrants,
    issueGrant,
    revokeGrant,
    fonts,
    loadingFonts,
    uploadingFont,
    loadFonts,
    uploadFont,
  };
}

'use client';

import { useState, useCallback, useMemo } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { extractErrorMessage } from '@/lib/api/client';
import {
  normalizePagination,
  type BreakGlassGrantDto,
  type CreateProjectRequest,
  type GrantBreakGlassAccessRequest,
  type PagedEnvelope,
  type PlatformFontDto,
  type ProjectControlPlaneDto,
  type ProjectDto,
  type ProjectQuotaDto,
  type ResourceWithETag,
  type UpsertProjectQuotaRequest,
} from '@/lib/api/types';
import { projectsApi, type RegisterPlatformFontPayload } from '../api/projects.api';

export interface TenantsPaginationState {
  page: number;
  limit: number;
  totalCount: number;
  totalPages: number;
}

export const controlPlaneKeys = {
  all: ['control-plane'] as const,
  tenants: (page: number, search: string) => ['control-plane', 'tenants', { page, search }] as const,
  quota: (projectId: string) => ['control-plane', 'quota', projectId] as const,
  grants: (projectId: string) => ['control-plane', 'grants', projectId] as const,
  fonts: (projectId?: string) => ['control-plane', 'fonts', projectId ?? 'all'] as const,
};

export function useProjectsControlPlane() {
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [egressCidrsByProjectId, setEgressCidrsByProjectId] = useState<Record<string, string[]>>({});
  const [mutatingProjectId, setMutatingProjectId] = useState<string | null>(null);

  const [selectedQuotaProjectId, setSelectedQuotaProjectId] = useState<string>('');
  const [grantFilterProjectId, setGrantFilterProjectId] = useState<string>('');
  const [revokingGrantId, setRevokingGrantId] = useState<string | null>(null);

  const normalizedSearch = search.trim();

  const tenantsQuery = useQuery<PagedEnvelope<ProjectDto>, Error>({
    queryKey: controlPlaneKeys.tenants(page, normalizedSearch),
    queryFn: () =>
      projectsApi.listTenants({
        page,
        limit: 20,
        search: normalizedSearch || undefined,
        includeTotal: true,
      }),
    placeholderData: keepPreviousData,
  });

  const tenants = useMemo(() => tenantsQuery.data?.data ?? [], [tenantsQuery.data]);
  const pagination = useMemo<TenantsPaginationState>(() => {
    const normalized = normalizePagination(tenantsQuery.data?.pagination, 20, page);
    return {
      page: normalized.page,
      limit: normalized.limit,
      totalCount: normalized.totalCount,
      totalPages: normalized.totalPages,
    };
  }, [tenantsQuery.data, page]);

  const tenantsError = tenantsQuery.error
    ? extractErrorMessage(tenantsQuery.error, 'ไม่สามารถโหลดรายชื่อเทแนนต์และโปรเจกต์ได้')
    : null;

  const quotaProjectId = selectedQuotaProjectId || tenants[0]?.id || '';

  const quotaQuery = useQuery<ResourceWithETag<ProjectQuotaDto>, Error>({
    queryKey: controlPlaneKeys.quota(quotaProjectId),
    enabled: Boolean(quotaProjectId),
    queryFn: () => projectsApi.getQuotaWithETag(quotaProjectId),
  });

  const grantsQuery = useQuery<BreakGlassGrantDto[], Error>({
    queryKey: controlPlaneKeys.grants(grantFilterProjectId),
    queryFn: () =>
      projectsApi.listBreakGlassGrants(
        grantFilterProjectId ? { targetProjectId: grantFilterProjectId } : undefined,
      ),
  });

  const fontsQuery = useQuery<PlatformFontDto[], Error>({
    queryKey: controlPlaneKeys.fonts(),
    queryFn: () => projectsApi.listPlatformFonts(),
  });

  const recordControlPlaneSnapshot = useCallback(
    (snapshot: ProjectControlPlaneDto) => {
      setEgressCidrsByProjectId((prev) => ({
        ...prev,
        [snapshot.id]: snapshot.allowedEgressCidrs ?? [],
      }));
      void queryClient.invalidateQueries({ queryKey: ['control-plane', 'tenants'] });
    },
    [queryClient],
  );

  const fetchTenants = useCallback(
    async (_targetPage?: number, _targetSearch?: string) => {
      await tenantsQuery.refetch();
    },
    [tenantsQuery],
  );

  const createProjectMutation = useMutation({
    mutationFn: ({
      request,
      idempotencyKey,
    }: {
      request: CreateProjectRequest;
      idempotencyKey: string;
    }) => projectsApi.createProject(request, idempotencyKey),
    onSuccess: async () => {
      setPage(1);
      await queryClient.invalidateQueries({ queryKey: ['control-plane', 'tenants'] });
    },
  });

  const createProject = useCallback(
    async (request: CreateProjectRequest, idempotencyKey: string): Promise<ProjectDto> => {
      const res = await createProjectMutation.mutateAsync({ request, idempotencyKey });
      return res.data;
    },
    [createProjectMutation],
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
        await queryClient.invalidateQueries({ queryKey: ['control-plane', 'tenants'] });
      } finally {
        setMutatingProjectId(null);
      }
    },
    [queryClient],
  );

  const loadQuota = useCallback(
    async (projectId: string) => {
      if (!projectId) return;
      await queryClient.invalidateQueries({ queryKey: controlPlaneKeys.quota(projectId) });
    },
    [queryClient],
  );

  const saveQuotaMutation = useMutation({
    mutationFn: ({
      projectId,
      request,
      ifMatch,
    }: {
      projectId: string;
      request: UpsertProjectQuotaRequest;
      ifMatch?: string | null;
    }) => projectsApi.upsertQuota(projectId, request, ifMatch),
    onSuccess: (res, variables) => {
      queryClient.setQueryData(controlPlaneKeys.quota(variables.projectId), res);
    },
  });

  const quota = quotaQuery.data?.data ?? null;
  const quotaEtag = quotaQuery.data?.etag ?? null;

  const saveQuota = useCallback(
    async (
      projectId: string,
      request: UpsertProjectQuotaRequest,
      ifMatch?: string | null,
    ): Promise<ProjectQuotaDto> => {
      const effectiveIfMatch = ifMatch !== undefined ? ifMatch : quotaEtag;
      const res = await saveQuotaMutation.mutateAsync({
        projectId,
        request,
        ifMatch: effectiveIfMatch,
      });
      return res.data;
    },
    [quotaEtag, saveQuotaMutation],
  );

  const loadGrants = useCallback(
    async (targetProjectId = grantFilterProjectId) => {
      await queryClient.invalidateQueries({
        queryKey: controlPlaneKeys.grants(targetProjectId),
      });
    },
    [grantFilterProjectId, queryClient],
  );

  const issueGrantMutation = useMutation({
    mutationFn: ({
      request,
      idempotencyKey,
    }: {
      request: GrantBreakGlassAccessRequest;
      idempotencyKey: string;
    }) => projectsApi.grantBreakGlassAccess(request, idempotencyKey),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['control-plane', 'grants'] });
    },
  });

  const issueGrant = useCallback(
    async (
      request: GrantBreakGlassAccessRequest,
      idempotencyKey: string,
    ): Promise<BreakGlassGrantDto> => {
      const res = await issueGrantMutation.mutateAsync({ request, idempotencyKey });
      return res.data;
    },
    [issueGrantMutation],
  );

  const revokeGrant = useCallback(
    async (grantId: string): Promise<void> => {
      setRevokingGrantId(grantId);
      try {
        const current = await projectsApi.getBreakGlassGrantWithETag(grantId);
        await projectsApi.revokeBreakGlassAccess(grantId, current.etag);
        await queryClient.invalidateQueries({ queryKey: ['control-plane', 'grants'] });
      } finally {
        setRevokingGrantId(null);
      }
    },
    [queryClient],
  );

  const loadFonts = useCallback(
    async (projectId?: string) => {
      if (projectId) {
        await queryClient.fetchQuery({
          queryKey: controlPlaneKeys.fonts(projectId),
          queryFn: () => projectsApi.listPlatformFonts(projectId),
        });
        return;
      }
      await fontsQuery.refetch();
    },
    [fontsQuery, queryClient],
  );

  const uploadFontMutation = useMutation({
    mutationFn: ({
      payload,
      idempotencyKey,
    }: {
      payload: RegisterPlatformFontPayload;
      idempotencyKey: string;
    }) => projectsApi.registerPlatformFont(payload, idempotencyKey),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['control-plane', 'fonts'] });
    },
  });

  const uploadFont = useCallback(
    async (
      payload: RegisterPlatformFontPayload,
      idempotencyKey: string,
    ): Promise<PlatformFontDto> => {
      return uploadFontMutation.mutateAsync({ payload, idempotencyKey });
    },
    [uploadFontMutation],
  );

  return {
    tenants,
    search,
    setSearch,
    page,
    setPage,
    pagination,
    loadingTenants: tenantsQuery.isLoading || tenantsQuery.isFetching,
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
    setQuotaProjectId: setSelectedQuotaProjectId,
    quota,
    quotaEtag,
    loadingQuota: quotaQuery.isLoading || quotaQuery.isFetching,
    quotaError: quotaQuery.error
      ? extractErrorMessage(quotaQuery.error, 'ไม่สามารถโหลดข้อมูลโควตาของโปรเจกต์ได้')
      : null,
    savingQuota: saveQuotaMutation.isPending,
    loadQuota,
    saveQuota,
    grants: grantsQuery.data ?? [],
    grantFilterProjectId,
    setGrantFilterProjectId,
    loadingGrants: grantsQuery.isLoading || grantsQuery.isFetching,
    grantsError: grantsQuery.error
      ? extractErrorMessage(grantsQuery.error, 'โหลดรายการ Break-Glass Grants ไม่สำเร็จ')
      : null,
    revokingGrantId,
    loadGrants,
    issueGrant,
    revokeGrant,
    fonts: fontsQuery.data ?? [],
    loadingFonts: fontsQuery.isLoading || fontsQuery.isFetching,
    fontsError: fontsQuery.error
      ? extractErrorMessage(fontsQuery.error, 'โหลดรายการฟอนต์แพลตฟอร์มไม่สำเร็จ')
      : null,
    uploadingFont: uploadFontMutation.isPending,
    loadFonts,
    uploadFont,
  };
}

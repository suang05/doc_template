'use client';

import { useState, useCallback, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useTemplates, TEMPLATES_QUERY_KEY } from '@/features/templates/hooks/useTemplates';
import { useAutoSelectFirst } from '@/hooks/useAutoSelectFirst';
import { templatesApi } from '@/features/templates/api/templates.api';
import { extractErrorMessage } from '@/lib/api/client';
import { withTimeoutSignal } from '@/lib/api/withTimeoutSignal';
import type { TemplateVersionDto } from '@/lib/api/types';
import { useConflictRecovery } from '@/hooks/useConflictRecovery';

interface VersionHistoryBundle {
  versions: TemplateVersionDto[];
  etag: string | null;
  html: string;
}

export function useVersionHistory(initialTemplateId?: string) {
  const queryClient = useQueryClient();
  const { templates, loading: templatesLoading } = useTemplates();
  const [selectedTemplateId, setSelectedTemplateIdRaw] = useAutoSelectFirst(
    templates,
    (t) => t.id,
    initialTemplateId,
  );

  const [pickedVersion, setPickedVersion] = useState<{
    templateId: string;
    version: number;
  } | null>(null);
  const [actionError, setActionError] = useState<{
    templateId: string;
    message: string;
  } | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const { conflict, captureConflict, clearConflict } = useConflictRecovery();

  const selectedTemplate = templates.find((t) => t.id === selectedTemplateId) || null;

  const setSelectedTemplateId = useCallback(
    (id: string) => {
      setActionError(null);
      clearConflict();
      setSelectedTemplateIdRaw(id);
    },
    [clearConflict, setSelectedTemplateIdRaw],
  );

  const versionQuery = useQuery<VersionHistoryBundle, Error>({
    queryKey: ['template-versions', selectedTemplateId],
    enabled: Boolean(selectedTemplateId),
    queryFn: ({ signal }) =>
      withTimeoutSignal(async (timedSignal) => {
        const [verList, templateRes, studioData] = await Promise.all([
          templatesApi.listTemplateVersions(selectedTemplateId, { signal: timedSignal }),
          templatesApi.getTemplateWithETag(selectedTemplateId, { signal: timedSignal }).catch(() => null),
          templatesApi.getTemplateStudio(selectedTemplateId, { signal: timedSignal }).catch(() => null),
        ]);

        const sorted = [...verList].sort((a, b) => b.version - a.version);
        return {
          versions: sorted,
          etag: templateRes?.etag ?? null,
          html: studioData?.html ?? '',
        };
      }, signal),
  });

  const versions = useMemo(
    () => (selectedTemplateId ? versionQuery.data?.versions ?? [] : []),
    [selectedTemplateId, versionQuery.data?.versions],
  );
  const currentHtml = selectedTemplateId ? versionQuery.data?.html ?? '' : '';
  const etag = selectedTemplateId ? versionQuery.data?.etag ?? null : null;

  const selectedVersion = useMemo(() => {
    if (
      pickedVersion &&
      pickedVersion.templateId === selectedTemplateId &&
      versions.some((v) => v.version === pickedVersion.version)
    ) {
      return pickedVersion.version;
    }
    return versions.length > 0 ? versions[0].version : null;
  }, [pickedVersion, selectedTemplateId, versions]);

  const setSelectedVersion = useCallback(
    (version: number | null) => {
      if (version === null || !selectedTemplateId) {
        setPickedVersion(null);
        return;
      }
      setPickedVersion({ templateId: selectedTemplateId, version });
    },
    [selectedTemplateId],
  );

  const rollbackMutation = useMutation({
    mutationFn: async ({
      templateId,
      version,
      ifMatch,
    }: {
      templateId: string;
      version: number;
      ifMatch: string | null;
    }) => {
      return templatesApi.rollbackTemplateVersionWithETag(templateId, version, ifMatch);
    },
    onSuccess: async (res, variables) => {
      queryClient.setQueryData<VersionHistoryBundle>(
        ['template-versions', variables.templateId],
        (prev) => (prev ? { ...prev, etag: res.etag ?? prev.etag } : prev),
      );
      setPickedVersion(null);
      setSuccessMessage(`ย้อนเวอร์ชันกลับเป็น v${variables.version} สำเร็จ`);
      setTimeout(() => setSuccessMessage(null), 4000);
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ['template-versions', variables.templateId],
        }),
        queryClient.invalidateQueries({
          queryKey: ['template-studio', variables.templateId],
        }),
        queryClient.invalidateQueries({ queryKey: TEMPLATES_QUERY_KEY }),
      ]);
    },
  });

  const handleRollback = async (version: number) => {
    if (!selectedTemplateId) return;

    setActionError(null);
    clearConflict();
    try {
      await rollbackMutation.mutateAsync({
        templateId: selectedTemplateId,
        version,
        ifMatch: etag,
      });
    } catch (err: unknown) {
      if (!captureConflict(err)) {
        setActionError({
          templateId: selectedTemplateId,
          message: extractErrorMessage(err, 'ย้อนเวอร์ชันไม่สำเร็จ'),
        });
      }
    }
  };

  const queryErrorMessage = versionQuery.error
    ? extractErrorMessage(versionQuery.error, 'ไม่สามารถโหลดประวัติเวอร์ชันได้')
    : null;
  const error =
    (actionError?.templateId === selectedTemplateId ? actionError.message : null) ??
    queryErrorMessage;

  return {
    templates,
    templatesLoading,
    selectedTemplate,
    selectedTemplateId,
    setSelectedTemplateId,
    versions,
    selectedVersion,
    setSelectedVersion,
    currentHtml,
    loading: Boolean(selectedTemplateId) && (versionQuery.isLoading || versionQuery.isRefetching),
    rollingBack: rollbackMutation.isPending,
    error,
    successMessage,
    conflict,
    clearConflict,
    handleRollback,
    refresh: () => {
      if (!selectedTemplateId) return;
      setActionError(null);
      clearConflict();
      void versionQuery.refetch();
    },
  };
}


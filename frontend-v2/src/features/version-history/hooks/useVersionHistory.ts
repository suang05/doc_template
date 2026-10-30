'use client';

import { useState, useEffect, useCallback } from 'react';
import { useTemplates } from '@/features/templates/hooks/useTemplates';
import { useAutoSelectFirst } from '@/hooks/useAutoSelectFirst';
import { templatesApi } from '@/features/templates/api/templates.api';
import { TemplateVersionDto } from '@/types/api';

export function useVersionHistory(initialTemplateId?: string) {
  const { templates, loading: templatesLoading } = useTemplates();
  const [selectedTemplateId, setSelectedTemplateId] = useAutoSelectFirst(
    templates,
    (t) => t.id,
    initialTemplateId
  );

  const [versions, setVersions] = useState<TemplateVersionDto[]>([]);
  const [selectedVersion, setSelectedVersion] = useState<number | null>(null);
  const [currentHtml, setCurrentHtml] = useState<string>('');
  const [loading, setLoading] = useState<boolean>(false);
  const [rollingBack, setRollingBack] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const selectedTemplate = templates.find((t) => t.id === selectedTemplateId) || null;

  const fetchVersions = useCallback(async (templateId: string) => {
    if (!templateId) return;
    setLoading(true);
    setError(null);
    try {
      const [verList, studioData] = await Promise.all([
        templatesApi.listTemplateVersions(templateId),
        templatesApi.getTemplateStudio(templateId).catch(() => null),
      ]);

      const sorted = [...verList].sort((a, b) => b.version - a.version);
      setVersions(sorted);
      if (sorted.length > 0) {
        setSelectedVersion(sorted[0].version);
      } else {
        setSelectedVersion(null);
      }

      if (studioData?.html) {
        setCurrentHtml(studioData.html);
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'ไม่สามารถโหลดประวัติเวอร์ชันได้');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (selectedTemplateId) {
      fetchVersions(selectedTemplateId);
    }
  }, [selectedTemplateId, fetchVersions]);

  const handleRollback = async (version: number) => {
    if (!selectedTemplateId) return;
    if (!confirm(`ยืนยันการย้อนเวอร์ชัน (Rollback) กลับไปเป็น v${version} หรือไม่?`)) return;

    setRollingBack(true);
    setError(null);
    try {
      await templatesApi.rollbackTemplateVersion(selectedTemplateId, version);
      setSuccessMessage(`ย้อนเวอร์ชันกลับเป็น v${version} สำเร็จ`);
      setTimeout(() => setSuccessMessage(null), 4000);
      await fetchVersions(selectedTemplateId);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'ย้อนเวอร์ชันไม่สำเร็จ');
    } finally {
      setRollingBack(false);
    }
  };

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
    loading,
    rollingBack,
    error,
    successMessage,
    handleRollback,
    refresh: () => {
      if (selectedTemplateId) fetchVersions(selectedTemplateId);
    },
  };
}


'use client';

import { useState, useCallback } from 'react';
import { extractErrorMessage } from '@/lib/api/client';
import type { DocumentVersionDto } from '@/lib/api/types';
import { documentsApi } from '@/features/generator/api/documents.api';
import { downloadBlobFile } from '@/utils/download';

export function useLegalAudit() {
  const [documentRef, setDocumentRef] = useState('');
  const [versions, setVersions] = useState<DocumentVersionDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [downloadingVersion, setDownloadingVersion] = useState<number | null>(null);

  const fetchVersions = useCallback(async (ref: string) => {
    if (!ref.trim()) return;
    setLoading(true);
    setError(null);
    try {
      const list = await documentsApi.getDocumentVersions(ref.trim());
      setVersions(list);
      setDocumentRef(ref.trim());
    } catch (err) {
      setError(extractErrorMessage(err, 'ไม่พบประวัติเวอร์ชันของเอกสารนี้'));
      setVersions([]);
    } finally {
      setLoading(false);
    }
  }, []);

  const downloadVersion = useCallback(async (ref: string, version: number) => {
    setDownloadingVersion(version);
    try {
      const { blob, fileName } = await documentsApi.downloadDocumentVersion(ref, version);
      downloadBlobFile(blob, fileName || `${ref}-v${version}.pdf`);
    } catch (err) {
      throw new Error(extractErrorMessage(err, 'ดาวน์โหลดเอกสารล้มเหลว'));
    } finally {
      setDownloadingVersion(null);
    }
  }, []);

  return {
    documentRef,
    setDocumentRef,
    versions,
    loading,
    error,
    fetchVersions,
    downloadVersion,
    downloadingVersion,
  };
}

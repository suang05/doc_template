'use client';

import { useState, useCallback } from 'react';
import { DocumentVersionDto } from '@/types/api';
import { documentsApi } from '@/lib/api/documents.api';

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
    } catch (err: any) {
      setError(err.message || 'ไม่พบประวัติเวอร์ชันของเอกสารนี้');
      setVersions([]);
    } finally {
      setLoading(false);
    }
  }, []);

  const downloadVersion = useCallback(async (ref: string, version: number) => {
    setDownloadingVersion(version);
    try {
      const { blob, fileName } = await documentsApi.downloadDocumentVersion(ref, version);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName || `${ref}-v${version}.pdf`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (err: any) {
      alert(err.message || 'ดาวน์โหลดเอกสารล้มเหลว');
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

'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { templatesApi } from '@/lib/api/templates.api';

export function useFormPreview(
  templateId: string | undefined,
  data: Record<string, unknown>
) {
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const prevUrlRef = useRef<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const runPreview = useCallback(
    async (id: string, payload: Record<string, unknown>) => {
      setLoading(true);
      setError(null);
      try {
        const blob = await templatesApi.previewMappings(id, payload);
        if (prevUrlRef.current) URL.revokeObjectURL(prevUrlRef.current);
        const url = URL.createObjectURL(blob);
        prevUrlRef.current = url;
        setPdfUrl(url);
      } catch (e: any) {
        setError(e.message || 'ไม่สามารถแสดง Preview ได้');
      } finally {
        setLoading(false);
      }
    },
    []
  );

  // Serialize data for stable comparison — effect only fires when content changes
  const dataKey = JSON.stringify(data);

  useEffect(() => {
    if (!templateId) {
      setPdfUrl(null);
      setError(null);
      return;
    }
    if (timerRef.current) clearTimeout(timerRef.current);
    // eslint-disable-next-line react-hooks/exhaustive-deps
    timerRef.current = setTimeout(() => runPreview(templateId, JSON.parse(dataKey)), 800);
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [templateId, dataKey]);

  // Revoke blob URL on unmount
  useEffect(() => {
    return () => {
      if (prevUrlRef.current) URL.revokeObjectURL(prevUrlRef.current);
    };
  }, []);

  const refresh = useCallback(() => {
    if (templateId) runPreview(templateId, data);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [templateId, dataKey]);

  return { pdfUrl, loading, error, refresh };
}

'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { templatesApi } from '@/lib/api/templates.api';
import { ApiError } from '@/lib/api/client';
import { PreviewErrorDetails } from './useLivePreview';

export function useFormPreview(
  templateId: string | undefined,
  data: Record<string, unknown>
) {
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorDetails, setErrorDetails] = useState<PreviewErrorDetails | null>(null);
  const [renderLatencyMs, setRenderLatencyMs] = useState<number | null>(null);
  const [contentSizeBytes, setContentSizeBytes] = useState<number | null>(null);

  const prevUrlRef = useRef<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  const runPreview = useCallback(
    async (id: string, payload: Record<string, unknown>) => {
      // Abort any pending in-flight request
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      const controller = new AbortController();
      abortControllerRef.current = controller;

      setLoading(true);
      setError(null);
      setErrorDetails(null);
      const startTime = performance.now();

      try {
        const blob = await templatesApi.previewMappings(id, payload, { signal: controller.signal });
        
        if (controller.signal.aborted) return;

        const durationMs = Math.round(performance.now() - startTime);
        setRenderLatencyMs(durationMs);
        setContentSizeBytes(blob.size);

        if (prevUrlRef.current) URL.revokeObjectURL(prevUrlRef.current);
        const url = URL.createObjectURL(blob);
        prevUrlRef.current = url;
        setPdfUrl(url);
      } catch (e: any) {
        if (e.name === 'AbortError' || controller.signal.aborted) {
          return;
        }

        if (e instanceof ApiError) {
          setError(e.message || 'ไม่สามารถแสดง Preview ได้');
          setErrorDetails({
            code: e.errorCode,
            detail: e.detail,
            errors: e.errors,
          });
        } else {
          setError(e.message || 'ไม่สามารถแสดง Preview ได้');
          setErrorDetails(null);
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
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
      setErrorDetails(null);
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

  // Revoke blob URL and abort on unmount
  useEffect(() => {
    return () => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      if (timerRef.current) {
        clearTimeout(timerRef.current);
      }
      if (prevUrlRef.current) URL.revokeObjectURL(prevUrlRef.current);
    };
  }, []);

  const refresh = useCallback(() => {
    if (templateId) runPreview(templateId, data);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [templateId, dataKey]);

  return {
    pdfUrl,
    loading,
    error,
    errorDetails,
    renderLatencyMs,
    contentSizeBytes,
    refresh,
  };
}

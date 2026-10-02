'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { documentsApi } from '@/lib/api/documents.api';
import { ApiError } from '@/lib/api/client';

export interface PreviewErrorDetails {
  code?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function useLivePreview(slug?: string, html?: string, sampleDataJson?: string) {
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorDetails, setErrorDetails] = useState<PreviewErrorDetails | null>(null);
  const [renderLatencyMs, setRenderLatencyMs] = useState<number | null>(null);
  const [contentSizeBytes, setContentSizeBytes] = useState<number | null>(null);

  const previousUrlRef = useRef<string | null>(null);
  const timerRef = useRef<NodeJS.Timeout | null>(null);
  const abortControllerRef = useRef<AbortController | null>(null);

  const generatePreview = useCallback(async () => {
    // If we have no html, we can't preview anything for a new template
    if (!slug && !html) return;

    // Abort any pending in-flight preview request to prevent race conditions
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
      let parsedData: Record<string, any> = {};
      if (sampleDataJson) {
        try {
          parsedData = JSON.parse(sampleDataJson);
        } catch {
          // ignore parse error during typing
        }
      }

      const blob = await documentsApi.previewDocument(
        slug,
        {
          data: parsedData,
          html: html || undefined,
        },
        { signal: controller.signal }
      );

      // If aborted while reading/processing, bail out early
      if (controller.signal.aborted) return;

      const durationMs = Math.round(performance.now() - startTime);
      setRenderLatencyMs(durationMs);
      setContentSizeBytes(blob.size);

      if (previousUrlRef.current) {
        URL.revokeObjectURL(previousUrlRef.current);
      }

      const newUrl = URL.createObjectURL(blob);
      previousUrlRef.current = newUrl;
      setPdfUrl(newUrl);
    } catch (err: any) {
      if (err.name === 'AbortError' || controller.signal.aborted) {
        // Intentionally aborted by newer keystroke, do not show error
        return;
      }

      if (err instanceof ApiError) {
        setError(err.message || 'เกิดข้อผิดพลาดในการสร้าง PDF พรีวิว');
        setErrorDetails({
          code: err.errorCode,
          detail: err.detail,
          errors: err.errors,
        });
      } else {
        setError(err.message || 'เกิดข้อผิดพลาดในการสร้าง PDF พรีวิว');
        setErrorDetails(null);
      }
    } finally {
      if (!controller.signal.aborted) {
        setLoading(false);
      }
    }
  }, [slug, html, sampleDataJson]);

  // Debounced auto-preview when html or sampleData changes
  useEffect(() => {
    // We allow preview if there is a slug OR if there is html content
    if (!slug && !html) return;

    if (timerRef.current) {
      clearTimeout(timerRef.current);
    }

    timerRef.current = setTimeout(() => {
      generatePreview();
    }, 800); // 800ms debounce

    return () => {
      if (timerRef.current) {
        clearTimeout(timerRef.current);
      }
    };
  }, [slug, html, sampleDataJson, generatePreview]);

  // Clean up on unmount
  useEffect(() => {
    return () => {
      if (abortControllerRef.current) {
        abortControllerRef.current.abort();
      }
      if (timerRef.current) {
        clearTimeout(timerRef.current);
      }
      if (previousUrlRef.current) {
        URL.revokeObjectURL(previousUrlRef.current);
      }
    };
  }, []);

  return {
    pdfUrl,
    loading,
    error,
    errorDetails,
    renderLatencyMs,
    contentSizeBytes,
    refreshPreview: generatePreview,
  };
}

'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { documentsApi } from '@/lib/api/documents.api';

export function useLivePreview(slug?: string, html?: string, sampleDataJson?: string) {
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const previousUrlRef = useRef<string | null>(null);
  const timerRef = useRef<NodeJS.Timeout | null>(null);

  const generatePreview = useCallback(async () => {
    // If we have no html, we can't preview anything for a new template
    if (!slug && !html) return;
    
    setLoading(true);
    setError(null);
    try {
      let parsedData: Record<string, any> = {};
      if (sampleDataJson) {
        try {
          parsedData = JSON.parse(sampleDataJson);
        } catch {
          // ignore parse error during typing
        }
      }

      const blob = await documentsApi.previewDocument(slug, {
        data: parsedData,
        html: html || undefined,
      });

      const url = URL.createObjectURL(blob);
      if (previousUrlRef.current) {
        URL.revokeObjectURL(previousUrlRef.current);
      }

      const newUrl = URL.createObjectURL(blob);
      previousUrlRef.current = newUrl;
      setPdfUrl(newUrl);
    } catch (err: any) {
      setError(err.message || 'เกิดข้อผิดพลาดในการสร้าง PDF พรีวิว');
    } finally {
      setLoading(false);
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
      if (previousUrlRef.current) {
        URL.revokeObjectURL(previousUrlRef.current);
      }
    };
  }, []);

  return {
    pdfUrl,
    loading,
    error,
    refreshPreview: generatePreview,
  };
}

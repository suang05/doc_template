'use client';

import { useState, useCallback } from 'react';
import { OutputFormat, GenerateDocumentResponse } from '@/types/api';
import { documentsApi } from '@/lib/api/documents.api';

export function useDocumentGenerator() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<GenerateDocumentResponse | null>(null);

  const generate = useCallback(async (
    slug: string,
    params: {
      payload: Record<string, any>;
      outputFormat: OutputFormat;
      documentRef?: string;
      changeNote?: string;
    }
  ) => {
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const response = await documentsApi.generateDocument(slug, {
        payload: params.payload,
        outputFormat: params.outputFormat,
        documentRef: params.documentRef || null,
        changeNote: params.changeNote || null,
      });
      setResult(response);
      return response;
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'สร้างเอกสารล้มเหลว');
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  return {
    generate,
    loading,
    error,
    result,
    resetResult: () => setResult(null),
  };
}

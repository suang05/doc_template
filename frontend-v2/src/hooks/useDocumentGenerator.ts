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
    payload: {
      data: Record<string, any>;
      output: OutputFormat;
      documentRef?: string;
      changeNote?: string;
    }
  ) => {
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const response = await documentsApi.generateDocument(slug, {
        data: payload.data,
        output: payload.output,
        documentRef: payload.documentRef || null,
        changeNote: payload.changeNote || null,
      });
      setResult(response);
      return response;
    } catch (err: any) {
      setError(err.message || 'สร้างเอกสารล้มเหลว');
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

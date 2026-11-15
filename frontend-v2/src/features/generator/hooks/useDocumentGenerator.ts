'use client';

import { useState, useCallback } from 'react';
import { ApiError, extractErrorMessage } from '@/lib/api/client';
import type { DocumentJobResultDto, GenerateDocumentResultDto } from '@/lib/api/types';
import type { OutputFormat } from '@/constants/domain';
import { jobsApi } from '@/features/jobs/api/jobs.api';
import { documentsApi } from '../api/documents.api';

export function useDocumentGenerator() {
  const [loading, setLoading] = useState(false);
  const [enqueuing, setEnqueuing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [serverFieldErrors, setServerFieldErrors] = useState<Record<string, string>>({});
  const [result, setResult] = useState<GenerateDocumentResultDto | null>(null);
  const [queuedJob, setQueuedJob] = useState<DocumentJobResultDto | null>(null);

  const generate = useCallback(
    async (
      slug: string,
      params: {
        payload: Record<string, unknown>;
        outputFormat: OutputFormat;
        documentRef?: string;
        changeNote?: string;
        idempotencyKey?: string;
      },
    ) => {
      setLoading(true);
      setError(null);
      setServerFieldErrors({});
      setResult(null);
      setQueuedJob(null);
      try {
        const response = await documentsApi.generateDocument(
          slug,
          {
            payload: params.payload,
            outputFormat: params.outputFormat,
            documentRef: params.documentRef || null,
            changeNote: params.changeNote || null,
          },
          params.idempotencyKey,
        );
        setResult(response);
        return response;
      } catch (err) {
        if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
          setServerFieldErrors(err.fieldErrors);
        }
        setError(extractErrorMessage(err, 'สร้างเอกสารล้มเหลว'));
        throw err;
      } finally {
        setLoading(false);
      }
    },
    [],
  );

  const enqueueBackground = useCallback(
    async (
      slug: string,
      params: {
        payload: Record<string, unknown>;
        outputFormat: OutputFormat;
        documentRef?: string;
        idempotencyKey?: string;
      },
    ) => {
      setEnqueuing(true);
      setError(null);
      setServerFieldErrors({});
      setResult(null);
      setQueuedJob(null);
      try {
        const jobRes = await jobsApi.createJob(
          {
            slug,
            payload: params.payload,
            outputFormat: params.outputFormat,
            documentRef: params.documentRef || null,
            maxAttempts: 3,
          },
          params.idempotencyKey,
        );
        setQueuedJob(jobRes.data);
        return jobRes.data;
      } catch (err) {
        if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
          setServerFieldErrors(err.fieldErrors);
        }
        setError(extractErrorMessage(err, 'ส่งคำสั่งเข้าคิว Pipeline C ล้มเหลว'));
        throw err;
      } finally {
        setEnqueuing(false);
      }
    },
    [],
  );

  return {
    generate,
    enqueueBackground,
    loading,
    enqueuing,
    error,
    serverFieldErrors,
    result,
    queuedJob,
    resetResult: () => {
      setResult(null);
      setQueuedJob(null);
      setError(null);
      setServerFieldErrors({});
    },
  };
}

'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { extractErrorMessage } from '@/lib/api/client';
import type {
  CreateDocumentJobRequest,
  DocumentJobItemDto,
  DocumentJobResultDto,
} from '@/lib/api/types';
import { TERMINAL_JOB_STATUSES } from '@/constants/domain';
import { downloadBlobFile } from '@/utils/download';
import { jobsApi } from '../api/jobs.api';

const DEFAULT_POLL_MS = 2500;
const MIN_POLL_MS = 1500;
const MAX_POLL_MS = 15000;

function clampPollMs(retryAfterSeconds?: number): number {
  if (!retryAfterSeconds || retryAfterSeconds <= 0) return DEFAULT_POLL_MS;
  return Math.min(MAX_POLL_MS, Math.max(MIN_POLL_MS, retryAfterSeconds * 1000));
}

export function useDocumentJobs() {
  const [jobs, setJobs] = useState<DocumentJobResultDto[]>([]);
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [selectedJob, setSelectedJob] = useState<DocumentJobResultDto | null>(null);
  const [selectedJobEtag, setSelectedJobEtag] = useState<string | null>(null);
  const [selectedItems, setSelectedItems] = useState<DocumentJobItemDto[]>([]);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [mutatingJobId, setMutatingJobId] = useState<string | null>(null);
  const [downloadingJobId, setDownloadingJobId] = useState<string | null>(null);
  const [pollDelayMs, setPollDelayMs] = useState<number>(DEFAULT_POLL_MS);

  const pollTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const fetchJobs = useCallback(
    async (silent = false) => {
      if (!silent) setLoading(true);
      setError(null);
      try {
        const res = await jobsApi.listJobs({
          status: statusFilter || undefined,
          limit: 20,
        });
        setJobs(res.items ?? []);
        setNextCursor(res.nextCursor ?? null);
        setHasMore(Boolean(res.hasMore));
      } catch (err) {
        if (!silent) {
          setError(extractErrorMessage(err, 'ไม่สามารถโหลดรายการงาน Pipeline C ได้'));
        }
      } finally {
        if (!silent) setLoading(false);
      }
    },
    [statusFilter],
  );

  useEffect(() => {
    void fetchJobs();
  }, [fetchJobs]);

  const inspectJob = useCallback(async (jobId: string, silent = false) => {
    if (!silent) setLoadingDetail(true);
    try {
      const [detailRes, itemsRes] = await Promise.all([
        jobsApi.getJobWithETag(jobId),
        jobsApi.listJobItems(jobId),
      ]);
      setSelectedJob(detailRes.data);
      setSelectedJobEtag(detailRes.etag);
      setSelectedItems(itemsRes);
      if (detailRes.retryAfterSeconds) {
        setPollDelayMs(clampPollMs(detailRes.retryAfterSeconds));
      }
    } catch (err) {
      if (!silent) {
        throw err;
      }
    } finally {
      if (!silent) setLoadingDetail(false);
    }
  }, []);

  const closeDetail = useCallback(() => {
    setSelectedJob(null);
    setSelectedJobEtag(null);
    setSelectedItems([]);
  }, []);

  const hasActiveJobs =
    jobs.some((j) => !TERMINAL_JOB_STATUSES.has(j.status)) ||
    (selectedJob !== null && !TERMINAL_JOB_STATUSES.has(selectedJob.status));

  useEffect(() => {
    if (pollTimerRef.current) {
      clearTimeout(pollTimerRef.current);
      pollTimerRef.current = null;
    }

    if (!hasActiveJobs) return;

    pollTimerRef.current = setTimeout(() => {
      void fetchJobs(true);
      if (selectedJob && !TERMINAL_JOB_STATUSES.has(selectedJob.status)) {
        void inspectJob(selectedJob.jobId, true);
      }
    }, pollDelayMs);

    return () => {
      if (pollTimerRef.current) {
        clearTimeout(pollTimerRef.current);
        pollTimerRef.current = null;
      }
    };
  }, [hasActiveJobs, pollDelayMs, fetchJobs, inspectJob, selectedJob]);

  const loadMore = useCallback(async () => {
    if (!hasMore || !nextCursor || loadingMore) return;
    setLoadingMore(true);
    try {
      const res = await jobsApi.listJobs({
        status: statusFilter || undefined,
        cursor: nextCursor,
        limit: 20,
      });
      setJobs((prev) => [...prev, ...(res.items ?? [])]);
      setNextCursor(res.nextCursor ?? null);
      setHasMore(Boolean(res.hasMore));
    } finally {
      setLoadingMore(false);
    }
  }, [hasMore, nextCursor, loadingMore, statusFilter]);

  const createJob = useCallback(
    async (request: CreateDocumentJobRequest, idempotencyKey?: string) => {
      const res = await jobsApi.createJob(request, idempotencyKey);
      if (res.retryAfterSeconds) {
        setPollDelayMs(clampPollMs(res.retryAfterSeconds));
      } else {
        setPollDelayMs(DEFAULT_POLL_MS);
      }
      await fetchJobs(true);
      return res.data;
    },
    [fetchJobs],
  );

  const cancelJob = useCallback(
    async (jobId: string, knownEtag?: string | null) => {
      setMutatingJobId(jobId);
      try {
        const etag = knownEtag ?? (await jobsApi.getJobWithETag(jobId)).etag;
        const updated = await jobsApi.cancelJob(jobId, etag);
        setJobs((prev) => prev.map((j) => (j.jobId === jobId ? updated.data : j)));
        if (selectedJob?.jobId === jobId) {
          setSelectedJob(updated.data);
          setSelectedJobEtag(updated.etag);
        }
        return updated.data;
      } finally {
        setMutatingJobId(null);
      }
    },
    [selectedJob?.jobId],
  );

  const retryJob = useCallback(
    async (jobId: string, knownEtag?: string | null) => {
      setMutatingJobId(jobId);
      try {
        const etag = knownEtag ?? (await jobsApi.getJobWithETag(jobId)).etag;
        const updated = await jobsApi.retryJob(jobId, etag);
        setPollDelayMs(DEFAULT_POLL_MS);
        setJobs((prev) => prev.map((j) => (j.jobId === jobId ? updated.data : j)));
        if (selectedJob?.jobId === jobId) {
          setSelectedJob(updated.data);
          setSelectedJobEtag(updated.etag);
        }
        return updated.data;
      } finally {
        setMutatingJobId(null);
      }
    },
    [selectedJob?.jobId],
  );

  const downloadJobResult = useCallback(async (job: DocumentJobResultDto) => {
    setDownloadingJobId(job.jobId);
    try {
      const { blob, fileName } = await jobsApi.downloadResult(job.jobId);
      const fallbackName =
        job.totalItems > 1
          ? `${job.templateSlug}-${job.jobId.slice(0, 8)}.zip`
          : `${job.templateSlug}-${job.jobId.slice(0, 8)}.${job.outputFormat}`;
      downloadBlobFile(blob, fileName || fallbackName);
    } finally {
      setDownloadingJobId(null);
    }
  }, []);

  return {
    jobs,
    statusFilter,
    setStatusFilter,
    hasMore,
    loading,
    loadingMore,
    error,
    selectedJob,
    selectedJobEtag,
    selectedItems,
    loadingDetail,
    mutatingJobId,
    downloadingJobId,
    hasActiveJobs,
    fetchJobs,
    loadMore,
    inspectJob,
    closeDetail,
    createJob,
    cancelJob,
    retryJob,
    downloadJobResult,
  };
}

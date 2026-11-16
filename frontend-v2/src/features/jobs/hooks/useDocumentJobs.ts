'use client';

import { useState, useCallback, useMemo } from 'react';
import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
  type InfiniteData,
} from '@tanstack/react-query';
import { extractErrorMessage } from '@/lib/api/client';
import type {
  CreateDocumentJobRequest,
  DocumentJobItemDto,
  DocumentJobResultDto,
  PagedEnvelope,
} from '@/lib/api/types';
import { TERMINAL_JOB_STATUSES } from '@/constants/domain';
import { downloadBlobFile } from '@/utils/download';
import { jobsApi, type DocumentJobResource } from '../api/jobs.api';

const DEFAULT_POLL_MS = 2500;
const MIN_POLL_MS = 1500;
const MAX_POLL_MS = 15000;

interface InspectedJobDetail {
  detail: DocumentJobResource;
  items: DocumentJobItemDto[];
}

function clampPollMs(retryAfterSeconds?: number): number {
  if (!retryAfterSeconds || retryAfterSeconds <= 0) return DEFAULT_POLL_MS;
  return Math.min(MAX_POLL_MS, Math.max(MIN_POLL_MS, retryAfterSeconds * 1000));
}

export const documentJobsQueryKey = (statusFilter: string) =>
  ['document-jobs', statusFilter] as const;

export const documentJobDetailQueryKey = (jobId: string | null) =>
  ['document-job-detail', jobId] as const;

export function useDocumentJobs() {
  const queryClient = useQueryClient();
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [inspectedJobId, setInspectedJobId] = useState<string | null>(null);
  const [mutatingJobId, setMutatingJobId] = useState<string | null>(null);
  const [downloadingJobId, setDownloadingJobId] = useState<string | null>(null);
  const [pollDelayMs, setPollDelayMs] = useState<number>(DEFAULT_POLL_MS);

  const fetchJobDetailBundle = useCallback(async (jobId: string): Promise<InspectedJobDetail> => {
    const [detail, items] = await Promise.all([
      jobsApi.getJobWithETag(jobId),
      jobsApi.listJobItems(jobId),
    ]);
    if (detail.retryAfterSeconds) {
      setPollDelayMs(clampPollMs(detail.retryAfterSeconds));
    }
    return { detail, items };
  }, []);

  const listQuery = useInfiniteQuery<
    PagedEnvelope<DocumentJobResultDto>,
    Error,
    InfiniteData<PagedEnvelope<DocumentJobResultDto>>,
    ReturnType<typeof documentJobsQueryKey>,
    string | null
  >({
    queryKey: documentJobsQueryKey(statusFilter),
    initialPageParam: null,
    queryFn: ({ pageParam }) =>
      jobsApi.listJobs({
        status: statusFilter || undefined,
        cursor: pageParam,
        limit: 20,
      }),
    getNextPageParam: (lastPage) => lastPage.pagination?.nextCursor ?? undefined,
    refetchInterval: (query) => {
      if (query.state.status === 'error') return false;
      const pages = query.state.data?.pages ?? [];
      const hasPending = pages.some((p) =>
        (p.data ?? []).some((j) => !TERMINAL_JOB_STATUSES.has(j.status)),
      );
      return hasPending ? pollDelayMs : false;
    },
  });

  const jobs = useMemo(
    () => listQuery.data?.pages.flatMap((page) => page.data ?? []) ?? [],
    [listQuery.data],
  );

  const detailQuery = useQuery<InspectedJobDetail, Error>({
    queryKey: documentJobDetailQueryKey(inspectedJobId),
    enabled: Boolean(inspectedJobId),
    queryFn: () => fetchJobDetailBundle(inspectedJobId!),
    refetchInterval: (query) => {
      if (query.state.status === 'error') return false;
      const status = query.state.data?.detail.data.status;
      if (!status || TERMINAL_JOB_STATUSES.has(status)) return false;
      return clampPollMs(query.state.data?.detail.retryAfterSeconds) || pollDelayMs;
    },
  });

  const selectedJob = detailQuery.data?.detail.data ?? null;
  const selectedJobEtag = detailQuery.data?.detail.etag ?? null;
  const selectedItems = detailQuery.data?.items ?? [];

  const hasActiveJobs =
    jobs.some((j) => !TERMINAL_JOB_STATUSES.has(j.status)) ||
    (selectedJob !== null && !TERMINAL_JOB_STATUSES.has(selectedJob.status));

  const fetchJobs = useCallback(
    async (_silent = false) => {
      await listQuery.refetch();
    },
    [listQuery],
  );

  const inspectJob = useCallback(
    async (jobId: string, _silent = false) => {
      setInspectedJobId(jobId);
      await queryClient.fetchQuery({
        queryKey: documentJobDetailQueryKey(jobId),
        queryFn: () => fetchJobDetailBundle(jobId),
      });
    },
    [fetchJobDetailBundle, queryClient],
  );

  const closeDetail = useCallback(() => {
    setInspectedJobId(null);
  }, []);

  const loadMore = useCallback(async () => {
    if (!listQuery.hasNextPage || listQuery.isFetchingNextPage) return;
    await listQuery.fetchNextPage();
  }, [listQuery]);

  const createMutation = useMutation({
    mutationFn: ({
      request,
      idempotencyKey,
    }: {
      request: CreateDocumentJobRequest;
      idempotencyKey?: string;
    }) => jobsApi.createJob(request, idempotencyKey),
    onSuccess: async (res) => {
      setPollDelayMs(clampPollMs(res.retryAfterSeconds));
      await queryClient.invalidateQueries({ queryKey: ['document-jobs'] });
    },
  });

  const createJob = useCallback(
    async (request: CreateDocumentJobRequest, idempotencyKey?: string) => {
      const res = await createMutation.mutateAsync({ request, idempotencyKey });
      return res.data;
    },
    [createMutation],
  );

  const cancelJob = useCallback(
    async (jobId: string, knownEtag?: string | null) => {
      setMutatingJobId(jobId);
      try {
        const etag = knownEtag ?? (await jobsApi.getJobWithETag(jobId)).etag;
        const updated = await jobsApi.cancelJob(jobId, etag);
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: ['document-jobs'] }),
          queryClient.invalidateQueries({ queryKey: documentJobDetailQueryKey(jobId) }),
        ]);
        return updated.data;
      } finally {
        setMutatingJobId(null);
      }
    },
    [queryClient],
  );

  const retryJob = useCallback(
    async (jobId: string, knownEtag?: string | null) => {
      setMutatingJobId(jobId);
      try {
        const etag = knownEtag ?? (await jobsApi.getJobWithETag(jobId)).etag;
        const updated = await jobsApi.retryJob(jobId, etag);
        setPollDelayMs(DEFAULT_POLL_MS);
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: ['document-jobs'] }),
          queryClient.invalidateQueries({ queryKey: documentJobDetailQueryKey(jobId) }),
        ]);
        return updated.data;
      } finally {
        setMutatingJobId(null);
      }
    },
    [queryClient],
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

  const error = listQuery.error
    ? extractErrorMessage(listQuery.error, 'ไม่สามารถโหลดรายการงาน Pipeline C ได้')
    : null;

  return {
    jobs,
    statusFilter,
    setStatusFilter,
    hasMore: Boolean(listQuery.hasNextPage),
    loading: listQuery.isLoading,
    loadingMore: listQuery.isFetchingNextPage,
    error,
    selectedJob,
    selectedJobEtag,
    selectedItems,
    loadingDetail: detailQuery.isLoading,
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

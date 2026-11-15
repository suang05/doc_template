'use client';

import React, { useState, useCallback } from 'react';
import {
  Layers,
  Plus,
  RefreshCw,
  Eye,
  XCircle,
  RotateCcw,
  Download,
  Loader2,
} from 'lucide-react';
import {
  PageHeader,
  Button,
  Badge,
  Table,
  Column,
  Modal,
  Input,
  Select,
  EmptyState,
  ConflictBanner,
  Toast,
} from '@/components/ui';
import { ApiError, extractErrorMessage } from '@/lib/api/client';
import type { CreateDocumentJobItemRequest, DocumentJobResultDto } from '@/lib/api/types';
import {
  documentJobStatuses,
  outputFormats,
  type OutputFormat,
} from '@/constants/domain';
import type { SystemStatus } from '@/tokens';
import { useConflictRecovery } from '@/hooks/useConflictRecovery';
import { useIdempotencyKey } from '@/hooks/useIdempotencyKey';
import { formatThaiDateTime } from '@/utils/format';
import { useTemplates } from '@/features/templates';
import { useDocumentJobs } from '../hooks/useDocumentJobs';

function jobStatusToBadge(status: string): SystemStatus {
  if (status === 'Succeeded') return 'success';
  if (status === 'Failed' || status === 'Canceled') return 'failed';
  if (status === 'Queued' || status === 'Running') return 'pending';
  return 'idle';
}

const STATUS_FILTER_OPTIONS = [
  { value: '', label: 'ทั้งหมด' },
  ...documentJobStatuses.map((s) => ({ value: s, label: s })),
];

const FORMAT_OPTIONS = outputFormats.map((fmt) => ({
  value: fmt,
  label: fmt.toUpperCase(),
}));

const DEFAULT_BATCH_JSON = `[
  {
    "documentRef": "DOC-2026-001",
    "payload": {
      "contractNo": "SMK-001",
      "customerName": "นายสมชาย ใจดี"
    }
  }
]`;

const DEFAULT_SINGLE_JSON = `{
  "contractNo": "SMK-001",
  "customerName": "นายสมชาย ใจดี"
}`;

export const JobsView: React.FC = () => {
  const { templates } = useTemplates();
  const {
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
  } = useDocumentJobs();
  const { conflict, captureConflict, clearConflict } = useConflictRecovery();

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [slug, setSlug] = useState('');
  const [mode, setMode] = useState<'single' | 'batch'>('single');
  const [outputFormat, setOutputFormat] = useState<OutputFormat>('pdf');
  const [documentRef, setDocumentRef] = useState('');
  const [maxAttempts, setMaxAttempts] = useState(3);
  const [webhookUrl, setWebhookUrl] = useState('');
  const [jsonInput, setJsonInput] = useState(DEFAULT_SINGLE_JSON);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(
    null,
  );
  const { getKey: getJobIdempotencyKey, rotateKey: rotateJobIdempotencyKey, resetKey: resetJobIdempotencyKey } = useIdempotencyKey();

  const openCreateModal = () => {
    setFieldErrors({});
    setSlug((prev) => prev || templates[0]?.slug || '');
    rotateJobIdempotencyKey();
    setIsCreateOpen(true);
  };

  const closeCreateModal = () => {
    setIsCreateOpen(false);
    setFieldErrors({});
    resetJobIdempotencyKey();
  };

  const handleModeChange = (nextMode: 'single' | 'batch') => {
    setMode(nextMode);
    setJsonInput(nextMode === 'batch' ? DEFAULT_BATCH_JSON : DEFAULT_SINGLE_JSON);
  };

  const handleCreateJob = async (e: React.FormEvent) => {
    e.preventDefault();
    setFieldErrors({});
    const targetSlug = slug || templates[0]?.slug || '';
    if (!targetSlug) {
      setFieldErrors({ slug: 'กรุณาเลือกแม่แบบเอกสาร' });
      return;
    }

    let parsedJson: unknown;
    try {
      parsedJson = JSON.parse(jsonInput);
    } catch {
      setToast({ type: 'error', message: 'รูปแบบ JSON Payload ไม่ถูกต้อง' });
      return;
    }

    let items: CreateDocumentJobItemRequest[] | null = null;
    let payload: unknown = undefined;

    if (mode === 'batch') {
      if (!Array.isArray(parsedJson) || parsedJson.length === 0) {
        setToast({ type: 'error', message: 'Batch mode ต้องเป็น JSON Array ของ { payload, documentRef? }' });
        return;
      }
      items = parsedJson.map((entry) => {
        if (entry && typeof entry === 'object' && 'payload' in entry) {
          const item = entry as { payload: unknown; documentRef?: string | null };
          return {
            payload: item.payload,
            documentRef: item.documentRef ?? null,
          };
        }
        return { payload: entry, documentRef: null };
      });
    } else {
      payload = parsedJson;
    }

    setIsSubmitting(true);
    const attemptKey = getJobIdempotencyKey();

    try {
      const created = await createJob(
        {
          slug: targetSlug,
          items,
          payload,
          outputFormat,
          documentRef: documentRef.trim() || null,
          maxAttempts,
          webhookUrl: webhookUrl.trim() || null,
        },
        attemptKey,
      );
      closeCreateModal();
      setToast({ type: 'success', message: `ส่งคำสั่งคิวงาน ${created.jobId.slice(0, 8)} สำเร็จ` });
      void inspectJob(created.jobId);
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        setFieldErrors(err.fieldErrors);
      }
      setToast({ type: 'error', message: extractErrorMessage(err, 'สร้าง Job ไม่สำเร็จ') });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleInspect = async (jobId: string) => {
    clearConflict();
    try {
      await inspectJob(jobId);
    } catch (err) {
      setToast({ type: 'error', message: extractErrorMessage(err, 'โหลดรายละเอียดงานไม่สำเร็จ') });
    }
  };

  const handleCancel = async (jobId: string, knownEtag?: string | null) => {
    clearConflict();
    try {
      await cancelJob(jobId, knownEtag);
      setToast({ type: 'info', message: 'ส่งคำขอยกเลิกงานเรียบร้อยแล้ว' });
    } catch (err) {
      if (captureConflict(err)) return;
      setToast({ type: 'error', message: extractErrorMessage(err, 'ยกเลิกงานไม่สำเร็จ') });
    }
  };

  const handleRetry = async (jobId: string, knownEtag?: string | null) => {
    clearConflict();
    try {
      await retryJob(jobId, knownEtag);
      setToast({ type: 'success', message: 'ส่งงานเข้าคิว Retry สำเร็จ' });
    } catch (err) {
      if (captureConflict(err)) return;
      setToast({ type: 'error', message: extractErrorMessage(err, 'Retry งานไม่สำเร็จ') });
    }
  };

  const handleDownload = async (job: DocumentJobResultDto) => {
    try {
      await downloadJobResult(job);
    } catch (err) {
      setToast({ type: 'error', message: extractErrorMessage(err, 'ดาวน์โหลดผลลัพธ์ไม่สำเร็จ') });
    }
  };

  const handleReloadConflict = useCallback(async () => {
    clearConflict();
    await fetchJobs();
    if (selectedJob) {
      await inspectJob(selectedJob.jobId);
    }
    setToast({ type: 'info', message: 'อัปเดตสถานะงานล่าสุดแล้ว' });
  }, [clearConflict, fetchJobs, inspectJob, selectedJob]);

  const templateOptions = templates.map((t) => ({
    value: t.slug,
    label: `${t.name} (${t.slug})`,
  }));

  const columns: Column<DocumentJobResultDto>[] = [
    {
      header: 'งาน (Job / Template)',
      accessor: (job) => (
        <div className="space-y-0.5">
          <p className="text-xs font-semibold text-textPrimary font-mono">{job.templateSlug}</p>
          <p className="text-[10px] font-mono text-textMuted">{job.jobId}</p>
        </div>
      ),
    },
    {
      header: 'รูปแบบ',
      accessor: (job) => (
        <span className="font-mono text-[11px] uppercase px-2 py-0.5 rounded-xs bg-slate-100 border border-slate-200">
          {job.outputFormat}
        </span>
      ),
      align: 'center',
    },
    {
      header: 'สถานะ',
      accessor: (job) => (
        <Badge status={jobStatusToBadge(job.status)} dot size="sm">
          {job.status}
          {job.cancelRequested && job.status === 'Running' ? ' (กำลังยกเลิก)' : ''}
        </Badge>
      ),
      align: 'center',
    },
    {
      header: 'ความคืบหน้า (Items)',
      accessor: (job) => (
        <div className="text-[11px] font-mono text-textSecondary text-center">
          <span>
            {job.succeededItems}/{job.totalItems}
          </span>
          {job.failedItems > 0 && (
            <span className="ml-1.5 text-red-600">({job.failedItems} ล้มเหลว)</span>
          )}
        </div>
      ),
      align: 'center',
    },
    {
      header: 'Attempt',
      accessor: (job) => (
        <span className="text-[11px] font-mono text-textMuted">
          {job.attempt}/{job.maxAttempts}
        </span>
      ),
      align: 'center',
    },
    {
      header: 'สร้างเมื่อ',
      accessor: (job) => (
        <span className="text-[11px] font-mono text-textMuted">
          {formatThaiDateTime(job.createdAt)}
        </span>
      ),
    },
    {
      header: 'จัดการ',
      accessor: (job) => (
        <div className="flex items-center justify-end gap-1">
          <Button
            variant="ghost"
            size="sm"
            icon={Eye}
            onClick={() => void handleInspect(job.jobId)}
          >
            รายละเอียด
          </Button>
          {(job.status === 'Queued' || job.status === 'Running') && (
            <Button
              variant="ghost"
              size="sm"
              icon={XCircle}
              loading={mutatingJobId === job.jobId}
              onClick={() => void handleCancel(job.jobId)}
              className="text-amber-700 hover:bg-amber-50"
            >
              ยกเลิก
            </Button>
          )}
          {(job.status === 'Failed' || job.status === 'Canceled') && (
            <Button
              variant="outline"
              size="sm"
              icon={RotateCcw}
              loading={mutatingJobId === job.jobId}
              onClick={() => void handleRetry(job.jobId)}
            >
              Retry
            </Button>
          )}
          {job.status === 'Succeeded' && (
            <Button
              variant="success"
              size="sm"
              icon={Download}
              loading={downloadingJobId === job.jobId}
              onClick={() => void handleDownload(job)}
            >
              ดาวน์โหลด
            </Button>
          )}
        </div>
      ),
      align: 'right',
    },
  ];

  return (
    <div className="space-y-4">
      {toast && <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} />}

      <PageHeader
        title="คิวงานสร้างเอกสารพื้นหลัง (Pipeline C Jobs)"
        description="ติดตามคิวงานสร้างเอกสารชุดใหญ่ (Async & Batch), ดาวน์โหลดไฟล์ผลลัพธ์, Cancel และ Retry แบบ Idempotent"
        icon={Layers}
        iconColor="sky"
        rightSlot={
          <div className="flex items-center gap-2">
            {hasActiveJobs && (
              <span className="inline-flex items-center gap-1.5 text-[11px] font-mono text-sky-700 bg-sky-50 border border-sky-200 px-2 py-1 rounded-sm">
                <Loader2 className="w-3 h-3 animate-spin" />
                Auto Polling
              </span>
            )}
            <Button
              variant="outline"
              size="sm"
              icon={RefreshCw}
              loading={loading}
              onClick={() => void fetchJobs()}
            >
              รีเฟรช
            </Button>
            <Button variant="primary" size="sm" icon={Plus} onClick={openCreateModal}>
              สร้างงาน Batch / Async
            </Button>
          </div>
        }
      />

      {!selectedJob && conflict && (
        <ConflictBanner
          conflict={conflict}
          onReload={handleReloadConflict}
          onDismiss={clearConflict}
        />
      )}

      {error && (
        <div className="p-3 bg-red-50 border border-red-200 text-red-700 text-xs rounded-sm">
          {error}
        </div>
      )}

      <div className="flex items-center gap-2 flex-wrap">
        <div className="w-56">
          <Select
            label="กรองตามสถานะ"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            options={STATUS_FILTER_OPTIONS}
          />
        </div>
      </div>

      {jobs.length === 0 && !loading ? (
        <EmptyState
          icon={Layers}
          title="ยังไม่มีคิวงาน Pipeline C"
          description="ส่งคำขอสร้างเอกสารแบบ Async หรือ Batch ZIP เพื่อประมวลผลในเบื้องหลัง"
          actionLabel="สร้างงานแรก"
          onAction={openCreateModal}
        />
      ) : (
        <div className="space-y-3">
          <Table columns={columns} data={jobs} keyExtractor={(j) => j.jobId} loading={loading} />
          {hasMore && (
            <div className="flex justify-center">
              <Button variant="outline" size="sm" loading={loadingMore} onClick={() => void loadMore()}>
                โหลดรายการถัดไป
              </Button>
            </div>
          )}
        </div>
      )}

      {/* Job Detail Modal */}
      <Modal
        isOpen={Boolean(selectedJob)}
        onClose={() => {
          closeDetail();
          clearConflict();
        }}
        title={
          selectedJob
            ? `รายละเอียด Job: ${selectedJob.templateSlug} (${selectedJob.jobId.slice(0, 8)})`
            : 'รายละเอียด Job'
        }
        maxWidth="2xl"
      >
        {selectedJob && (
          <div className="space-y-4">
            {conflict && (
              <ConflictBanner
                conflict={conflict}
                onReload={handleReloadConflict}
                onDismiss={clearConflict}
              />
            )}

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 text-xs">
              <div className="p-2.5 bg-surfaceSubtle border border-border rounded-sm">
                <span className="text-[10px] text-textMuted block">สถานะ</span>
                <Badge status={jobStatusToBadge(selectedJob.status)} dot size="sm">
                  {selectedJob.status}
                </Badge>
              </div>
              <div className="p-2.5 bg-surfaceSubtle border border-border rounded-sm">
                <span className="text-[10px] text-textMuted block">ความคืบหน้า</span>
                <span className="font-mono font-semibold text-textPrimary">
                  {selectedJob.succeededItems}/{selectedJob.totalItems} (พลาด {selectedJob.failedItems})
                </span>
              </div>
              <div className="p-2.5 bg-surfaceSubtle border border-border rounded-sm">
                <span className="text-[10px] text-textMuted block">Attempt</span>
                <span className="font-mono font-semibold text-textPrimary">
                  {selectedJob.attempt} / {selectedJob.maxAttempts}
                </span>
              </div>
              <div className="p-2.5 bg-surfaceSubtle border border-border rounded-sm">
                <span className="text-[10px] text-textMuted block">ETag Version</span>
                <span className="font-mono text-textSecondary">{selectedJobEtag ?? '—'}</span>
              </div>
            </div>

            {selectedJob.errorMessage && (
              <div className="p-2.5 bg-red-50 border border-red-200 text-red-800 text-xs rounded-sm font-mono">
                {selectedJob.errorCode ? `[${selectedJob.errorCode}] ` : ''}
                {selectedJob.errorMessage}
              </div>
            )}

            <div className="space-y-1.5">
              <h4 className="text-xs font-semibold text-textPrimary">
                รายการย่อย (Job Items: {selectedItems.length})
              </h4>
              {loadingDetail ? (
                <p className="text-xs text-textMuted">กำลังโหลดรายการย่อย...</p>
              ) : (
                <div className="max-h-56 overflow-auto border border-border rounded-sm">
                  <table className="min-w-full text-xs border-collapse">
                    <thead className="bg-surfaceSubtle border-b border-border text-[11px] text-textMuted">
                      <tr>
                        <th className="px-2.5 py-1.5 text-left">#</th>
                        <th className="px-2.5 py-1.5 text-left">Document Ref</th>
                        <th className="px-2.5 py-1.5 text-left">สถานะ</th>
                        <th className="px-2.5 py-1.5 text-left">Error</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-border">
                      {selectedItems.map((item) => (
                        <tr key={item.id}>
                          <td className="px-2.5 py-1.5 font-mono text-textMuted">
                            {item.itemIndex + 1}
                          </td>
                          <td className="px-2.5 py-1.5 font-mono text-textPrimary">
                            {item.documentRef || '—'}
                          </td>
                          <td className="px-2.5 py-1.5">
                            <Badge status={jobStatusToBadge(item.status)} dot size="sm">
                              {item.status}
                            </Badge>
                          </td>
                          <td className="px-2.5 py-1.5 font-mono text-[11px] text-red-700">
                            {item.errorMessage || '—'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            <div className="pt-3 border-t border-border flex items-center justify-between gap-2">
              <div className="flex items-center gap-2">
                {(selectedJob.status === 'Queued' || selectedJob.status === 'Running') && (
                  <Button
                    variant="outline"
                    size="sm"
                    icon={XCircle}
                    loading={mutatingJobId === selectedJob.jobId}
                    onClick={() => void handleCancel(selectedJob.jobId, selectedJobEtag)}
                  >
                    ยกเลิกงาน (:cancel)
                  </Button>
                )}
                {(selectedJob.status === 'Failed' || selectedJob.status === 'Canceled') && (
                  <Button
                    variant="outline"
                    size="sm"
                    icon={RotateCcw}
                    loading={mutatingJobId === selectedJob.jobId}
                    onClick={() => void handleRetry(selectedJob.jobId, selectedJobEtag)}
                  >
                    ส่งซ้ำ (:retry)
                  </Button>
                )}
              </div>

              <div className="flex items-center gap-2">
                {selectedJob.status === 'Succeeded' && (
                  <Button
                    variant="success"
                    size="sm"
                    icon={Download}
                    loading={downloadingJobId === selectedJob.jobId}
                    onClick={() => void handleDownload(selectedJob)}
                  >
                    ดาวน์โหลดผลลัพธ์
                  </Button>
                )}
                <Button variant="ghost" size="sm" onClick={closeDetail}>
                  ปิด
                </Button>
              </div>
            </div>
          </div>
        )}
      </Modal>

      {/* Enqueue Job Modal */}
      <Modal
        isOpen={isCreateOpen}
        onClose={closeCreateModal}
        title="ส่งคำสั่งสร้างเอกสารแบบ Background (Pipeline C)"
        maxWidth="lg"
      >
        <form onSubmit={handleCreateJob} className="space-y-3">
          <Select
            label="แม่แบบเอกสาร (Template Slug)"
            value={slug || templates[0]?.slug || ''}
            options={templateOptions}
            error={fieldErrors.slug}
            onChange={(e) => setSlug(e.target.value)}
          />

          <div className="grid grid-cols-2 gap-3">
            <Select
              label="โหมดการทำงาน"
              value={mode}
              options={[
                { value: 'single', label: 'Single Document' },
                { value: 'batch', label: 'Batch Array (ZIP Bundle)' },
              ]}
              onChange={(e) => handleModeChange(e.target.value as 'single' | 'batch')}
            />
            <Select
              label="รูปแบบผลลัพธ์"
              value={outputFormat}
              options={FORMAT_OPTIONS}
              error={fieldErrors.outputFormat}
              onChange={(e) => setOutputFormat(e.target.value as OutputFormat)}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            {mode === 'single' ? (
              <Input
                label="Document Ref (ไม่บังคับ)"
                value={documentRef}
                onChange={(e) => setDocumentRef(e.target.value)}
                placeholder="เช่น DOC-2026-001"
              />
            ) : (
              <Input
                label="Max Attempts"
                type="number"
                min={1}
                max={10}
                value={maxAttempts}
                onChange={(e) =>
                  setMaxAttempts(Math.max(1, Math.min(10, Number(e.target.value) || 3)))
                }
              />
            )}
            <Input
              label="Webhook Callback URL (ไม่บังคับ)"
              value={webhookUrl}
              onChange={(e) => setWebhookUrl(e.target.value)}
              placeholder="https://example.internal/hooks/doc-ready"
              error={fieldErrors.webhookUrl}
            />
          </div>

          <div className="space-y-1">
            <label className="block text-xs font-medium text-textSecondary">
              {mode === 'batch' ? 'Batch Items JSON Array' : 'Payload JSON'}
            </label>
            <textarea
              value={jsonInput}
              onChange={(e) => setJsonInput(e.target.value)}
              className="w-full h-44 p-3 rounded-sm font-mono text-xs bg-zinc-900 text-zinc-200 border border-border outline-none focus:border-primary"
            />
          </div>

          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <Button type="button" variant="ghost" size="sm" onClick={closeCreateModal}>
              ยกเลิก
            </Button>
            <Button type="submit" size="sm" loading={isSubmitting}>
              ส่งเข้าคิว Pipeline C
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
};

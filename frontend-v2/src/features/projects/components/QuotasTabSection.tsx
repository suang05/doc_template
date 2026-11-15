'use client';

import React, { useEffect, useState } from 'react';
import { RefreshCw, Save, Gauge } from 'lucide-react';
import { Button, CardBlock, EmptyState, Input, Select } from '@/components/ui';
import { ApiError, extractErrorMessage } from '@/lib/api/client';
import type { ProjectDto, ProjectQuotaDto, UpsertProjectQuotaRequest } from '@/lib/api/types';
import { upsertProjectQuotaSchema } from '../schemas/project.schema';
import { formatBytes, formatThaiDateTime } from '@/utils/format';
import { toFieldErrorMap } from '@/utils/zod';

interface QuotasTabSectionProps {
  tenants: ProjectDto[];
  selectedProjectId: string;
  onSelectProject: (projectId: string) => void;
  quota: ProjectQuotaDto | null;
  quotaEtag: string | null;
  loading: boolean;
  saving: boolean;
  onLoadQuota: (projectId: string) => Promise<void>;
  onSaveQuota: (
    projectId: string,
    request: UpsertProjectQuotaRequest,
    ifMatch?: string | null,
  ) => Promise<ProjectQuotaDto>;
  onConflict: (err: unknown, reloadFn?: () => Promise<void>) => boolean;
  onNotify: (type: 'success' | 'error' | 'info', message: string) => void;
}

export const QuotasTabSection: React.FC<QuotasTabSectionProps> = ({
  tenants,
  selectedProjectId,
  onSelectProject,
  quota,
  quotaEtag,
  loading,
  saving,
  onLoadQuota,
  onSaveQuota,
  onConflict,
  onNotify,
}) => {
  const [maxRequestsPerMinute, setMaxRequestsPerMinute] = useState('120');
  const [maxConcurrentJobs, setMaxConcurrentJobs] = useState('5');
  const [maxBatchItems, setMaxBatchItems] = useState('100');
  const [maxStorageBytes, setMaxStorageBytes] = useState('1073741824');
  const [maxActiveApiKeys, setMaxActiveApiKeys] = useState('2');
  const [isActive, setIsActive] = useState(true);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (!selectedProjectId) return;
    setLoadError(null);
    onLoadQuota(selectedProjectId).catch((err) => {
      setLoadError(extractErrorMessage(err, 'ไม่สามารถโหลดข้อมูลโควตาของโปรเจกต์ได้'));
    });
  }, [onLoadQuota, selectedProjectId]);

  useEffect(() => {
    if (!quota) return;
    setMaxRequestsPerMinute(String(quota.maxRequestsPerMinute));
    setMaxConcurrentJobs(String(quota.maxConcurrentJobs));
    setMaxBatchItems(String(quota.maxBatchItems));
    setMaxStorageBytes(String(quota.maxStorageBytes));
    setMaxActiveApiKeys(String(quota.maxActiveApiKeys));
    setIsActive(quota.isActive);
    setFieldErrors({});
  }, [quota]);

  const reloadCurrentQuota = async () => {
    if (!selectedProjectId) return;
    await onLoadQuota(selectedProjectId);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedProjectId) return;

    const parsed = upsertProjectQuotaSchema.safeParse({
      maxRequestsPerMinute,
      maxConcurrentJobs,
      maxBatchItems,
      maxStorageBytes,
      maxActiveApiKeys,
      isActive,
    });

    if (!parsed.success) {
      setFieldErrors(toFieldErrorMap(parsed.error));
      return;
    }

    setFieldErrors({});
    try {
      await onSaveQuota(selectedProjectId, parsed.data, quotaEtag);
      onNotify('success', 'บันทึกค่าโควตาและขีดจำกัดของโปรเจกต์สำเร็จ');
    } catch (err) {
      if (onConflict(err, reloadCurrentQuota)) return;
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        setFieldErrors(err.fieldErrors);
      }
      onNotify('error', extractErrorMessage(err, 'บันทึกค่าโควตาไม่สำเร็จ'));
    }
  };

  const projectOptions =
    tenants.length > 0
      ? tenants.map((t) => ({ value: t.id, label: `${t.name} (${t.slug})` }))
      : [{ value: '', label: 'ไม่มีโปรเจกต์ในระบบ' }];

  return (
    <div className="space-y-4">
      <div className="flex items-end justify-between gap-3 flex-wrap">
        <div className="w-80">
          <Select
            label="เลือกโปรเจกต์ที่ต้องการตั้งค่าโควตา"
            value={selectedProjectId}
            options={projectOptions}
            onChange={(e) => onSelectProject(e.target.value)}
            disabled={tenants.length === 0}
          />
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          icon={RefreshCw}
          loading={loading}
          disabled={!selectedProjectId}
          onClick={() => void reloadCurrentQuota()}
        >
          โหลดโควตาล่าสุด
        </Button>
      </div>

      {loadError && (
        <div className="p-3 bg-red-50 border border-red-200 text-red-700 text-xs rounded-sm">
          {loadError}
        </div>
      )}

      {!selectedProjectId ? (
        <EmptyState
          icon={Gauge}
          title="ยังไม่ได้เลือกโปรเจกต์"
          description="กรุณาเลือกโปรเจกต์จากรายการด้านบนเพื่อดูและปรับแต่งค่าโควตา"
        />
      ) : (
        <CardBlock className="p-4 space-y-4">
          <div className="flex items-start justify-between gap-3 pb-3 border-b border-border">
            <div>
              <h3 className="text-xs font-bold text-textPrimary">
                ขีดจำกัดการใช้งานระดับเทแนนต์ (Tenant Operational Quotas)
              </h3>
              <p className="text-[11px] text-textMuted">
                การบันทึกจะใช้ If-Match ETag เพื่อป้องกันการเขียนทับข้อมูลพร้อมกันระหว่างผู้ดูแลระบบ
              </p>
            </div>
            {quotaEtag && (
              <span className="font-mono text-[10px] px-2 py-0.5 rounded-xs bg-slate-100 border border-border text-textSecondary shrink-0">
                ETag: {quotaEtag}
              </span>
            )}
          </div>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
              <Input
                label="Max Requests / นาที"
                type="number"
                min={1}
                value={maxRequestsPerMinute}
                onChange={(e) => setMaxRequestsPerMinute(e.target.value)}
                error={fieldErrors.maxRequestsPerMinute}
              />
              <Input
                label="Max Concurrent Jobs (Pipeline C)"
                type="number"
                min={1}
                value={maxConcurrentJobs}
                onChange={(e) => setMaxConcurrentJobs(e.target.value)}
                error={fieldErrors.maxConcurrentJobs}
              />
              <Input
                label="Max Batch Items / Job"
                type="number"
                min={1}
                value={maxBatchItems}
                onChange={(e) => setMaxBatchItems(e.target.value)}
                error={fieldErrors.maxBatchItems}
              />
              <Input
                label="Max Storage Bytes"
                type="number"
                min={1}
                value={maxStorageBytes}
                onChange={(e) => setMaxStorageBytes(e.target.value)}
                helperText={`ขนาดโดยประมาณ: ${formatBytes(Number(maxStorageBytes))}`}
                error={fieldErrors.maxStorageBytes}
              />
              <Input
                label="Max Active API Keys"
                type="number"
                min={1}
                value={maxActiveApiKeys}
                onChange={(e) => setMaxActiveApiKeys(e.target.value)}
                error={fieldErrors.maxActiveApiKeys}
              />
              <div className="flex items-end pb-1.5">
                <label className="inline-flex items-center gap-2 text-xs font-medium text-textPrimary cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={isActive}
                    onChange={(e) => setIsActive(e.target.checked)}
                    className="rounded-xs border-border text-primary focus:ring-primary"
                  />
                  เปิดบังคับใช้โควตา (Quota Active)
                </label>
              </div>
            </div>

            <div className="flex items-center justify-between pt-3 border-t border-border">
              <span className="text-[11px] text-textMuted font-mono">
                {quota?.updatedAt
                  ? `อัปเดตล่าสุด: ${formatThaiDateTime(quota.updatedAt)}`
                  : 'ใช้ค่าโควตาเริ่มต้นของระบบ (ยังไม่เคยกำหนดค่าเฉพาะ)'}
              </span>
              <Button type="submit" size="sm" icon={Save} loading={saving} disabled={loading}>
                บันทึกค่าโควตา
              </Button>
            </div>
          </form>
        </CardBlock>
      )}
    </div>
  );
};

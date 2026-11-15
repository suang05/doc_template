'use client';

import React, { useEffect, useState } from 'react';
import { ShieldAlert, Plus, RefreshCw, Ban } from 'lucide-react';
import {
  Badge,
  Button,
  ConfirmModal,
  Input,
  Modal,
  Select,
  Table,
  Column,
} from '@/components/ui';
import { ApiError, extractErrorMessage } from '@/lib/api/client';
import type {
  BreakGlassGrantDto,
  GrantBreakGlassAccessRequest,
  ProjectDto,
} from '@/lib/api/types';
import { useIdempotencyKey } from '@/hooks/useIdempotencyKey';
import { formatThaiDateTime } from '@/utils/format';
import { grantBreakGlassSchema } from '../schemas/project.schema';
import { toFieldErrorMap } from '@/utils/zod';

interface BreakGlassTabSectionProps {
  tenants: ProjectDto[];
  grants: BreakGlassGrantDto[];
  filterProjectId: string;
  onFilterProjectChange: (projectId: string) => void;
  loading: boolean;
  revokingGrantId: string | null;
  onLoadGrants: (projectId?: string) => Promise<void>;
  onIssueGrant: (
    request: GrantBreakGlassAccessRequest,
    idempotencyKey: string,
  ) => Promise<BreakGlassGrantDto>;
  onRevokeGrant: (grantId: string) => Promise<void>;
  onConflict: (err: unknown, reloadFn?: () => Promise<void>) => boolean;
  onNotify: (type: 'success' | 'error' | 'info', message: string) => void;
}

export const BreakGlassTabSection: React.FC<BreakGlassTabSectionProps> = ({
  tenants,
  grants,
  filterProjectId,
  onFilterProjectChange,
  loading,
  revokingGrantId,
  onLoadGrants,
  onIssueGrant,
  onRevokeGrant,
  onConflict,
  onNotify,
}) => {
  const [isGrantModalOpen, setIsGrantModalOpen] = useState(false);
  const [targetProjectId, setTargetProjectId] = useState('');
  const [superAdminUserId, setSuperAdminUserId] = useState('');
  const [durationMinutes, setDurationMinutes] = useState('60');
  const [reason, setReason] = useState('');
  const [grantErrors, setGrantErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [confirmRevokeGrant, setConfirmRevokeGrant] = useState<BreakGlassGrantDto | null>(null);
  const {
    getKey: getGrantIdempotencyKey,
    rotateKey: rotateGrantIdempotencyKey,
    resetKey: resetGrantIdempotencyKey,
  } = useIdempotencyKey();

  useEffect(() => {
    setLoadError(null);
    onLoadGrants(filterProjectId).catch((err) => {
      setLoadError(extractErrorMessage(err, 'โหลดรายการ Break-Glass Grants ไม่สำเร็จ'));
    });
  }, [filterProjectId, onLoadGrants]);

  const projectNameById = new Map(tenants.map((t) => [t.id.toLowerCase(), `${t.name} (${t.slug})`]));

  const openGrantModal = () => {
    setTargetProjectId(filterProjectId || tenants[0]?.id || '');
    setSuperAdminUserId('');
    setDurationMinutes('60');
    setReason('');
    setGrantErrors({});
    rotateGrantIdempotencyKey();
    setIsGrantModalOpen(true);
  };

  const handleGrantSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const parsed = grantBreakGlassSchema.safeParse({
      targetProjectId,
      reason,
      superAdminUserId: superAdminUserId.trim() || undefined,
      durationMinutes,
    });

    if (!parsed.success) {
      setGrantErrors(toFieldErrorMap(parsed.error));
      return;
    }

    setGrantErrors({});
    setSubmitting(true);
    const attemptKey = getGrantIdempotencyKey();

    try {
      await onIssueGrant(
        {
          targetProjectId: parsed.data.targetProjectId,
          reason: parsed.data.reason,
          superAdminUserId: parsed.data.superAdminUserId || null,
          durationMinutes: parsed.data.durationMinutes,
        },
        attemptKey,
      );
      setIsGrantModalOpen(false);
      resetGrantIdempotencyKey();
      onNotify('success', 'ออกสิทธิ์ Break-Glass Access แบบจำกัดเวลาสำเร็จ');
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        setGrantErrors(err.fieldErrors);
      }
      onNotify('error', extractErrorMessage(err, 'ออกสิทธิ์ Break-Glass Access ไม่สำเร็จ'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleConfirmRevoke = async () => {
    if (!confirmRevokeGrant) return;
    const target = confirmRevokeGrant;
    try {
      await onRevokeGrant(target.grantId);
      setConfirmRevokeGrant(null);
      onNotify('info', 'เพิกถอนสิทธิ์ Break-Glass เรียบร้อยแล้ว');
    } catch (err) {
      setConfirmRevokeGrant(null);
      if (onConflict(err, () => onLoadGrants(filterProjectId))) return;
      onNotify('error', extractErrorMessage(err, 'เพิกถอนสิทธิ์ Break-Glass ไม่สำเร็จ'));
    }
  };

  const filterOptions = [
    { value: '', label: 'ทุกโปรเจกต์' },
    ...tenants.map((t) => ({ value: t.id, label: `${t.name} (${t.slug})` })),
  ];

  const columns: Column<BreakGlassGrantDto>[] = [
    {
      header: 'โปรเจกต์เป้าหมาย',
      accessor: (item) => (
        <div className="space-y-0.5">
          <p className="text-xs font-semibold text-textPrimary">
            {projectNameById.get(item.targetProjectId.toLowerCase()) ?? item.targetProjectId}
          </p>
          <p className="text-[10px] font-mono text-textMuted">Grant: {item.grantId}</p>
        </div>
      ),
    },
    {
      header: 'ผู้ได้รับสิทธิ์ (SuperAdmin)',
      accessor: (item) => (
        <span className="text-[11px] font-mono text-textSecondary">{item.superAdminUserId}</span>
      ),
    },
    {
      header: 'เหตุผลประกอบ (Audit Reason)',
      accessor: (item) => <p className="text-xs text-textPrimary max-w-xs">{item.reason}</p>,
    },
    {
      header: 'ช่วงเวลาหมดอายุ',
      accessor: (item) => (
        <div className="text-[11px] font-mono text-textMuted space-y-0.5">
          <div>เริ่ม: {formatThaiDateTime(item.grantedAt)}</div>
          <div>หมดอายุ: {formatThaiDateTime(item.expiresAt)}</div>
        </div>
      ),
    },
    {
      header: 'สถานะ',
      accessor: (item) => (
        <Badge status={item.isActive ? 'pending' : 'idle'} dot size="sm">
          {item.isActive ? 'Active Grant' : item.revokedAt ? 'Revoked' : 'Expired'}
        </Badge>
      ),
      align: 'center',
    },
    {
      header: 'จัดการ',
      accessor: (item) =>
        item.isActive ? (
          <Button
            variant="danger"
            size="sm"
            icon={Ban}
            loading={revokingGrantId === item.grantId}
            onClick={() => setConfirmRevokeGrant(item)}
          >
            เพิกถอน
          </Button>
        ) : (
          <span className="text-[11px] text-textMuted">-</span>
        ),
      align: 'right',
    },
  ];

  return (
    <div className="space-y-3">
      <div className="p-3 rounded-sm bg-sky-50 border border-sky-200 text-sky-900 text-xs flex items-start gap-2">
        <ShieldAlert className="w-4 h-4 shrink-0 mt-0.5 text-sky-700" />
        <div>
          <p className="font-semibold">Zero-Standing-Access (ZSA) Break-Glass Governance</p>
          <p className="text-[11px] text-sky-800">
            SuperAdmin ไม่มีสิทธิ์เข้าถึงข้อมูลระดับโปรเจกต์โดยอัตโนมัติ ต้องออกสิทธิ์ชั่วคราวแบบระบุเหตุผลและบันทึก Audit
          </p>
        </div>
      </div>

      <div className="flex items-end justify-between gap-3 flex-wrap">
        <div className="w-72">
          <Select
            label="กรองตามโปรเจกต์"
            value={filterProjectId}
            options={filterOptions}
            onChange={(e) => onFilterProjectChange(e.target.value)}
          />
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            loading={loading}
            onClick={() => void onLoadGrants(filterProjectId)}
          >
            รีเฟรช
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={Plus}
            disabled={tenants.length === 0}
            onClick={openGrantModal}
          >
            ออกสิทธิ์ Break-Glass
          </Button>
        </div>
      </div>

      {loadError && (
        <div className="p-3 bg-red-50 border border-red-200 text-red-700 text-xs rounded-sm">
          {loadError}
        </div>
      )}

      <Table
        columns={columns}
        data={grants}
        keyExtractor={(item) => item.grantId}
        loading={loading}
        emptyMessage="ไม่มีรายการ Break-Glass Grant ในขณะนี้"
      />

      {/* Issue Break-Glass Modal */}
      <Modal
        isOpen={isGrantModalOpen}
        onClose={() => setIsGrantModalOpen(false)}
        title="ออกสิทธิ์ Break-Glass Access ชั่วคราว"
        maxWidth="md"
      >
        <form onSubmit={handleGrantSubmit} className="space-y-3">
          <Select
            label="โปรเจกต์เป้าหมาย"
            value={targetProjectId}
            options={tenants.map((t) => ({ value: t.id, label: `${t.name} (${t.slug})` }))}
            onChange={(e) => setTargetProjectId(e.target.value)}
            error={grantErrors.targetProjectId}
          />
          <div className="grid grid-cols-2 gap-3">
            <Input
              label="ระยะเวลา (นาที)"
              type="number"
              min={1}
              max={1440}
              value={durationMinutes}
              onChange={(e) => setDurationMinutes(e.target.value)}
              error={grantErrors.durationMinutes}
            />
            <Input
              label="SuperAdmin User ID (ไม่บังคับ)"
              value={superAdminUserId}
              onChange={(e) => setSuperAdminUserId(e.target.value)}
              placeholder="เว้นว่าง = ให้สิทธิ์ตัวเอง"
              error={grantErrors.superAdminUserId}
            />
          </div>
          <div className="space-y-1">
            <label className="block text-xs font-medium text-textSecondary">
              เหตุผลความจำเป็นในการเข้าถึง (Audit Justification)
            </label>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={3}
              placeholder="เช่น ตรวจสอบ Incident #INC-402 เรื่องเทมเพลตเรนเดอร์ไม่ผ่าน"
              className={`w-full p-2.5 rounded-sm text-xs bg-surface border focus:outline-none focus:ring-1 ${
                grantErrors.reason
                  ? 'border-red-500 focus:border-red-500 focus:ring-red-500'
                  : 'border-border focus:border-primary focus:ring-primary'
              }`}
            />
            {grantErrors.reason && (
              <p className="text-[11px] text-red-600">{grantErrors.reason}</p>
            )}
          </div>
          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setIsGrantModalOpen(false)}
            >
              ยกเลิก
            </Button>
            <Button type="submit" size="sm" loading={submitting}>
              ยืนยันออกสิทธิ์
            </Button>
          </div>
        </form>
      </Modal>

      <ConfirmModal
        isOpen={confirmRevokeGrant !== null}
        onClose={() => {
          if (revokingGrantId !== confirmRevokeGrant?.grantId) {
            setConfirmRevokeGrant(null);
          }
        }}
        onConfirm={handleConfirmRevoke}
        title="ยืนยันการเพิกถอนสิทธิ์ Break-Glass"
        message={
          <>
            คุณต้องการเพิกถอนสิทธิ์ Break-Glass Grant{' '}
            <code className="font-mono text-textPrimary">{confirmRevokeGrant?.grantId}</code>{' '}
            ทันทีใช่หรือไม่?
          </>
        }
        confirmLabel="เพิกถอนทันที"
        variant="danger"
        loading={confirmRevokeGrant !== null && revokingGrantId === confirmRevokeGrant.grantId}
      />
    </div>
  );
};

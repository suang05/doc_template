'use client';

import React, { useState, useRef } from 'react';
import {
  Plus,
  Search,
  Power,
  Network,
  UserCheck,
  Gauge,
  RefreshCw,
  Copy,
  Check,
  AlertTriangle,
} from 'lucide-react';
import {
  Button,
  Badge,
  Input,
  Modal,
  Pagination,
  Table,
  Column,
} from '@/components/ui';
import { ApiError, extractErrorMessage } from '@/lib/api/client';
import type { ProjectControlPlaneDto, ProjectDto, ResourceWithETag } from '@/lib/api/types';
import type { TenantsPaginationState } from '../hooks/useProjectsControlPlane';
import {
  createProjectSchema,
  parseAndValidateEgressCidrs,
  slugifyProjectName,
  toFieldErrorMap,
  transferProjectOwnerSchema,
} from '../schemas/project.schema';

interface TenantsTabSectionProps {
  tenants: ProjectDto[];
  search: string;
  onSearchChange: (value: string) => void;
  pagination: TenantsPaginationState;
  onPageChange: (page: number) => void;
  loading: boolean;
  error: string | null;
  mutatingProjectId: string | null;
  egressCidrsByProjectId: Record<string, string[]>;
  onRefresh: () => Promise<void>;
  onCreateProject: (
    request: { name: string; slug: string },
    idempotencyKey: string,
  ) => Promise<ProjectDto>;
  onFetchProjectWithETag: (projectId: string) => Promise<ResourceWithETag<ProjectDto>>;
  onToggleActive: (project: ProjectDto) => Promise<ProjectControlPlaneDto>;
  onUpdateEgressCidrs: (
    projectId: string,
    cidrs: string[],
    etag: string | null,
  ) => Promise<ProjectControlPlaneDto>;
  onTransferOwner: (
    projectId: string,
    newOwnerUserId: string,
    etag: string | null,
  ) => Promise<void>;
  onOpenQuotaTab: (projectId: string) => void;
  onConflict: (err: unknown, reloadFn?: () => Promise<void>) => boolean;
  onNotify: (type: 'success' | 'error' | 'info', message: string) => void;
}

export const TenantsTabSection: React.FC<TenantsTabSectionProps> = ({
  tenants,
  search,
  onSearchChange,
  pagination,
  onPageChange,
  loading,
  error,
  mutatingProjectId,
  egressCidrsByProjectId,
  onRefresh,
  onCreateProject,
  onFetchProjectWithETag,
  onToggleActive,
  onUpdateEgressCidrs,
  onTransferOwner,
  onOpenQuotaTab,
  onConflict,
  onNotify,
}) => {
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [createName, setCreateName] = useState('');
  const [createSlug, setCreateSlug] = useState('');
  const [slugTouched, setSlugTouched] = useState(false);
  const [createErrors, setCreateErrors] = useState<Record<string, string>>({});
  const [creating, setCreating] = useState(false);
  const [provisionedProject, setProvisionedProject] = useState<ProjectDto | null>(null);
  const [copiedKey, setCopiedKey] = useState<string | null>(null);
  const createIdempotencyRef = useRef<string | null>(null);

  const [egressTarget, setEgressTarget] = useState<ProjectDto | null>(null);
  const [egressEtag, setEgressEtag] = useState<string | null>(null);
  const [egressText, setEgressText] = useState('');
  const [egressError, setEgressError] = useState<string | null>(null);
  const [loadingEgress, setLoadingEgress] = useState(false);
  const [savingEgress, setSavingEgress] = useState(false);

  const [transferTarget, setTransferTarget] = useState<ProjectDto | null>(null);
  const [transferEtag, setTransferEtag] = useState<string | null>(null);
  const [newOwnerUserId, setNewOwnerUserId] = useState('');
  const [transferError, setTransferError] = useState<string | null>(null);
  const [transferring, setTransferring] = useState(false);

  const openCreateModal = () => {
    setCreateName('');
    setCreateSlug('');
    setSlugTouched(false);
    setCreateErrors({});
    createIdempotencyRef.current = crypto.randomUUID();
    setIsCreateOpen(true);
  };

  const handleNameInput = (value: string) => {
    setCreateName(value);
    if (!slugTouched) {
      setCreateSlug(slugifyProjectName(value));
    }
  };

  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const parsed = createProjectSchema.safeParse({ name: createName, slug: createSlug });
    if (!parsed.success) {
      setCreateErrors(toFieldErrorMap(parsed.error));
      return;
    }

    setCreateErrors({});
    setCreating(true);
    const attemptKey = createIdempotencyRef.current ?? crypto.randomUUID();
    createIdempotencyRef.current = attemptKey;

    try {
      const created = await onCreateProject(parsed.data, attemptKey);
      setIsCreateOpen(false);
      createIdempotencyRef.current = null;
      onNotify('success', `สร้างโปรเจกต์ "${created.name}" สำเร็จ`);
      if (created.readApiKey || created.writeApiKey) {
        setProvisionedProject(created);
      }
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        setCreateErrors(err.fieldErrors);
      }
      onNotify('error', extractErrorMessage(err, 'สร้างโปรเจกต์ไม่สำเร็จ'));
    } finally {
      setCreating(false);
    }
  };

  const handleToggleProject = async (project: ProjectDto) => {
    try {
      const updated = await onToggleActive(project);
      onNotify(
        updated.isActive ? 'success' : 'info',
        updated.isActive
          ? `เปิดใช้งานเทแนนต์ "${updated.name}" แล้ว`
          : `ระงับการใช้งานเทแนนต์ "${updated.name}" ชั่วคราวแล้ว`,
      );
    } catch (err) {
      if (onConflict(err, onRefresh)) return;
      onNotify('error', extractErrorMessage(err, 'เปลี่ยนสถานะเทแนนต์ไม่สำเร็จ'));
    }
  };

  const openEgressModal = async (project: ProjectDto) => {
    setEgressTarget(project);
    setEgressError(null);
    setLoadingEgress(true);
    const existing = egressCidrsByProjectId[project.id] ?? [];
    setEgressText(existing.join('\n'));
    try {
      const res = await onFetchProjectWithETag(project.id);
      setEgressTarget(res.data);
      setEgressEtag(res.etag);
    } catch (err) {
      setEgressError(extractErrorMessage(err, 'โหลด ETag ล่าสุดของโปรเจกต์ไม่สำเร็จ'));
    } finally {
      setLoadingEgress(false);
    }
  };

  const reloadEgressModal = async () => {
    if (!egressTarget) return;
    await openEgressModal(egressTarget);
  };

  const handleSaveEgress = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!egressTarget) return;

    const { cidrs, error: validationErr } = parseAndValidateEgressCidrs(egressText);
    if (validationErr) {
      setEgressError(validationErr);
      return;
    }

    setEgressError(null);
    setSavingEgress(true);
    try {
      await onUpdateEgressCidrs(egressTarget.id, cidrs, egressEtag);
      setEgressTarget(null);
      onNotify('success', `อัปเดต Outbound Egress CIDRs (${cidrs.length} รายการ) สำเร็จ`);
    } catch (err) {
      if (onConflict(err, reloadEgressModal)) return;
      const msg = extractErrorMessage(err, 'อัปเดต Egress CIDRs ไม่สำเร็จ');
      setEgressError(msg);
      onNotify('error', msg);
    } finally {
      setSavingEgress(false);
    }
  };

  const openTransferModal = async (project: ProjectDto) => {
    setTransferTarget(project);
    setNewOwnerUserId('');
    setTransferError(null);
    try {
      const res = await onFetchProjectWithETag(project.id);
      setTransferTarget(res.data);
      setTransferEtag(res.etag);
    } catch (err) {
      setTransferError(extractErrorMessage(err, 'โหลดข้อมูลโปรเจกต์ไม่สำเร็จ'));
    }
  };

  const reloadTransferModal = async () => {
    if (!transferTarget) return;
    await openTransferModal(transferTarget);
  };

  const handleTransferSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!transferTarget) return;
    const parsed = transferProjectOwnerSchema.safeParse({ newOwnerUserId });
    if (!parsed.success) {
      setTransferError(parsed.error.issues[0]?.message ?? 'รูปแบบ UUID ไม่ถูกต้อง');
      return;
    }

    setTransferError(null);
    setTransferring(true);
    try {
      await onTransferOwner(transferTarget.id, parsed.data.newOwnerUserId, transferEtag);
      setTransferTarget(null);
      onNotify('success', 'โอนสิทธิ์ Master Owner ของโปรเจกต์สำเร็จ');
    } catch (err) {
      if (onConflict(err, reloadTransferModal)) return;
      const msg = extractErrorMessage(err, 'โอนสิทธิ์เจ้าของโปรเจกต์ไม่สำเร็จ');
      setTransferError(msg);
      onNotify('error', msg);
    } finally {
      setTransferring(false);
    }
  };

  const copySecret = async (label: string, value: string) => {
    await navigator.clipboard.writeText(value);
    setCopiedKey(label);
    setTimeout(() => setCopiedKey(null), 1800);
  };

  const columns: Column<ProjectDto>[] = [
    {
      header: 'โปรเจกต์ / เทแนนต์',
      accessor: (item) => (
        <div className="space-y-0.5">
          <p className="text-xs font-semibold text-textPrimary">{item.name}</p>
          <p className="text-[10px] font-mono text-textMuted">{item.id}</p>
        </div>
      ),
    },
    {
      header: 'Slug',
      accessor: (item) => (
        <span className="font-mono text-[11px] px-2 py-0.5 rounded-xs bg-slate-100 border border-slate-200 text-textSecondary">
          {item.slug}
        </span>
      ),
    },
    {
      header: 'สถานะ',
      accessor: (item) => (
        <Badge status={item.isActive ? 'success' : 'failed'} dot size="sm">
          {item.isActive ? 'Active' : 'Suspended'}
        </Badge>
      ),
      align: 'center',
    },
    {
      header: 'Outbound Egress CIDRs',
      accessor: (item) => {
        const cidrs = egressCidrsByProjectId[item.id];
        if (!cidrs) {
          return <span className="text-[11px] text-textMuted">กดตั้งค่าเพื่อแก้ไข</span>;
        }
        if (cidrs.length === 0) {
          return <span className="text-[11px] text-textMuted">ไม่ได้ระบุ CIDR</span>;
        }
        return (
          <div className="flex flex-wrap gap-1">
            {cidrs.slice(0, 3).map((cidr) => (
              <span
                key={cidr}
                className="font-mono text-[10px] px-1.5 py-0.5 rounded-xs bg-sky-50 text-sky-800 border border-sky-200"
              >
                {cidr}
              </span>
            ))}
            {cidrs.length > 3 && (
              <span className="text-[10px] text-textMuted">+{cidrs.length - 3}</span>
            )}
          </div>
        );
      },
    },
    {
      header: 'สร้างเมื่อ',
      accessor: (item) => (
        <span className="text-[11px] font-mono text-textMuted">
          {new Date(item.createdAt).toLocaleString('th-TH')}
        </span>
      ),
    },
    {
      header: 'จัดการ',
      accessor: (item) => (
        <div className="flex items-center justify-end gap-1 flex-wrap">
          <Button
            variant="ghost"
            size="sm"
            icon={Gauge}
            onClick={() => onOpenQuotaTab(item.id)}
          >
            โควตา
          </Button>
          <Button
            variant="ghost"
            size="sm"
            icon={Network}
            onClick={() => void openEgressModal(item)}
          >
            Egress CIDR
          </Button>
          <Button
            variant="ghost"
            size="sm"
            icon={UserCheck}
            onClick={() => void openTransferModal(item)}
          >
            โอน Owner
          </Button>
          <Button
            variant={item.isActive ? 'outline' : 'success'}
            size="sm"
            icon={Power}
            loading={mutatingProjectId === item.id}
            onClick={() => void handleToggleProject(item)}
          >
            {item.isActive ? 'ระงับ' : 'เปิดใช้'}
          </Button>
        </div>
      ),
      align: 'right',
    },
  ];

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-2 flex-wrap">
        <div className="w-72">
          <Input
            icon={Search}
            placeholder="ค้นหาชื่อโปรเจกต์หรือ slug..."
            value={search}
            onChange={(e) => {
              onSearchChange(e.target.value);
              onPageChange(1);
            }}
          />
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            loading={loading}
            onClick={() => void onRefresh()}
          >
            รีเฟรช
          </Button>
          <Button variant="primary" size="sm" icon={Plus} onClick={openCreateModal}>
            สร้างโปรเจกต์ใหม่
          </Button>
        </div>
      </div>

      {error && (
        <div className="p-3 bg-red-50 border border-red-200 text-red-700 text-xs rounded-sm">
          {error}
        </div>
      )}

      <Table
        columns={columns}
        data={tenants}
        keyExtractor={(item) => item.id}
        loading={loading}
        emptyMessage="ไม่พบโปรเจกต์ในระบบ Control Plane"
      />

      {pagination.totalCount > 0 && (
        <Pagination
          currentPage={pagination.page}
          totalItems={pagination.totalCount}
          pageSize={pagination.pageSize}
          onPageChange={onPageChange}
        />
      )}

      {/* Create Project Modal */}
      <Modal
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        title="สร้างโปรเจกต์เทแนนต์ใหม่ (Idempotent)"
        maxWidth="md"
      >
        <form onSubmit={handleCreateSubmit} className="space-y-3">
          <Input
            label="ชื่อโปรเจกต์"
            value={createName}
            onChange={(e) => handleNameInput(e.target.value)}
            placeholder="เช่น Claims Document Hub"
            error={createErrors.name}
            autoFocus
          />
          <Input
            label="Project Slug"
            value={createSlug}
            onChange={(e) => {
              setSlugTouched(true);
              setCreateSlug(e.target.value);
            }}
            placeholder="เช่น claims-doc-hub"
            helperText="ใช้ตัวอักษรพิมพ์เล็ก ตัวเลข และขีดกลาง (-) เท่านั้น"
            error={createErrors.slug}
          />
          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setIsCreateOpen(false)}
            >
              ยกเลิก
            </Button>
            <Button type="submit" size="sm" loading={creating}>
              สร้างโปรเจกต์
            </Button>
          </div>
        </form>
      </Modal>

      {/* Initial API Keys Secret Modal */}
      <Modal
        isOpen={provisionedProject !== null}
        onClose={() => setProvisionedProject(null)}
        title="บันทึก Initial API Keys ของโปรเจกต์ใหม่"
        maxWidth="lg"
      >
        {provisionedProject && (
          <div className="space-y-3">
            <div className="p-2.5 rounded-sm bg-amber-50 border border-amber-200 text-amber-800 text-xs">
              โปรดคัดลอก API Keys ด้านล่างเก็บไว้ทันที ระบบจะไม่แสดง Plaintext Key นี้อีกหลังปิดหน้าต่าง
            </div>
            {provisionedProject.readApiKey && (
              <div className="space-y-1">
                <span className="text-[11px] font-semibold text-textSecondary">
                  Read-Only API Key
                </span>
                <div className="flex items-center gap-2">
                  <code className="flex-1 p-2 bg-zinc-900 text-emerald-300 font-mono text-[11px] rounded-sm break-all">
                    {provisionedProject.readApiKey}
                  </code>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    icon={copiedKey === 'read' ? Check : Copy}
                    onClick={() => void copySecret('read', provisionedProject.readApiKey ?? '')}
                  >
                    คัดลอก
                  </Button>
                </div>
              </div>
            )}
            {provisionedProject.writeApiKey && (
              <div className="space-y-1">
                <span className="text-[11px] font-semibold text-textSecondary">
                  Read-Write API Key
                </span>
                <div className="flex items-center gap-2">
                  <code className="flex-1 p-2 bg-zinc-900 text-amber-300 font-mono text-[11px] rounded-sm break-all">
                    {provisionedProject.writeApiKey}
                  </code>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    icon={copiedKey === 'write' ? Check : Copy}
                    onClick={() => void copySecret('write', provisionedProject.writeApiKey ?? '')}
                  >
                    คัดลอก
                  </Button>
                </div>
              </div>
            )}
            <div className="flex justify-end pt-2 border-t border-border">
              <Button size="sm" onClick={() => setProvisionedProject(null)}>
                เรียบร้อย
              </Button>
            </div>
          </div>
        )}
      </Modal>

      {/* Egress CIDRs Modal */}
      <Modal
        isOpen={egressTarget !== null}
        onClose={() => setEgressTarget(null)}
        title={`ตั้งค่า Outbound Egress CIDRs — ${egressTarget?.name ?? ''}`}
        maxWidth="md"
      >
        <form onSubmit={handleSaveEgress} className="space-y-3">
          <div className="flex items-center justify-between text-[11px] text-textMuted">
            <span>ระบุ CIDR ที่อนุญาตให้ Data Connection หรือ Webhook เชื่อมต่อออก</span>
            {egressEtag && (
              <span className="font-mono px-1.5 py-0.5 rounded-xs bg-slate-100 border border-border">
                ETag: {egressEtag}
              </span>
            )}
          </div>
          <textarea
            value={egressText}
            onChange={(e) => setEgressText(e.target.value)}
            disabled={loadingEgress}
            rows={5}
            placeholder={'10.20.0.0/16\n192.168.100.0/24'}
            className="w-full p-2.5 rounded-sm font-mono text-xs bg-surface border border-border focus:outline-none focus:border-primary"
          />
          <p className="text-[11px] text-textMuted">
            แยกแต่ละ CIDR ด้วยการขึ้นบรรทัดใหม่ หรือเครื่องหมายจุลภาค (เว้นว่างเพื่อล้างรายการ CIDR)
          </p>
          {egressError && (
            <div className="p-2 bg-red-50 border border-red-200 text-red-700 text-xs rounded-sm">
              {egressError}
            </div>
          )}
          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setEgressTarget(null)}
            >
              ยกเลิก
            </Button>
            <Button type="submit" size="sm" loading={savingEgress || loadingEgress}>
              บันทึก Egress CIDRs
            </Button>
          </div>
        </form>
      </Modal>

      {/* Transfer Owner Modal */}
      <Modal
        isOpen={transferTarget !== null}
        onClose={() => setTransferTarget(null)}
        title={`โอนสิทธิ์เจ้าของโปรเจกต์ — ${transferTarget?.name ?? ''}`}
        maxWidth="md"
      >
        <form onSubmit={handleTransferSubmit} className="space-y-3">
          <div className="p-2.5 rounded-sm bg-amber-50 border border-amber-200 text-amber-800 text-xs flex items-start gap-2">
            <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
            <span>
              การโอนสิทธิ์ Master Owner จะเปลี่ยนผู้ถืออำนาจสูงสุดของโปรเจกต์นี้ทันที (ควบคุมด้วย
              If-Match ETag: <code className="font-mono">{transferEtag ?? '-'}</code>)
            </span>
          </div>
          <Input
            label="New Owner User ID (UUID)"
            value={newOwnerUserId}
            onChange={(e) => setNewOwnerUserId(e.target.value)}
            placeholder="00000000-0000-0000-0000-000000000000"
            error={transferError ?? undefined}
          />
          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setTransferTarget(null)}
            >
              ยกเลิก
            </Button>
            <Button type="submit" variant="danger" size="sm" loading={transferring}>
              ยืนยันการโอนสิทธิ์
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
};

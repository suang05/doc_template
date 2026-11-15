'use client';

import React, { useState, useCallback, useRef } from 'react';
import { Key, Plus, Trash2, Copy, AlertTriangle, CheckCircle } from 'lucide-react';
import {
  Button,
  Badge,
  Input,
  Select,
  Table,
  Column,
  Modal,
  EmptyState,
  PageHeader,
  ConflictBanner,
  Toast,
} from '@/components/ui';
import { ApiError, extractErrorMessage } from '@/lib/api/client';
import type { ApiKeyDto } from '@/lib/api/types';
import { apiKeyScopes, type ApiKeyScope } from '@/constants/domain';
import { useConflictRecovery } from '@/hooks/useConflictRecovery';
import { apiKeysApi } from '../api/apikeys.api';
import { useApiKeys } from '../hooks/useApiKeys';

const SCOPE_OPTIONS = apiKeyScopes.map((scope) => ({
  value: scope,
  label:
    scope === 'ReadOnly'
      ? 'ReadOnly (อ่านข้อมูลและดูประวัติอย่างเดียว)'
      : 'ReadWrite (สร้างเอกสารและอ่านข้อมูล)',
}));

export const ApiKeysView: React.FC = () => {
  const {
    keys,
    loading,
    createKey,
    revokeKey,
    newlyCreatedKey,
    setNewlyCreatedKey,
    refresh,
  } = useApiKeys();
  const { conflict, captureConflict, clearConflict } = useConflictRecovery();

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [name, setName] = useState('');
  const [callerApp, setCallerApp] = useState('');
  const [scope, setScope] = useState<ApiKeyScope>('ReadWrite');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [copied, setCopied] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(
    null,
  );
  const idempotencyKeyRef = useRef<string | null>(null);

  const openCreateModal = () => {
    setFieldErrors({});
    idempotencyKeyRef.current = crypto.randomUUID();
    setIsCreateOpen(true);
  };

  const closeCreateModal = () => {
    setIsCreateOpen(false);
    setFieldErrors({});
    idempotencyKeyRef.current = null;
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !callerApp.trim()) {
      setToast({ type: 'error', message: 'กรุณากรอกชื่อคีย์และ Caller App ให้ครบถ้วน' });
      return;
    }

    setIsSubmitting(true);
    setFieldErrors({});
    const attemptKey = idempotencyKeyRef.current ?? crypto.randomUUID();
    idempotencyKeyRef.current = attemptKey;

    try {
      await createKey(
        {
          name: name.trim(),
          callerApp: callerApp.trim(),
          scope,
          effectiveScope: scope,
        },
        attemptKey,
      );
      closeCreateModal();
      setName('');
      setCallerApp('');
      setScope('ReadWrite');
      setToast({ type: 'success', message: 'สร้าง API Key สำเร็จ' });
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        setFieldErrors(err.fieldErrors);
      }
      setToast({ type: 'error', message: extractErrorMessage(err, 'สร้าง API Key ล้มเหลว') });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleRevoke = async (item: ApiKeyDto) => {
    if (!confirm(`ต้องการเพิกถอนสิทธิ์ API Key "${item.name}" หรือไม่?`)) return;
    clearConflict();
    try {
      const latest = await apiKeysApi.getWithETag(item.id);
      await revokeKey(item.id, latest.etag);
      setToast({ type: 'success', message: `เพิกถอน API Key "${item.name}" สำเร็จ` });
    } catch (err) {
      if (captureConflict(err)) return;
      setToast({ type: 'error', message: extractErrorMessage(err, 'เพิกถอน API Key ล้มเหลว') });
    }
  };

  const handleReloadConflict = useCallback(async () => {
    clearConflict();
    await refresh();
    setToast({ type: 'info', message: 'โหลดรายการ API Keys ล่าสุดแล้ว' });
  }, [clearConflict, refresh]);

  const plainKeyToCopy = newlyCreatedKey?.plainTextKey || newlyCreatedKey?.key || '';

  const handleCopyKey = () => {
    if (!plainKeyToCopy) return;
    void navigator.clipboard.writeText(plainKeyToCopy);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const columns: Column<ApiKeyDto>[] = [
    {
      header: 'ชื่อคีย์ (Name)',
      accessor: (item) => (
        <div className="space-y-0.5">
          <span className="font-semibold text-textPrimary">{item.name}</span>
          {item.maskedKey && (
            <p className="text-[11px] font-mono text-textSecondary">{item.maskedKey}</p>
          )}
          <p className="text-[10px] font-mono text-textMuted">{item.id}</p>
        </div>
      ),
    },
    {
      header: 'แอปพลิเคชัน (Caller App)',
      accessor: (item) => (
        <span className="font-mono text-sky-700 bg-sky-50 px-2 py-0.5 rounded-sm border border-sky-200">
          {item.callerApp}
        </span>
      ),
      align: 'center',
    },
    {
      header: 'ขอบเขตสิทธิ์ (Scope)',
      accessor: (item) => (
        <span className="font-mono text-[11px] px-2 py-0.5 rounded-sm bg-slate-100 text-slate-700 border border-slate-200">
          {item.scope || 'ReadWrite'}
        </span>
      ),
      align: 'center',
    },
    {
      header: 'สถานะ',
      accessor: (item) => (
        <Badge status={item.isActive ? 'success' : 'failed'} dot size="sm">
          {item.isActive ? 'เปิดใช้งาน' : 'ถูกเพิกถอน'}
        </Badge>
      ),
      align: 'center',
    },
    {
      header: 'ใช้งานล่าสุด',
      accessor: (item) => (
        <span className="text-[11px] font-mono text-textMuted">
          {item.lastUsedAt ? new Date(item.lastUsedAt).toLocaleString('th-TH') : 'ยังไม่มีการใช้งาน'}
        </span>
      ),
    },
    {
      header: 'วันที่สร้าง',
      accessor: (item) => (
        <span className="text-[11px] font-mono text-textMuted">
          {new Date(item.createdAt).toLocaleDateString('th-TH')}
        </span>
      ),
    },
    {
      header: 'จัดการ',
      accessor: (item) => (
        <Button
          variant="ghost"
          size="sm"
          disabled={!item.isActive}
          icon={Trash2}
          onClick={() => void handleRevoke(item)}
          className="text-red-600 hover:text-red-700 hover:bg-red-50"
        >
          เพิกถอน
        </Button>
      ),
      align: 'right',
    },
  ];

  return (
    <div className="space-y-4">
      {toast && <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} />}

      <PageHeader
        title="การจัดการกุญแจเชื่อมต่อระบบ (API Keys Management)"
        description="สร้างและควบคุมการเข้าถึงระบบเอกสารสำหรับแอปพลิเคชันภายนอก (ผ่าน Header: X-API-Key)"
        icon={Key}
        iconColor="amber"
        rightSlot={
          <Button variant="primary" icon={Plus} onClick={openCreateModal}>
            สร้าง API Key
          </Button>
        }
      />

      {conflict && (
        <ConflictBanner
          conflict={conflict}
          onReload={handleReloadConflict}
          onDismiss={clearConflict}
        />
      )}

      {keys.length === 0 && !loading ? (
        <EmptyState
          icon={Key}
          title="ยังไม่มี API Key"
          description="สร้าง API Key เพื่อเปิดให้ระบบ เช่น Sales CRM, SAP, หรือ Portal อื่นเรียกสร้างเอกสาร"
          actionLabel="สร้างคีย์แรก"
          onAction={openCreateModal}
        />
      ) : (
        <Table columns={columns} data={keys} keyExtractor={(k) => k.id} loading={loading} />
      )}

      <Modal
        isOpen={isCreateOpen}
        onClose={closeCreateModal}
        title="สร้าง API Key ใหม่"
        footer={
          <>
            <Button variant="ghost" onClick={closeCreateModal}>
              ยกเลิก
            </Button>
            <Button variant="primary" onClick={handleCreate} loading={isSubmitting}>
              สร้างคีย์
            </Button>
          </>
        }
      >
        <form onSubmit={handleCreate} className="space-y-3">
          <Input
            label="ชื่อเรียกของคีย์ (Key Name)"
            placeholder="เช่น CRM Production Service"
            value={name}
            onChange={(e) => setName(e.target.value)}
            error={fieldErrors.name}
            required
          />

          <Input
            label="รหัสระบบต้นทาง (Caller App ID)"
            placeholder="เช่น erp-system, crm-portal, hr-service"
            value={callerApp}
            onChange={(e) => setCallerApp(e.target.value)}
            error={fieldErrors.callerApp}
            helperText="จะถูกบันทึกใน Audit Log ทุกครั้งที่มีการเรียกใช้"
            required
          />

          <Select
            label="ขอบเขตสิทธิ์ (API Scope)"
            value={scope}
            onChange={(e) => setScope(e.target.value as ApiKeyScope)}
            options={SCOPE_OPTIONS}
            error={fieldErrors.scope}
          />
        </form>
      </Modal>

      <Modal
        isOpen={!!newlyCreatedKey}
        onClose={() => setNewlyCreatedKey(null)}
        title="กุญแจ API Key สร้างสำเร็จ (จัดเก็บคีย์นี้ทันที)"
        maxWidth="md"
        footer={
          <Button variant="primary" onClick={() => setNewlyCreatedKey(null)}>
            รับทราบและปิดหน้านี้
          </Button>
        }
      >
        <div className="space-y-3">
          <div className="p-3 bg-amber-50 border border-amber-200 rounded-sm text-amber-900 text-xs flex items-start gap-2">
            <AlertTriangle className="w-4 h-4 shrink-0 text-amber-600 mt-0.5" />
            <p className="leading-relaxed">
              <strong>คำเตือน:</strong> คีย์นี้จะแสดงเพียงครั้งเดียวเท่านั้น
              ระบบจะเข้ารหัส SHA-256 ในฐานข้อมูลและไม่สามารถดึงคืนได้อีก
            </p>
          </div>

          <div className="space-y-1">
            <label className="text-xs font-semibold text-textPrimary">Plain-Text API Key:</label>
            <div className="flex items-center gap-2 p-2 bg-slate-900 text-emerald-400 font-mono text-xs rounded-sm border border-slate-800">
              <span className="flex-1 select-all break-all">{plainKeyToCopy}</span>
              <button
                type="button"
                onClick={handleCopyKey}
                className="p-1.5 rounded-sm bg-slate-800 text-slate-200 hover:text-white transition-colors cursor-pointer"
                aria-label="คัดลอก API Key"
                title="คัดลอกคีย์"
              >
                {copied ? (
                  <CheckCircle className="w-4 h-4 text-emerald-400" />
                ) : (
                  <Copy className="w-4 h-4" />
                )}
              </button>
            </div>
          </div>
        </div>
      </Modal>
    </div>
  );
};

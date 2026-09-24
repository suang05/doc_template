'use client';

import React, { useState } from 'react';
import {
  Key,
  Plus,
  Trash2,
  Copy,
  AlertTriangle,
  CheckCircle,
} from 'lucide-react';
import { useApiKeys } from '@/hooks/useApiKeys';
import {
  Button,
  Badge,
  Input,
  Toolbar,
  Table,
  Column,
  Modal,
  EmptyState,
  PageHeader,
} from '@/components/ui';
import { ApiKeyItem } from '@/types/api';

export const ApiKeysView: React.FC = () => {
  const {
    keys,
    loading,
    createKey,
    revokeKey,
    newlyCreatedKey,
    setNewlyCreatedKey,
  } = useApiKeys();

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [name, setName] = useState('');
  const [callerApp, setCallerApp] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [copied, setCopied] = useState(false);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name || !callerApp) {
      alert('กรุณากรอกข้อมูลให้ครบถ้วน');
      return;
    }

    setIsSubmitting(true);
    try {
      await createKey(name, callerApp);
      setIsCreateOpen(false);
      setName('');
      setCallerApp('');
    } catch (err: any) {
      alert(err.message || 'สร้าง API Key ล้มเหลว');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCopyKey = () => {
    if (!newlyCreatedKey) return;
    navigator.clipboard.writeText(newlyCreatedKey.key);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const columns: Column<ApiKeyItem>[] = [
    {
      header: 'ชื่อคีย์ (Name)',
      accessor: (item) => (
        <div className="space-y-0.5">
          <span className="font-semibold text-textPrimary">{item.name}</span>
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
          onClick={() => {
            if (confirm(`ต้องการเพิกถอนสิทธิ์ API Key "${item.name}" หรือไม่?`)) {
              revokeKey(item.id);
            }
          }}
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
      {/* Header Info */}
      <PageHeader
        title="การจัดการกุญแจเชื่อมต่อระบบ (API Keys Management)"
        description="สร้างและควบคุมการเข้าถึงระบบเอกสารสำหรับแอปพลิเคชันภายนอก (ผ่าน Header: X-API-Key)"
        icon={Key}
        iconColor="amber"
        rightSlot={
          <Button
            variant="primary"
            icon={Plus}
            onClick={() => setIsCreateOpen(true)}
          >
            สร้าง API Key
          </Button>
        }
      />

      {/* Table */}
      {keys.length === 0 && !loading ? (
        <EmptyState
          icon={Key}
          title="ยังไม่มี API Key"
          description="สร้าง API Key เพื่อเปิดให้ระบบ เช่น Sales CRM, SAP, หรือ Portal อื่นเรียกสร้างเอกสาร"
          actionLabel="สร้างคีย์แรก"
          onAction={() => setIsCreateOpen(true)}
        />
      ) : (
        <Table
          columns={columns}
          data={keys}
          keyExtractor={(k) => k.id}
          loading={loading}
        />
      )}

      {/* Create Modal */}
      <Modal
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
        title="สร้าง API Key ใหม่"
        footer={
          <>
            <Button variant="ghost" onClick={() => setIsCreateOpen(false)}>
              ยกเลิก
            </Button>
            <Button
              variant="primary"
              onClick={handleCreate}
              loading={isSubmitting}
            >
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
            required
          />

          <Input
            label="รหัสระบบต้นทาง (Caller App ID)"
            placeholder="เช่น erp-system, crm-portal, hr-service"
            value={callerApp}
            onChange={(e) => setCallerApp(e.target.value)}
            helperText="จะถูกบันทึกใน Audit Log ทุกครั้งที่มีการเรียกใช้"
            required
          />
        </form>
      </Modal>

      {/* Key Created Modal (Shows PlainTextKey ONCE) */}
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
              <span className="flex-1 select-all break-all">{newlyCreatedKey?.key}</span>
              <button
                onClick={handleCopyKey}
                className="p-1.5 rounded-sm bg-slate-800 text-slate-200 hover:text-white transition-colors cursor-pointer"
                title="คัดลอกคีย์"
              >
                {copied ? <CheckCircle className="w-4 h-4 text-emerald-400" /> : <Copy className="w-4 h-4" />}
              </button>
            </div>
          </div>
        </div>
      </Modal>
    </div>
  );
};

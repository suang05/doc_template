'use client';

import React, { useState } from 'react';
import {
  Search,
  History,
  Download,
  FileCheck,
  Eye,
} from 'lucide-react';
import { useLegalAudit } from '../hooks/useLegalAudit';
import {
  Button,
  Input,
  Toolbar,
  Table,
  Column,
  EmptyState,
  PageHeader,
  Modal,
} from '@/components/ui';
import { DocumentVersionDto } from '@/types/api';

export const AuditView: React.FC = () => {
  const {
    documentRef,
    setDocumentRef,
    versions,
    loading,
    error,
    fetchVersions,
    downloadVersion,
    downloadingVersion,
  } = useLegalAudit();

  const [inspectVersion, setInspectVersion] = useState<DocumentVersionDto | null>(null);

  const handleSearch = (e?: React.FormEvent, customRef?: string) => {
    if (e) e.preventDefault();
    const query = customRef || documentRef;
    if (customRef) setDocumentRef(customRef);
    fetchVersions(query);
  };

  const columns: Column<DocumentVersionDto>[] = [
    {
      header: 'เวอร์ชัน',
      accessor: (item) => (
        <span className="font-mono font-bold text-sky-700 bg-sky-50 px-2 py-0.5 rounded-sm border border-sky-200">
          v{item.version}
        </span>
      ),
      align: 'center',
    },
    {
      header: 'บันทึกการแก้ไข (Change Note)',
      accessor: (item) => (
        <span className="text-xs text-textPrimary truncate max-w-xs block">
          {item.changeNote || 'ไม่มีบันทึก'}
        </span>
      ),
    },
    {
      header: 'เวอร์ชันแม่แบบ',
      accessor: (item) => (
        <span className="text-[11px] font-mono text-textMuted">
          {item.templateVersionId ? item.templateVersionId.slice(0, 8) + '…' : '-'}
        </span>
      ),
      align: 'center',
    },
    {
      header: 'วัน-เวลาที่สร้าง',
      accessor: (item) => (
        <span className="text-[11px] text-textMuted font-mono">
          {new Date(item.createdAt).toLocaleString('th-TH')}
        </span>
      ),
    },
    {
      header: 'การจัดการ',
      accessor: (item) => (
        <div className="flex items-center justify-end gap-1.5">
          <Button
            variant="ghost"
            size="sm"
            icon={Eye}
            onClick={() => setInspectVersion(item)}
          >
            ตรวจสอบ
          </Button>
          <Button
            variant="outline"
            size="sm"
            icon={Download}
            loading={downloadingVersion === item.version}
            onClick={() => downloadVersion(item.documentRef, item.version)}
          >
            ดาวน์โหลด
          </Button>
        </div>
      ),
      align: 'right',
    },
  ];

  return (
    <div className="space-y-4">
      {/* Header Info */}
      <PageHeader
        title="ประวัติการจัดทำเอกสารทางกฎหมาย (Legal Audit Trail)"
        description="ตรวจสอบย้อนหลังทุกเวอร์ชันของเอกสารสัญญาตามเลขอ้างอิง (Document Ref)"
        icon={FileCheck}
        iconColor="indigo"
      />

      {/* Toolbar Search with Presets */}
      <Toolbar
        leftSlot={
          <div className="flex items-center gap-3 flex-wrap w-full">
            <form onSubmit={(e) => handleSearch(e)} className="flex items-center gap-2 w-full max-w-md">
              <Input
                placeholder="ระบุเลขอ้างอิง เช่น DOC-2026-0001..."
                icon={Search}
                value={documentRef}
                onChange={(e) => setDocumentRef(e.target.value)}
              />
              <Button
                variant="primary"
                size="md"
                type="submit"
                loading={loading}
                icon={Search}
              >
                ค้นหา
              </Button>
            </form>

            <div className="flex items-center gap-1.5 text-xs">
              <span className="text-textMuted text-[11px]">ตัวอย่างค้นหา:</span>
              <button
                type="button"
                onClick={() => handleSearch(undefined, 'DOC-2026-0001')}
                className="px-2 py-0.5 bg-surfaceSubtle hover:bg-slate-200 text-textSecondary text-[11px] font-mono rounded-xs border border-border transition-colors cursor-pointer"
              >
                DOC-2026-0001
              </button>
              <button
                type="button"
                onClick={() => handleSearch(undefined, 'CONDO-2026-001')}
                className="px-2 py-0.5 bg-surfaceSubtle hover:bg-slate-200 text-textSecondary text-[11px] font-mono rounded-xs border border-border transition-colors cursor-pointer"
              >
                CONDO-2026-001
              </button>
            </div>
          </div>
        }
      />

      {/* Table Result */}
      {error ? (
        <div className="p-4 bg-amber-50 border border-amber-200 text-amber-800 text-xs rounded-sm">
          {error}
        </div>
      ) : versions.length > 0 ? (
        <div className="space-y-2">
          <div className="flex items-center justify-between px-1 text-xs text-textMuted">
            <span>
              พบ <strong className="text-textPrimary">{versions.length}</strong> เวอร์ชันสำหรับเอกสาร{' '}
              <span className="font-mono text-primary font-semibold">{documentRef}</span>
            </span>
          </div>

          <Table
            columns={columns}
            data={versions}
            keyExtractor={(v) => v.id}
            loading={loading}
          />
        </div>
      ) : (
        <EmptyState
          icon={History}
          title="พร้อมค้นหาประวัติเอกสาร"
          description="กรอกเลขอ้างอิงเอกสาร (Document Ref) ด้านบน เพื่อดึงประวัติการแก้ไขและดาวน์โหลดไฟล์ย้อนหลัง"
        />
      )}

      {/* Inspection Modal */}
      {inspectVersion && (
        <Modal
          isOpen={Boolean(inspectVersion)}
          onClose={() => setInspectVersion(null)}
          title={`รายละเอียดเอกสาร ${inspectVersion.documentRef} (v${inspectVersion.version})`}
          maxWidth="lg"
        >
          <div className="space-y-4">
            <div className="p-3 bg-surfaceSubtle border border-border rounded-sm flex items-center justify-between text-xs">
              <div>
                <span className="text-textMuted text-[11px] block">เวอร์ชันเอกสาร</span>
                <span className="font-mono font-bold text-sky-800 text-sm">v{inspectVersion.version}</span>
              </div>
              <div className="text-right">
                <span className="text-textMuted text-[11px] block">วัน-เวลาที่จัดทำ</span>
                <span className="font-mono text-textPrimary text-xs">
                  {new Date(inspectVersion.createdAt).toLocaleString('th-TH')}
                </span>
              </div>
            </div>

            <div className="space-y-1">
              <label className="text-xs font-semibold text-textPrimary block">
                บันทึกการแก้ไขทางกฎหมาย (Legal Revision Note):
              </label>
              <div className="p-3 bg-slate-50 border border-border rounded-sm text-xs text-textSecondary font-sans leading-relaxed">
                {inspectVersion.changeNote || 'ไม่มีบันทึกรายละเอียดเพิ่มเติมในการสร้างเวอร์ชันนี้'}
              </div>
            </div>

            <div className="space-y-1">
              <label className="text-xs font-semibold text-textPrimary block">
                รหัสอ้างอิงแม่แบบ (Template Version ID):
              </label>
              <p className="font-mono text-xs bg-surfaceSubtle p-2 rounded-sm border border-border text-textMuted select-all">
                {inspectVersion.templateVersionId || '-'}
              </p>
            </div>

            <div className="pt-3 border-t border-border flex items-center justify-between">
              <Button variant="ghost" onClick={() => setInspectVersion(null)}>
                ปิดหน้าต่าง
              </Button>
              <Button
                variant="primary"
                icon={Download}
                loading={downloadingVersion === inspectVersion.version}
                onClick={() => downloadVersion(inspectVersion.documentRef, inspectVersion.version)}
              >
                ดาวน์โหลดเอกสารต้นฉบับ
              </Button>
            </div>
          </div>
        </Modal>
      )}

    </div>
  );
};

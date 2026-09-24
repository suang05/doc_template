'use client';

import React from 'react';
import {
  Search,
  History,
  Download,
  FileCheck,
} from 'lucide-react';
import { useLegalAudit } from '@/hooks/useLegalAudit';
import {
  Button,
  Badge,
  Input,
  Toolbar,
  Table,
  Column,
  EmptyState,
  PageHeader,
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

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    fetchVersions(documentRef);
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

      {/* Toolbar Search */}
      <Toolbar
        leftSlot={
          <form onSubmit={handleSearch} className="flex items-center gap-2 w-full max-w-md">
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

    </div>
  );
};

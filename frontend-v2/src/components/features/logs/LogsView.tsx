'use client';

import React, { useState } from 'react';
import {
  ListOrdered,
  Search,
  CheckCircle,
  AlertCircle,
  Eye,
  Clock,
  Download,
} from 'lucide-react';
import { useGenerationLogs } from '@/hooks/useGenerationLogs';
import { documentsApi } from '@/lib/api/documents.api';
import {
  Badge,
  Button,
  Input,
  Toolbar,
  Table,
  Column,
  Pagination,
  Modal,
  CodeBlock,
  EmptyState,
  PageHeader,
} from '@/components/ui';
import { GenerationLogDto, DocumentFormat } from '@/types/api';

export const LogsView: React.FC = () => {
  const {
    logs,
    total,
    page,
    setPage,
    limit,
    appFilter,
    setAppFilter,
    loading,
    refresh,
  } = useGenerationLogs();

  const [inspectPayload, setInspectPayload] = useState<string | null>(null);
  const [selectedError, setSelectedError] = useState<string | null>(null);
  const [downloadingLogId, setDownloadingLogId] = useState<string | null>(null);

  const handleDownload = async (logId: string) => {
    setDownloadingLogId(logId);
    try {
      const { url } = await documentsApi.getDownloadUrlByLogId(logId);
      window.open(url, '_blank', 'noopener,noreferrer');
    } catch {
      // silently fail — no toast available in this component
    } finally {
      setDownloadingLogId(null);
    }
  };

  const columns: Column<GenerationLogDto>[] = [
    {
      header: 'สถานะ',
      accessor: (item) => (
        <Badge
          status={item.status === 'success' ? 'success' : 'failed'}
          dot
          size="sm"
        >
          {item.status === 'success' ? 'สำเร็จ' : 'ล้มเหลว'}
        </Badge>
      ),
      align: 'center',
    },
    {
      header: 'แอปพลิเคชัน (Caller App)',
      accessor: (item) => (
        <span className="font-mono text-xs text-sky-700 bg-sky-50 px-2 py-0.5 rounded-sm border border-sky-200">
          {item.callerApp || 'direct-portal'}
        </span>
      ),
      align: 'center',
    },
    {
      header: 'รูปแบบ',
      accessor: (item) => (
        <Badge
          format={(item.outputFormat?.toLowerCase() || 'pdf') as DocumentFormat}
          size="sm"
        >
          {(item.outputFormat || 'pdf').toUpperCase()}
        </Badge>
      ),
      align: 'center',
    },
    {
      header: 'ระยะเวลาประมวลผล',
      accessor: (item) => (
        <span className="text-[11px] font-mono text-textMuted flex items-center gap-1">
          <Clock className="w-3 h-3" />
          <span>{item.durationMs || 0} ms</span>
        </span>
      ),
      align: 'center',
    },
    {
      header: 'วัน-เวลาที่สร้าง',
      accessor: (item) => (
        <span className="text-[11px] font-mono text-textMuted">
          {new Date(item.createdAt).toLocaleString('th-TH')}
        </span>
      ),
    },
    {
      header: 'รายละเอียด',
      accessor: (item) => (
        <div className="flex items-center justify-end gap-1">
          {item.inputData && (
            <Button
              variant="ghost"
              size="sm"
              icon={Eye}
              onClick={() => setInspectPayload(item.inputData || '')}
            >
              Data
            </Button>
          )}

          {item.status === 'success' && item.outputKey && (
            <Button
              variant="outline"
              size="sm"
              icon={Download}
              loading={downloadingLogId === item.id}
              onClick={() => handleDownload(item.id)}
            >
              ดาวน์โหลด
            </Button>
          )}

          {item.errorMsg && (
            <Button
              variant="ghost"
              size="sm"
              icon={AlertCircle}
              onClick={() => setSelectedError(item.errorMsg || '')}
              className="text-red-600 hover:text-red-700 hover:bg-red-50"
            >
              Error
            </Button>
          )}
        </div>
      ),
      align: 'right',
    },
  ];

  return (
    <div className="space-y-4">
      {/* Header Info */}
      <PageHeader
        title="ประวัติการเรียกสร้างเอกสารทั้งหมด (System Generation Logs)"
        description="บันทึกการส่งคำขอแบบเรียลไทม์จากระบบภายนอก พร้อมระยะเวลา Duration และ Error Diagnosis"
        icon={ListOrdered}
        iconColor="slate"
      />

      {/* Toolbar Filter */}
      <Toolbar
        leftSlot={
          <div className="w-64">
            <Input
              placeholder="กรองตาม Caller App (เช่น sales-app)..."
              icon={Search}
              value={appFilter}
              onChange={(e) => {
                setAppFilter(e.target.value);
                setPage(1);
              }}
            />
          </div>
        }
        rightSlot={
          <Button variant="outline" size="sm" onClick={refresh}>
            รีเฟรช
          </Button>
        }
      />

      {/* Logs Table */}
      {logs.length === 0 && !loading ? (
        <EmptyState
          icon={ListOrdered}
          title="ไม่พบประวัติการสร้างเอกสาร"
          description="เมื่อมีคำขอสร้างเอกสารผ่าน API ระบบจะบันทึกประวัติการทำงานไว้ที่นี่โดยอัตโนมัติ"
        />
      ) : (
        <div className="space-y-2">
          <Table
            columns={columns}
            data={logs}
            keyExtractor={(l) => l.id}
            loading={loading}
          />

          <Pagination
            currentPage={page}
            totalItems={total}
            pageSize={limit}
            onPageChange={setPage}
          />
        </div>
      )}

      {/* Inspect Input Payload Modal */}
      <Modal
        isOpen={!!inspectPayload}
        onClose={() => setInspectPayload(null)}
        title="ชุดข้อมูลที่ส่งเข้ามา (Request Payload Snapshot)"
        maxWidth="lg"
      >
        <div className="space-y-2">
          {inspectPayload && (
            <CodeBlock
              code={(() => {
                try {
                  return JSON.stringify(JSON.parse(inspectPayload), null, 2);
                } catch {
                  return inspectPayload;
                }
              })()}
              language="json"
            />
          )}
        </div>
      </Modal>

      {/* Error Details Modal */}
      <Modal
        isOpen={!!selectedError}
        onClose={() => setSelectedError(null)}
        title="รายละเอียดข้อผิดพลาด (Error Trace)"
        maxWidth="md"
      >
        <div className="p-3 bg-red-50 border border-red-200 rounded-sm text-red-900 text-xs font-mono whitespace-pre-wrap leading-relaxed">
          {selectedError}
        </div>
      </Modal>
    </div>
  );
};

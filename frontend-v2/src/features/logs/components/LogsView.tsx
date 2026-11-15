'use client';

import React, { useState } from 'react';
import { ListOrdered, Search, AlertCircle, Eye, Clock, Download } from 'lucide-react';
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
  Toast,
} from '@/components/ui';
import { documentsApi } from '@/features/generator';
import { extractErrorMessage } from '@/lib/api/client';
import type { GenerationLogDto, LogPayloadResultDto } from '@/lib/api/types';
import { isSuccessGenerationStatus } from '@/constants/domain';
import type { DocumentFormat } from '@/tokens';
import { logsApi } from '../api/logs.api';
import { useGenerationLogs } from '../hooks/useGenerationLogs';

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

  const [inspectPayload, setInspectPayload] = useState<LogPayloadResultDto | null>(null);
  const [loadingPayloadId, setLoadingPayloadId] = useState<string | null>(null);
  const [selectedError, setSelectedError] = useState<string | null>(null);
  const [downloadingLogId, setDownloadingLogId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(
    null,
  );

  const handleInspectPayload = async (logId: string) => {
    setLoadingPayloadId(logId);
    try {
      const payload = await logsApi.getPayload(logId);
      setInspectPayload(payload);
    } catch (err) {
      setToast({
        type: 'error',
        message: extractErrorMessage(err, 'ไม่สามารถโหลดข้อมูล Payload ได้'),
      });
    } finally {
      setLoadingPayloadId(null);
    }
  };

  const handleDownload = async (logId: string) => {
    setDownloadingLogId(logId);
    try {
      const { url } = await documentsApi.getDownloadUrlByLogId(logId);
      window.open(url, '_blank', 'noopener,noreferrer');
    } catch (err) {
      setToast({
        type: 'error',
        message: extractErrorMessage(err, 'ไม่สามารถดึงลิงก์ดาวน์โหลดได้'),
      });
    } finally {
      setDownloadingLogId(null);
    }
  };

  const columns: Column<GenerationLogDto>[] = [
    {
      header: 'สถานะ',
      accessor: (item) => {
        const ok = isSuccessGenerationStatus(item.status);
        return (
          <Badge status={ok ? 'success' : 'failed'} dot size="sm">
            {ok ? 'สำเร็จ' : item.status || 'ล้มเหลว'}
          </Badge>
        );
      },
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
        <Badge format={(item.outputFormat?.toLowerCase() || 'pdf') as DocumentFormat} size="sm">
          {(item.outputFormat || 'pdf').toUpperCase()}
        </Badge>
      ),
      align: 'center',
    },
    {
      header: 'ระยะเวลาประมวลผล',
      accessor: (item) => (
        <span className="text-[11px] font-mono text-textMuted flex items-center gap-1 justify-center">
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
          <Button
            variant="ghost"
            size="sm"
            icon={Eye}
            loading={loadingPayloadId === item.id}
            onClick={() => void handleInspectPayload(item.id)}
          >
            Payload
          </Button>

          {isSuccessGenerationStatus(item.status) && (
            <Button
              variant="outline"
              size="sm"
              icon={Download}
              loading={downloadingLogId === item.id}
              onClick={() => void handleDownload(item.id)}
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
      {toast && <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} />}

      <PageHeader
        title="ประวัติการเรียกสร้างเอกสารทั้งหมด (System Generation Logs)"
        description="บันทึกการส่งคำขอแบบเรียลไทม์จากระบบภายนอก พร้อมระยะเวลา Duration และ Error Diagnosis"
        icon={ListOrdered}
        iconColor="slate"
      />

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
          <Button variant="outline" size="sm" onClick={() => void refresh()}>
            รีเฟรช
          </Button>
        }
      />

      {logs.length === 0 && !loading ? (
        <EmptyState
          icon={ListOrdered}
          title="ไม่พบประวัติการสร้างเอกสาร"
          description="เมื่อมีคำขอสร้างเอกสารผ่าน API ระบบจะบันทึกประวัติการทำงานไว้ที่นี่โดยอัตโนมัติ"
        />
      ) : (
        <div className="space-y-2">
          <Table columns={columns} data={logs} keyExtractor={(l) => l.id} loading={loading} />

          <Pagination
            currentPage={page}
            totalItems={total}
            pageSize={limit}
            onPageChange={setPage}
          />
        </div>
      )}

      <Modal
        isOpen={!!inspectPayload}
        onClose={() => setInspectPayload(null)}
        title="ชุดข้อมูลที่ส่งเข้ามา (Request Payload Snapshot)"
        maxWidth="lg"
      >
        <div className="space-y-2">
          {inspectPayload?.isOffloaded && (
            <p className="text-[11px] font-mono text-amber-700 bg-amber-50 border border-amber-200 px-2.5 py-1.5 rounded-sm">
              Offloaded payload resolved from object storage
            </p>
          )}
          {inspectPayload?.payloadJson ? (
            <CodeBlock
              code={(() => {
                try {
                  return JSON.stringify(JSON.parse(inspectPayload.payloadJson), null, 2);
                } catch {
                  return inspectPayload.payloadJson;
                }
              })()}
              language="json"
            />
          ) : (
            <p className="text-xs text-textMuted py-4 text-center">
              ไม่พบข้อมูล Payload สำหรับรายการนี้
            </p>
          )}
        </div>
      </Modal>

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

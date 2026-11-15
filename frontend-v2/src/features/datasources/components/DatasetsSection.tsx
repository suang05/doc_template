'use client';

import React, { useState, useEffect, useCallback } from 'react';
import dynamic from 'next/dynamic';
import {
  Table2,
  Plus,
  Trash2,
  Edit2,
  Clock,
  Code2,
  Play,
  CheckCircle2,
  XCircle,
} from 'lucide-react';
import {
  Button,
  CardBlock,
  ConfirmModal,
  Input,
  Select,
  Modal,
  Table,
  EmptyState,
  ConflictBanner,
  Toast,
} from '@/components/ui';
import { ApiError, extractErrorMessage } from '@/lib/api/client';
import type {
  CreateDatasetRequest,
  DataConnectionDto,
  DatasetDto,
  DryRunDatasetResultDto,
} from '@/lib/api/types';
import { useConflictRecovery } from '@/hooks/useConflictRecovery';
import { datasetsApi } from '../api/datasets.api';
import { useDatasets } from '../hooks/useDatasets';

const Editor = dynamic(
  () => import('@monaco-editor/react').then((mod) => mod.Editor),
  { ssr: false },
);

interface DatasetModalState {
  name: string;
  description: string;
  dataConnectionId: string;
  sqlQuery: string;
  cacheSeconds: number;
  dryRunParamsJson: string;
}

const emptyDatasetForm = (): DatasetModalState => ({
  name: '',
  description: '',
  dataConnectionId: '',
  sqlQuery: '',
  cacheSeconds: 0,
  dryRunParamsJson: '{}',
});

interface DatasetsSectionProps {
  connections: DataConnectionDto[];
}

function formatCellValue(value: unknown): string {
  if (value === null || value === undefined) return '—';
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}

export const DatasetsSection: React.FC<DatasetsSectionProps> = ({ connections }) => {
  const {
    datasets,
    isLoading,
    fetchDatasets,
    createDataset,
    updateDataset,
    deleteDataset,
    dryRunDataset,
  } = useDatasets();
  const { conflict, captureConflict, clearConflict } = useConflictRecovery();

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingDataset, setEditingDataset] = useState<DatasetDto | null>(null);
  const [editingEtag, setEditingEtag] = useState<string | null>(null);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [form, setForm] = useState<DatasetModalState>(emptyDatasetForm());
  const [isSaving, setIsSaving] = useState(false);
  const [isDryRunning, setIsDryRunning] = useState(false);
  const [dryRunResult, setDryRunResult] = useState<DryRunDatasetResultDto | null>(null);
  const [dryRunLocalError, setDryRunLocalError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [deleteTarget, setDeleteTarget] = useState<DatasetDto | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(
    null,
  );

  useEffect(() => {
    void fetchDatasets();
  }, [fetchDatasets]);

  const openModal = async (dataset?: DatasetDto) => {
    clearConflict();
    setFieldErrors({});
    setDryRunResult(null);
    setDryRunLocalError(null);

    if (dataset) {
      setEditingDataset(dataset);
      setEditingEtag(null);
      setForm({
        name: dataset.name,
        description: dataset.description ?? '',
        dataConnectionId: dataset.dataConnectionId,
        sqlQuery: dataset.sqlQuery,
        cacheSeconds: dataset.cacheSeconds,
        dryRunParamsJson: '{}',
      });
      setIsModalOpen(true);
      setIsLoadingDetail(true);
      try {
        const latest = await datasetsApi.getWithETag(dataset.id);
        setEditingDataset(latest.data);
        setEditingEtag(latest.etag);
        setForm({
          name: latest.data.name,
          description: latest.data.description ?? '',
          dataConnectionId: latest.data.dataConnectionId,
          sqlQuery: latest.data.sqlQuery,
          cacheSeconds: latest.data.cacheSeconds,
          dryRunParamsJson: '{}',
        });
      } catch (err) {
        setToast({ type: 'error', message: extractErrorMessage(err) });
      } finally {
        setIsLoadingDetail(false);
      }
    } else {
      setEditingDataset(null);
      setEditingEtag(null);
      setForm({ ...emptyDatasetForm(), dataConnectionId: connections[0]?.id ?? '' });
      setIsModalOpen(true);
    }
  };

  const closeModal = () => {
    setIsModalOpen(false);
    setEditingDataset(null);
    setEditingEtag(null);
    setDryRunResult(null);
    setDryRunLocalError(null);
    setFieldErrors({});
    clearConflict();
  };

  const handleReloadAfterConflict = useCallback(async () => {
    clearConflict();
    await fetchDatasets();
    if (editingDataset) {
      try {
        const latest = await datasetsApi.getWithETag(editingDataset.id);
        setEditingDataset(latest.data);
        setEditingEtag(latest.etag);
        setForm((prev) => ({
          ...prev,
          name: latest.data.name,
          description: latest.data.description ?? '',
          dataConnectionId: latest.data.dataConnectionId,
          sqlQuery: latest.data.sqlQuery,
          cacheSeconds: latest.data.cacheSeconds,
        }));
        setToast({ type: 'info', message: 'โหลดข้อมูล Dataset ล่าสุดแล้ว' });
      } catch (err) {
        setToast({ type: 'error', message: extractErrorMessage(err) });
      }
    }
  }, [clearConflict, editingDataset, fetchDatasets]);

  const handleSave = async () => {
    if (!form.name.trim() || !form.dataConnectionId || !form.sqlQuery.trim()) return;
    setIsSaving(true);
    setFieldErrors({});
    try {
      const payload: CreateDatasetRequest = {
        name: form.name.trim(),
        description: form.description.trim() || null,
        dataConnectionId: form.dataConnectionId,
        sqlQuery: form.sqlQuery.trim(),
        cacheSeconds: form.cacheSeconds,
      };
      if (editingDataset) {
        await updateDataset(editingDataset.id, payload, editingEtag);
        setToast({ type: 'success', message: 'อัปเดต Dataset สำเร็จ' });
      } else {
        await createDataset(payload);
        setToast({ type: 'success', message: 'สร้าง Dataset สำเร็จ' });
      }
      closeModal();
    } catch (err) {
      if (captureConflict(err)) return;
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        setFieldErrors(err.fieldErrors);
      }
      setToast({ type: 'error', message: extractErrorMessage(err) });
    } finally {
      setIsSaving(false);
    }
  };

  const handleDryRun = async () => {
    if (!form.dataConnectionId || !form.sqlQuery.trim()) return;
    setDryRunLocalError(null);
    setDryRunResult(null);

    let parsedParams: Record<string, unknown> | null = null;
    const rawParams = form.dryRunParamsJson.trim();
    if (rawParams && rawParams !== '{}') {
      try {
        const parsed: unknown = JSON.parse(rawParams);
        if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
          setDryRunLocalError('Parameters สำหรับ Dry-Run ต้องเป็น JSON Object เช่น {"ref": "DOC-001"}');
          return;
        }
        parsedParams = parsed as Record<string, unknown>;
      } catch {
        setDryRunLocalError('รูปแบบ JSON ของ Parameters ไม่ถูกต้อง');
        return;
      }
    }

    setIsDryRunning(true);
    try {
      const result = await dryRunDataset({
        dataConnectionId: form.dataConnectionId,
        provider: null,
        connectionString: null,
        sqlQuery: form.sqlQuery.trim(),
        parameters: parsedParams,
        maxSampleRows: 5,
      });
      setDryRunResult(result);
    } catch (err) {
      setDryRunLocalError(extractErrorMessage(err, 'ไม่สามารถทดสอบ SQL Query ได้'));
    } finally {
      setIsDryRunning(false);
    }
  };

  const executeDelete = async () => {
    if (!deleteTarget) return;
    const row = deleteTarget;
    setIsDeleting(true);
    clearConflict();
    try {
      const latest = await datasetsApi.getWithETag(row.id);
      await deleteDataset(row.id, latest.etag);
      setDeleteTarget(null);
      setToast({ type: 'success', message: `ลบ Dataset "${row.name}" สำเร็จ` });
    } catch (err) {
      setDeleteTarget(null);
      if (captureConflict(err)) return;
      setToast({ type: 'error', message: extractErrorMessage(err) });
    } finally {
      setIsDeleting(false);
    }
  };

  const setField = <K extends keyof DatasetModalState>(key: K, val: DatasetModalState[K]) =>
    setForm((prev) => ({ ...prev, [key]: val }));

  const connectionOptions = connections.map((c) => ({ value: c.id, label: c.name }));
  const detectedParams = Array.from(new Set(form.sqlQuery.match(/[@:][a-zA-Z0-9_]+/g) || []));
  const dryRunSampleRows = (dryRunResult?.sampleRows ?? []) as Array<Record<string, unknown>>;

  const columns = [
    {
      header: 'ชื่อ Dataset',
      accessor: (row: DatasetDto) => (
        <div>
          <p className="text-xs font-medium text-textPrimary">{row.name}</p>
          {row.description && (
            <p className="text-[11px] text-textMuted truncate max-w-[200px]">{row.description}</p>
          )}
        </div>
      ),
    },
    {
      header: 'DataConnection',
      accessor: (row: DatasetDto) => (
        <span className="text-xs text-textSecondary">{row.dataConnectionName}</span>
      ),
    },
    {
      header: 'SQL Query',
      accessor: (row: DatasetDto) => (
        <code className="text-[11px] text-textMuted font-mono truncate block max-w-[280px]">
          {row.sqlQuery}
        </code>
      ),
    },
    {
      header: 'Cache',
      accessor: (row: DatasetDto) => (
        <span className="flex items-center gap-1 text-[11px] text-textMuted">
          <Clock className="w-3 h-3" />
          {row.cacheSeconds > 0 ? `${row.cacheSeconds}s` : 'ไม่ cache'}
        </span>
      ),
    },
    {
      header: 'จัดการ',
      accessor: (row: DatasetDto) => (
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => void openModal(row)}
            aria-label={`แก้ไข ${row.name}`}
          >
            <Edit2 className="w-4 h-4" />
          </Button>
          <Button
            variant="danger"
            size="sm"
            onClick={() => setDeleteTarget(row)}
            aria-label={`ลบ ${row.name}`}
          >
            <Trash2 className="w-4 h-4" />
          </Button>
        </div>
      ),
    },
  ];

  const canSave = form.name.trim() && form.dataConnectionId && form.sqlQuery.trim();

  return (
    <>
      {toast && <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} />}

      {!isModalOpen && conflict && (
        <ConflictBanner conflict={conflict} onReload={handleReloadAfterConflict} onDismiss={clearConflict} />
      )}

      <div className="flex items-center justify-between pb-3 border-b border-border">
        <span className="text-xs text-textMuted">
          Dataset คือ SQL query ที่ตั้งชื่อไว้ — FieldMapping แบบ SQL เลือก Dataset แทนการเขียน query ซ้ำ
        </span>
        <Button onClick={() => void openModal()} size="sm" className="gap-2" disabled={connections.length === 0}>
          <Plus className="w-4 h-4" />
          เพิ่ม Dataset
        </Button>
      </div>

      {connections.length === 0 && (
        <div className="p-3 rounded-sm bg-amber-50 border border-amber-200 text-xs text-amber-800">
          ต้องเพิ่ม DataConnection ก่อนจึงจะสร้าง Dataset ได้ — ไปที่แท็บ &quot;DataConnections&quot;
        </div>
      )}

      <CardBlock>
        {datasets.length === 0 && !isLoading ? (
          <EmptyState
            icon={Table2}
            title="ยังไม่มี Dataset"
            description="สร้าง Dataset เพื่อ reuse SQL query ใน FieldMapping หลายตัวโดยไม่ต้องเขียนซ้ำ"
            actionLabel="เพิ่ม Dataset"
            onAction={() => void openModal()}
          />
        ) : (
          <Table
            columns={columns}
            data={datasets}
            loading={isLoading}
            keyExtractor={(row: DatasetDto) => row.id}
            emptyMessage="ไม่พบ Dataset"
          />
        )}
      </CardBlock>

      <Modal
        isOpen={isModalOpen}
        onClose={closeModal}
        title={editingDataset ? 'แก้ไข Dataset' : 'เพิ่ม Dataset ใหม่'}
        maxWidth="2xl"
      >
        <div className="space-y-4">
          {conflict && (
            <ConflictBanner
              conflict={conflict}
              onReload={handleReloadAfterConflict}
              onDismiss={clearConflict}
            />
          )}

          <Input
            label="ชื่อ Dataset"
            value={form.name}
            onChange={(e) => setField('name', e.target.value)}
            placeholder="เช่น ราคาสัญญา, ข้อมูลลูกค้า"
            error={fieldErrors.name}
            required
          />
          <Input
            label="คำอธิบาย (ไม่บังคับ)"
            value={form.description}
            onChange={(e) => setField('description', e.target.value)}
            placeholder="อธิบายว่า Dataset นี้ดึงข้อมูลอะไร"
            error={fieldErrors.description}
          />
          <Select
            label="DataConnection"
            value={form.dataConnectionId}
            onChange={(e) => setField('dataConnectionId', e.target.value)}
            options={connectionOptions}
            error={fieldErrors.dataConnectionId}
          />

          <div className="space-y-1">
            <div className="flex items-center justify-between">
              <label className="text-xs font-medium text-textSecondary flex items-center gap-1.5">
                <Code2 className="w-3.5 h-3.5 text-primary" />
                <span>SQL Query (Monaco SQL Mode)</span>
                <span className="text-red-500">*</span>
              </label>
              <span className="text-[10px] text-textMuted font-mono">PostgreSQL / Oracle / SQL Server</span>
            </div>

            <div className="h-44 border border-border rounded-sm overflow-hidden bg-zinc-950">
              <Editor
                language="sql"
                theme="vs-dark"
                value={form.sqlQuery}
                onChange={(val) => setField('sqlQuery', val || '')}
                options={{
                  minimap: { enabled: false },
                  fontSize: 12,
                  scrollBeyondLastLine: false,
                  wordWrap: 'on',
                  lineNumbers: 'on',
                }}
              />
            </div>

            {fieldErrors.sqlQuery && (
              <p className="text-[11px] text-red-600">{fieldErrors.sqlQuery}</p>
            )}

            {detectedParams.length > 0 && (
              <div className="space-y-2 pt-1">
                <div className="flex items-center gap-1.5 flex-wrap">
                  <span className="text-[10px] text-textMuted uppercase font-mono">ตัวแปรที่พบ:</span>
                  {detectedParams.map((param) => (
                    <span
                      key={param}
                      className="px-1.5 py-0.2 bg-sky-50 text-sky-700 text-[10px] font-mono border border-sky-200 rounded-xs"
                    >
                      {param}
                    </span>
                  ))}
                </div>
                <Input
                  label="Sample Parameters JSON (สำหรับ Dry-Run)"
                  value={form.dryRunParamsJson}
                  onChange={(e) => setField('dryRunParamsJson', e.target.value)}
                  placeholder='เช่น {"ref": "DOC-001"}'
                />
              </div>
            )}

            <p className="text-[10px] text-textMuted pt-0.5">
              ใช้ <code className="bg-slate-100 px-1 rounded">@variableName</code> หรือ{' '}
              <code className="bg-slate-100 px-1 rounded">:param</code> เพื่อรับค่าจาก request payload เช่น{' '}
              <code className="bg-slate-100 px-1 rounded">@ref</code>
            </p>
          </div>

          <Input
            label="Cache (วินาที)"
            type="number"
            value={form.cacheSeconds}
            onChange={(e) => setField('cacheSeconds', Math.max(0, Number(e.target.value)))}
            placeholder="0 = ไม่ cache"
            helperText="ตั้งค่า cache เพื่อลดการยิง query ซ้ำต่อการสร้างเอกสาร 1 ครั้ง"
          />

          {(dryRunLocalError || dryRunResult) && (
            <div className="space-y-2 rounded-sm border border-border bg-slate-50 p-3 text-xs">
              {dryRunLocalError ? (
                <div className="flex items-start gap-2 text-red-700">
                  <XCircle className="w-4 h-4 shrink-0 mt-0.5" />
                  <span>{dryRunLocalError}</span>
                </div>
              ) : dryRunResult ? (
                <>
                  <div
                    className={`flex items-center justify-between gap-2 ${
                      dryRunResult.success ? 'text-emerald-800' : 'text-red-700'
                    }`}
                  >
                    <div className="flex items-center gap-1.5 font-medium">
                      {dryRunResult.success ? (
                        <CheckCircle2 className="w-4 h-4 shrink-0" />
                      ) : (
                        <XCircle className="w-4 h-4 shrink-0" />
                      )}
                      <span>
                        {dryRunResult.success
                          ? `Dry-Run สำเร็จ (${dryRunResult.sampleCount} แถว, ${dryRunResult.executionTimeMs} ms)`
                          : dryRunResult.error || 'Dry-Run ไม่สำเร็จ'}
                      </span>
                    </div>
                  </div>

                  {dryRunResult.columns.length > 0 && (
                    <div className="flex flex-wrap gap-1.5">
                      {dryRunResult.columns.map((col) => (
                        <span
                          key={col.name}
                          className="rounded-xs border border-slate-200 bg-white px-1.5 py-0.5 font-mono text-[10px] text-textSecondary"
                        >
                          {col.name}: {col.dataType}
                        </span>
                      ))}
                    </div>
                  )}

                  {dryRunSampleRows.length > 0 && dryRunResult.columns.length > 0 && (
                    <div className="max-h-40 overflow-auto rounded-xs border border-border bg-white">
                      <table className="min-w-full border-collapse text-[11px]">
                        <thead className="bg-slate-100 text-textSecondary">
                          <tr>
                            {dryRunResult.columns.map((col) => (
                              <th
                                key={col.name}
                                className="border-b border-border px-2 py-1 text-left font-mono font-medium"
                              >
                                {col.name}
                              </th>
                            ))}
                          </tr>
                        </thead>
                        <tbody>
                          {dryRunSampleRows.map((row, idx) => (
                            <tr key={idx} className="border-b border-border last:border-b-0">
                              {dryRunResult.columns.map((col) => (
                                <td key={col.name} className="px-2 py-1 font-mono text-textPrimary whitespace-nowrap">
                                  {formatCellValue(row[col.name])}
                                </td>
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </>
              ) : null}
            </div>
          )}

          <div className="pt-4 flex items-center justify-between gap-2 border-t border-border">
            <Button
              type="button"
              variant="secondary"
              onClick={() => void handleDryRun()}
              loading={isDryRunning}
              disabled={!form.dataConnectionId || !form.sqlQuery.trim()}
              icon={Play}
            >
              ทดสอบ Query (Dry-Run)
            </Button>
            <div className="flex gap-2">
              <Button variant="ghost" onClick={closeModal}>
                ยกเลิก
              </Button>
              <Button
                onClick={() => void handleSave()}
                loading={isSaving || isLoadingDetail}
                disabled={!canSave || isSaving}
              >
                บันทึก
              </Button>
            </div>
          </div>
        </div>
      </Modal>

      <ConfirmModal
        isOpen={deleteTarget !== null}
        onClose={() => {
          if (!isDeleting) setDeleteTarget(null);
        }}
        onConfirm={executeDelete}
        title="ยืนยันการลบ Dataset"
        message={
          <>
            คุณแน่ใจหรือไม่ว่าต้องการลบ Dataset{' '}
            <strong className="text-textPrimary">&ldquo;{deleteTarget?.name}&rdquo;</strong>?
            FieldMapping ที่ใช้อยู่จะสูญเสียข้อมูล SQL
          </>
        }
        confirmLabel="ลบ Dataset"
        variant="danger"
        loading={isDeleting}
      />
    </>
  );
};

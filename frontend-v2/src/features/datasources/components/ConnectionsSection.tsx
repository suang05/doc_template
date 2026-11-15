'use client';

import React, { useState, useEffect, useCallback } from 'react';
import {
  Database,
  Trash2,
  Edit2,
  Play,
  CheckCircle2,
  XCircle,
  Plus,
  Activity,
} from 'lucide-react';
import {
  Button,
  CardBlock,
  Input,
  Select,
  Modal,
  Table,
  EmptyState,
  ConflictBanner,
  Toast,
} from '@/components/ui';
import { ApiError, extractErrorMessage } from '@/lib/api/client';
import type { DataConnectionDto } from '@/lib/api/types';
import { databaseProviders, type DatabaseProvider } from '@/constants/domain';
import { useConflictRecovery } from '@/hooks/useConflictRecovery';
import { dataconnectionsApi } from '../api/dataconnections.api';
import { useDataConnections } from '../hooks/useDataConnections';

const PROVIDER_LABELS: Record<DatabaseProvider, string> = {
  PostgreSQL: 'PostgreSQL',
  SqlServer: 'SQL Server',
  MySQL: 'MySQL',
  Oracle: 'Oracle',
};

const PROVIDER_OPTIONS = databaseProviders.map((value) => ({
  value,
  label: PROVIDER_LABELS[value],
}));

function normalizeProvider(provider: string): DatabaseProvider {
  const lower = provider.trim().toLowerCase();
  const matched = databaseProviders.find((candidate) => candidate.toLowerCase() === lower);
  return matched ?? 'PostgreSQL';
}

export const ConnectionsSection: React.FC = () => {
  const {
    connections,
    isLoading,
    fetchConnections,
    createConnection,
    updateConnection,
    deleteConnection,
    testConnection,
    checkSavedConnection,
  } = useDataConnections();

  const { conflict, captureConflict, clearConflict } = useConflictRecovery();

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingConnection, setEditingConnection] = useState<DataConnectionDto | null>(null);
  const [editingEtag, setEditingEtag] = useState<string | null>(null);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [name, setName] = useState('');
  const [provider, setProvider] = useState<DatabaseProvider>('PostgreSQL');
  const [connectionString, setConnectionString] = useState('');
  const [isTesting, setIsTesting] = useState(false);
  const [checkingId, setCheckingId] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(
    null,
  );

  useEffect(() => {
    void fetchConnections();
  }, [fetchConnections]);

  const openModal = async (connection?: DataConnectionDto) => {
    clearConflict();
    setFieldErrors({});
    setTestResult(null);

    if (connection) {
      setEditingConnection(connection);
      setName(connection.name);
      setProvider(normalizeProvider(connection.provider));
      setConnectionString('');
      setEditingEtag(null);
      setIsModalOpen(true);
      setIsLoadingDetail(true);
      try {
        const latest = await dataconnectionsApi.getWithETag(connection.id);
        setEditingConnection(latest.data);
        setEditingEtag(latest.etag);
        setName(latest.data.name);
        setProvider(normalizeProvider(latest.data.provider));
      } catch (err) {
        setToast({ type: 'error', message: extractErrorMessage(err) });
      } finally {
        setIsLoadingDetail(false);
      }
    } else {
      setEditingConnection(null);
      setEditingEtag(null);
      setName('');
      setProvider('PostgreSQL');
      setConnectionString('');
      setIsModalOpen(true);
    }
  };

  const closeModal = () => {
    setIsModalOpen(false);
    setEditingConnection(null);
    setEditingEtag(null);
    setFieldErrors({});
    clearConflict();
  };

  const handleReloadAfterConflict = useCallback(async () => {
    clearConflict();
    await fetchConnections();
    if (editingConnection) {
      try {
        const latest = await dataconnectionsApi.getWithETag(editingConnection.id);
        setEditingConnection(latest.data);
        setEditingEtag(latest.etag);
        setName(latest.data.name);
        setProvider(normalizeProvider(latest.data.provider));
        setToast({ type: 'info', message: 'โหลดข้อมูลการเชื่อมต่อล่าสุดแล้ว' });
      } catch (err) {
        setToast({ type: 'error', message: extractErrorMessage(err) });
      }
    }
  }, [clearConflict, editingConnection, fetchConnections]);

  const handleSave = async () => {
    if (!name.trim() || !provider) return;
    if (!editingConnection && !connectionString) return;

    setIsSaving(true);
    setFieldErrors({});
    try {
      if (editingConnection) {
        await updateConnection(
          editingConnection.id,
          {
            name: name.trim(),
            provider,
            connectionString: connectionString || '',
          },
          editingEtag,
        );
        setToast({ type: 'success', message: 'อัปเดตการเชื่อมต่อฐานข้อมูลสำเร็จ' });
      } else {
        await createConnection({
          name: name.trim(),
          provider,
          connectionString,
        });
        setToast({ type: 'success', message: 'เพิ่มการเชื่อมต่อฐานข้อมูลสำเร็จ' });
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

  const handleTest = async () => {
    if (!provider || !connectionString) return;
    setIsTesting(true);
    setTestResult(null);
    try {
      const result = await testConnection({ provider, connectionString });
      setTestResult({
        success: result.success,
        message:
          result.message ||
          result.error ||
          (result.success ? 'เชื่อมต่อฐานข้อมูลสำเร็จ' : 'เชื่อมต่อฐานข้อมูลไม่สำเร็จ'),
      });
    } catch (err) {
      setTestResult({ success: false, message: extractErrorMessage(err, 'Connection failed') });
    } finally {
      setIsTesting(false);
    }
  };

  const handleCheckSaved = async (row: DataConnectionDto) => {
    setCheckingId(row.id);
    try {
      const result = await checkSavedConnection(row.id);
      setToast({
        type: result.success ? 'success' : 'error',
        message: `${row.name}: ${
          result.message ||
          result.error ||
          (result.success ? 'เชื่อมต่อสำเร็จ' : 'เชื่อมต่อไม่สำเร็จ')
        }`,
      });
    } catch (err) {
      setToast({ type: 'error', message: `${row.name}: ${extractErrorMessage(err)}` });
    } finally {
      setCheckingId(null);
    }
  };

  const handleDelete = async (row: DataConnectionDto) => {
    if (!confirm(`คุณแน่ใจหรือไม่ว่าต้องการลบการเชื่อมต่อ "${row.name}"?`)) return;
    clearConflict();
    try {
      const latest = await dataconnectionsApi.getWithETag(row.id);
      await deleteConnection(row.id, latest.etag);
      setToast({ type: 'success', message: `ลบการเชื่อมต่อ "${row.name}" สำเร็จ` });
    } catch (err) {
      if (captureConflict(err)) return;
      setToast({ type: 'error', message: extractErrorMessage(err) });
    }
  };

  const columns = [
    { header: 'ชื่อการเชื่อมต่อ', accessor: (row: DataConnectionDto) => row.name },
    { header: 'ประเภท (Provider)', accessor: (row: DataConnectionDto) => row.provider },
    {
      header: 'วันที่สร้าง',
      accessor: (row: DataConnectionDto) =>
        row.createdAt ? new Date(row.createdAt).toLocaleDateString('th-TH') : '—',
    },
    {
      header: 'จัดการ',
      accessor: (row: DataConnectionDto) => (
        <div className="flex items-center gap-1.5">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => void handleCheckSaved(row)}
            loading={checkingId === row.id}
            title="ตรวจสอบการเชื่อมต่อ"
            aria-label={`ตรวจสอบการเชื่อมต่อ ${row.name}`}
          >
            <Activity className="w-3.5 h-3.5" />
          </Button>
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
            onClick={() => void handleDelete(row)}
            aria-label={`ลบ ${row.name}`}
          >
            <Trash2 className="w-4 h-4" />
          </Button>
        </div>
      ),
    },
  ];

  return (
    <>
      {toast && <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} />}

      {!isModalOpen && conflict && (
        <ConflictBanner conflict={conflict} onReload={handleReloadAfterConflict} onDismiss={clearConflict} />
      )}

      <div className="flex items-center justify-between pb-3 border-b border-border">
        <span className="text-xs text-textMuted">
          จัดการการเชื่อมต่อฐานข้อมูลสำหรับดึงข้อมูลมาใช้ใน Dataset และ FieldMapping
        </span>
        <Button onClick={() => void openModal()} size="sm" className="gap-2">
          <Plus className="w-4 h-4" />
          เพิ่มฐานข้อมูล
        </Button>
      </div>

      <CardBlock>
        {connections.length === 0 && !isLoading ? (
          <EmptyState
            icon={Database}
            title="ยังไม่มีการเชื่อมต่อฐานข้อมูล"
            description="เพิ่มการเชื่อมต่อฐานข้อมูลของคุณเพื่อใช้ใน Dataset และ FieldMapping"
            actionLabel="เพิ่มการเชื่อมต่อ"
            onAction={() => void openModal()}
          />
        ) : (
          <Table
            columns={columns}
            data={connections}
            loading={isLoading}
            keyExtractor={(row: DataConnectionDto) => row.id}
            emptyMessage="ไม่พบข้อมูล"
          />
        )}
      </CardBlock>

      <Modal
        isOpen={isModalOpen}
        onClose={closeModal}
        title={editingConnection ? 'แก้ไขการเชื่อมต่อ' : 'เพิ่มการเชื่อมต่อใหม่'}
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
            label="ชื่อการเชื่อมต่อ"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="เช่น Production DB, CRM System"
            error={fieldErrors.name}
            required
          />
          <Select
            label="ประเภทฐานข้อมูล"
            value={provider}
            onChange={(e) => setProvider(e.target.value as DatabaseProvider)}
            options={PROVIDER_OPTIONS}
            error={fieldErrors.provider}
          />
          <div className="space-y-1">
            <Input
              label="Connection String"
              type="password"
              value={connectionString}
              onChange={(e) => setConnectionString(e.target.value)}
              placeholder={
                editingConnection
                  ? 'ปล่อยว่างหากไม่ต้องการเปลี่ยน'
                  : provider === 'PostgreSQL'
                    ? 'เช่น Host=localhost;Port=5432;Database=mydb;Username=user;Password=pass;'
                    : 'เช่น Server=myServer;Database=myDb;User Id=user;Password=pass;'
              }
              error={fieldErrors.connectionString}
              required={!editingConnection}
            />
            <p className="text-[10px] text-textMuted">
              ระบบจะเข้ารหัส (Encrypt) ข้อมูลนี้ด้วย AES-256 ก่อนบันทึกลงฐานข้อมูล
            </p>
          </div>

          {testResult && (
            <div
              className={`p-3 rounded-sm text-xs flex items-start gap-2 ${
                testResult.success
                  ? 'bg-emerald-50 text-emerald-800 border border-emerald-200'
                  : 'bg-red-50 text-red-800 border border-red-200'
              }`}
            >
              {testResult.success ? (
                <CheckCircle2 className="w-4 h-4 shrink-0 mt-0.5" />
              ) : (
                <XCircle className="w-4 h-4 shrink-0 mt-0.5" />
              )}
              <span>{testResult.message}</span>
            </div>
          )}

          <div className="pt-4 flex items-center justify-between border-t border-border">
            <Button
              variant="secondary"
              onClick={() => void handleTest()}
              disabled={isTesting || !connectionString}
              className="gap-2"
            >
              <Play className="w-4 h-4" />
              {isTesting ? 'กำลังทดสอบ...' : 'ทดสอบการเชื่อมต่อ'}
            </Button>
            <div className="flex gap-2">
              <Button variant="ghost" onClick={closeModal}>
                ยกเลิก
              </Button>
              <Button
                onClick={() => void handleSave()}
                loading={isSaving || isLoadingDetail}
                disabled={!name.trim() || (!editingConnection && !connectionString)}
              >
                บันทึก
              </Button>
            </div>
          </div>
        </div>
      </Modal>
    </>
  );
};

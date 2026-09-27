'use client';

import React, { useState, useEffect } from 'react';
import { Database, Trash2, Edit2, Play, CheckCircle2, XCircle, Plus } from 'lucide-react';
import { Button, CardBlock, Input, Select, Modal, Table, EmptyState } from '../../ui';
import { useDataConnections } from '@/hooks/useDataConnections';
import type { DataConnection } from '@/types/api';

export const ConnectionsSection: React.FC = () => {
  const {
    connections,
    isLoading,
    fetchConnections,
    createConnection,
    updateConnection,
    deleteConnection,
    testConnection,
  } = useDataConnections();

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingConnection, setEditingConnection] = useState<DataConnection | null>(null);
  const [name, setName] = useState('');
  const [provider, setProvider] = useState('postgresql');
  const [connectionString, setConnectionString] = useState('');
  const [isTesting, setIsTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);

  useEffect(() => { fetchConnections(); }, [fetchConnections]);

  const openModal = (connection?: DataConnection) => {
    if (connection) {
      setEditingConnection(connection);
      setName(connection.name);
      setProvider(connection.provider);
      setConnectionString('');
    } else {
      setEditingConnection(null);
      setName('');
      setProvider('postgresql');
      setConnectionString('');
    }
    setTestResult(null);
    setIsModalOpen(true);
  };

  const closeModal = () => { setIsModalOpen(false); setEditingConnection(null); };

  const handleSave = async () => {
    if (!name || !provider) return;
    if (!editingConnection && !connectionString) return;
    try {
      if (editingConnection) {
        await updateConnection(editingConnection.id, {
          name, provider,
          ...(connectionString ? { connectionString } : {}),
        });
      } else {
        await createConnection({ name, provider, connectionString });
      }
      closeModal();
    } catch (err) {
      console.error(err);
    }
  };

  const handleTest = async () => {
    if (!provider || !connectionString) return;
    setIsTesting(true);
    setTestResult(null);
    try {
      const result = await testConnection({ provider, connectionString });
      setTestResult(result);
    } catch (err: unknown) {
      setTestResult({ success: false, message: err instanceof Error ? err.message : 'Connection failed' });
    } finally {
      setIsTesting(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (confirm('คุณแน่ใจหรือไม่ว่าต้องการลบการเชื่อมต่อนี้?')) {
      await deleteConnection(id);
    }
  };

  const columns = [
    { header: 'ชื่อการเชื่อมต่อ', accessor: (row: DataConnection) => row.name },
    { header: 'ประเภท (Provider)', accessor: (row: DataConnection) => row.provider },
    {
      header: 'วันที่สร้าง',
      accessor: (row: DataConnection) => new Date(row.createdAt || '').toLocaleDateString('th-TH'),
    },
    {
      header: 'จัดการ',
      accessor: (row: DataConnection) => (
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" onClick={() => openModal(row)}>
            <Edit2 className="w-4 h-4" />
          </Button>
          <Button variant="danger" size="sm" onClick={() => handleDelete(row.id)}>
            <Trash2 className="w-4 h-4" />
          </Button>
        </div>
      ),
    },
  ];

  return (
    <>
      <div className="flex items-center justify-between pb-3 border-b border-border">
        <span className="text-xs text-textMuted">
          จัดการการเชื่อมต่อฐานข้อมูลสำหรับดึงข้อมูลมาใช้ใน Dataset และ FieldMapping
        </span>
        <Button onClick={() => openModal()} size="sm" className="gap-2">
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
            onAction={() => openModal()}
          />
        ) : (
          <Table
            columns={columns}
            data={connections}
            loading={isLoading}
            keyExtractor={(row: DataConnection) => row.id}
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
          <Input
            label="ชื่อการเชื่อมต่อ"
            value={name}
            onChange={e => setName(e.target.value)}
            placeholder="เช่น Production DB, CRM System"
            required
          />
          <Select
            label="ประเภทฐานข้อมูล"
            value={provider}
            onChange={e => setProvider(e.target.value)}
            options={[
              { value: 'postgresql', label: 'PostgreSQL' },
              { value: 'sqlserver', label: 'SQL Server' },
              { value: 'mysql', label: 'MySQL' },
            ]}
          />
          <div className="space-y-1">
            <Input
              label="Connection String"
              type="password"
              value={connectionString}
              onChange={e => setConnectionString(e.target.value)}
              placeholder={
                editingConnection
                  ? 'ปล่อยว่างหากไม่ต้องการเปลี่ยน'
                  : provider === 'postgresql'
                  ? 'เช่น postgresql://user:password@localhost:5432/mydb'
                  : 'เช่น Server=myServer;Database=myDb;User Id=user;Password=pass;'
              }
              required={!editingConnection}
            />
            <p className="text-[10px] text-textMuted">
              ระบบจะเข้ารหัส (Encrypt) ข้อมูลนี้ด้วย AES-256 ก่อนบันทึกลงฐานข้อมูล
            </p>
          </div>

          {testResult && (
            <div className={`p-3 rounded-sm text-xs flex items-start gap-2 ${testResult.success ? 'bg-emerald-50 text-emerald-800 border border-emerald-200' : 'bg-red-50 text-red-800 border border-red-200'}`}>
              {testResult.success
                ? <CheckCircle2 className="w-4 h-4 shrink-0 mt-0.5" />
                : <XCircle className="w-4 h-4 shrink-0 mt-0.5" />}
              <span>{testResult.message}</span>
            </div>
          )}

          <div className="pt-4 flex items-center justify-between border-t border-border">
            <Button
              variant="secondary"
              onClick={handleTest}
              disabled={isTesting || !connectionString}
              className="gap-2"
            >
              <Play className="w-4 h-4" />
              {isTesting ? 'กำลังทดสอบ...' : 'ทดสอบการเชื่อมต่อ'}
            </Button>
            <div className="flex gap-2">
              <Button variant="ghost" onClick={closeModal}>ยกเลิก</Button>
              <Button onClick={handleSave} disabled={!name || (!editingConnection && !connectionString)}>
                บันทึก
              </Button>
            </div>
          </div>
        </div>
      </Modal>
    </>
  );
};

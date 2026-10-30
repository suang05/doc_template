'use client';

import React, { useState, useEffect } from 'react';
import dynamic from 'next/dynamic';
import { Table2, Plus, Trash2, Edit2, Clock, Code2 } from 'lucide-react';
import { Button, CardBlock, Input, Select, Modal, Table, EmptyState } from '@/components/ui';
import { useDatasets } from '../hooks/useDatasets';
import type { DataConnection, Dataset, CreateDatasetRequest } from '@/types/api';

const Editor = dynamic(
  () => import('@monaco-editor/react').then((mod) => mod.Editor),
  { ssr: false }
);

interface DatasetModalState {
  name: string;
  description: string;
  dataConnectionId: string;
  sqlQuery: string;
  cacheSeconds: number;
}

const emptyDatasetForm = (): DatasetModalState => ({
  name: '',
  description: '',
  dataConnectionId: '',
  sqlQuery: '',
  cacheSeconds: 0,
});

interface DatasetsSectionProps {
  connections: DataConnection[];
}

export const DatasetsSection: React.FC<DatasetsSectionProps> = ({ connections }) => {
  const { datasets, isLoading, fetchDatasets, createDataset, updateDataset, deleteDataset } = useDatasets();
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingDataset, setEditingDataset] = useState<Dataset | null>(null);
  const [form, setForm] = useState<DatasetModalState>(emptyDatasetForm());
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => { fetchDatasets(); }, [fetchDatasets]);

  const openModal = (dataset?: Dataset) => {
    if (dataset) {
      setEditingDataset(dataset);
      setForm({
        name: dataset.name,
        description: dataset.description ?? '',
        dataConnectionId: dataset.dataConnectionId,
        sqlQuery: dataset.sqlQuery,
        cacheSeconds: dataset.cacheSeconds,
      });
    } else {
      setEditingDataset(null);
      setForm({ ...emptyDatasetForm(), dataConnectionId: connections[0]?.id ?? '' });
    }
    setIsModalOpen(true);
  };

  const closeModal = () => { setIsModalOpen(false); setEditingDataset(null); };

  const handleSave = async () => {
    if (!form.name.trim() || !form.dataConnectionId || !form.sqlQuery.trim()) return;
    setIsSaving(true);
    try {
      const payload: CreateDatasetRequest = {
        name: form.name.trim(),
        description: form.description.trim() || undefined,
        dataConnectionId: form.dataConnectionId,
        sqlQuery: form.sqlQuery.trim(),
        cacheSeconds: form.cacheSeconds,
      };
      if (editingDataset) {
        await updateDataset(editingDataset.id, payload);
      } else {
        await createDataset(payload);
      }
      closeModal();
    } catch (err) {
      console.error(err);
    } finally {
      setIsSaving(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('ยืนยันการลบ Dataset นี้? FieldMapping ที่ใช้อยู่จะสูญเสียข้อมูล SQL')) return;
    try {
      await deleteDataset(id);
    } catch (err) {
      console.error(err);
    }
  };

  const setField = <K extends keyof DatasetModalState>(key: K, val: DatasetModalState[K]) =>
    setForm(prev => ({ ...prev, [key]: val }));

  const connectionOptions = connections.map(c => ({ value: c.id, label: c.name }));

  const columns = [
    {
      header: 'ชื่อ Dataset',
      accessor: (row: Dataset) => (
        <div>
          <p className="text-xs font-medium text-textPrimary">{row.name}</p>
          {row.description && (
            <p className="text-[11px] text-textMuted truncate max-w-[200px]">{row.description}</p>
          )}
        </div>
      ),
    },
    { header: 'DataConnection', accessor: (row: Dataset) => (
      <span className="text-xs text-textSecondary">{row.dataConnectionName}</span>
    )},
    {
      header: 'SQL Query',
      accessor: (row: Dataset) => (
        <code className="text-[11px] text-textMuted font-mono truncate block max-w-[280px]">
          {row.sqlQuery}
        </code>
      ),
    },
    {
      header: 'Cache',
      accessor: (row: Dataset) => (
        <span className="flex items-center gap-1 text-[11px] text-textMuted">
          <Clock className="w-3 h-3" />
          {row.cacheSeconds > 0 ? `${row.cacheSeconds}s` : 'ไม่ cache'}
        </span>
      ),
    },
    {
      header: 'จัดการ',
      accessor: (row: Dataset) => (
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

  const canSave = form.name.trim() && form.dataConnectionId && form.sqlQuery.trim();

  return (
    <>
      <div className="flex items-center justify-between pb-3 border-b border-border">
        <span className="text-xs text-textMuted">
          Dataset คือ SQL query ที่ตั้งชื่อไว้ — FieldMapping แบบ SQL เลือก Dataset แทนการเขียน query ซ้ำ
        </span>
        <Button onClick={() => openModal()} size="sm" className="gap-2" disabled={connections.length === 0}>
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
            onAction={() => openModal()}
          />
        ) : (
          <Table
            columns={columns}
            data={datasets}
            loading={isLoading}
            keyExtractor={(row: Dataset) => row.id}
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
          <Input
            label="ชื่อ Dataset"
            value={form.name}
            onChange={e => setField('name', e.target.value)}
            placeholder="เช่น ราคาสัญญา, ข้อมูลลูกค้า"
            required
          />
          <Input
            label="คำอธิบาย (ไม่บังคับ)"
            value={form.description}
            onChange={e => setField('description', e.target.value)}
            placeholder="อธิบายว่า Dataset นี้ดึงข้อมูลอะไร"
          />
          <Select
            label="DataConnection"
            value={form.dataConnectionId}
            onChange={e => setField('dataConnectionId', e.target.value)}
            options={connectionOptions}
          />

          <div className="space-y-1">
            <div className="flex items-center justify-between">
              <label className="block text-xs font-medium text-textSecondary flex items-center gap-1.5">
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

            {Array.from(new Set(form.sqlQuery.match(/[@:][a-zA-Z0-9_]+/g) || [])).length > 0 && (
              <div className="flex items-center gap-1.5 flex-wrap pt-1">
                <span className="text-[10px] text-textMuted uppercase font-mono">ตัวแปรที่พบ:</span>
                {Array.from(new Set(form.sqlQuery.match(/[@:][a-zA-Z0-9_]+/g) || [])).map((param) => (
                  <span key={param} className="px-1.5 py-0.2 bg-sky-50 text-sky-700 text-[10px] font-mono border border-sky-200 rounded-xs">
                    {param}
                  </span>
                ))}
              </div>
            )}

            <p className="text-[10px] text-textMuted pt-0.5">
              ใช้ <code className="bg-slate-100 px-1 rounded">@variableName</code> หรือ <code className="bg-slate-100 px-1 rounded">:param</code> เพื่อรับค่าจาก request payload เช่น <code className="bg-slate-100 px-1 rounded">@ref</code>
            </p>
          </div>

          <Input
            label="Cache (วินาที)"
            type="number"
            value={form.cacheSeconds}
            onChange={e => setField('cacheSeconds', Math.max(0, Number(e.target.value)))}
            placeholder="0 = ไม่ cache"
            helperText="ตั้งค่า cache เพื่อลดการยิง query ซ้ำต่อการสร้างเอกสาร 1 ครั้ง"
          />

          <div className="pt-4 flex justify-end gap-2 border-t border-border">
            <Button variant="ghost" onClick={closeModal}>ยกเลิก</Button>
            <Button onClick={handleSave} disabled={!canSave || isSaving}>
              {isSaving ? 'กำลังบันทึก...' : 'บันทึก'}
            </Button>
          </div>
        </div>
      </Modal>
    </>
  );
};

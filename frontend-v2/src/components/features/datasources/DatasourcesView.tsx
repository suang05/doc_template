import React, { useState, useEffect, useCallback } from 'react';
import { Database, Table2, Plus, Trash2, Edit2, Play, CheckCircle2, XCircle, Clock } from 'lucide-react';
import {
  Button,
  CardBlock,
  Input,
  Select,
  Modal,
  Table,
  EmptyState,
  Tabs,
} from '../../ui';
import { useDataConnections } from '../../../hooks/useDataConnections';
import type { DataConnection } from '@/types/api';
import { datasetsApi } from '../../../lib/api/datasets.api';
import type { Dataset, CreateDatasetRequest } from '../../../types/api';

// ─── Datasets section ────────────────────────────────────────────────────────

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

const DatasetsSection: React.FC<DatasetsSectionProps> = ({ connections }) => {
  const [datasets, setDatasets] = useState<Dataset[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingDataset, setEditingDataset] = useState<Dataset | null>(null);
  const [form, setForm] = useState<DatasetModalState>(emptyDatasetForm());
  const [isSaving, setIsSaving] = useState(false);

  const fetchDatasets = useCallback(async () => {
    setIsLoading(true);
    try {
      const data = await datasetsApi.getAll();
      setDatasets(data ?? []);
    } catch {
      setDatasets([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

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
        const updated = await datasetsApi.update(editingDataset.id, payload);
        setDatasets(prev => prev.map(d => d.id === editingDataset.id ? updated : d));
      } else {
        const created = await datasetsApi.create(payload);
        setDatasets(prev => [...prev, created]);
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
      await datasetsApi.delete(id);
      setDatasets(prev => prev.filter(d => d.id !== id));
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
            <label className="block text-xs font-medium text-textSecondary">
              SQL Query <span className="text-red-500">*</span>
            </label>
            <textarea
              value={form.sqlQuery}
              onChange={e => setField('sqlQuery', e.target.value)}
              rows={5}
              placeholder={'SELECT total FROM contracts WHERE ref = @ref'}
              className="w-full rounded-sm bg-surface border border-border text-xs text-textPrimary placeholder:text-textMuted font-mono px-2.5 py-2 focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary resize-y"
            />
            <p className="text-[10px] text-textMuted">
              ใช้ <code className="bg-slate-100 px-1 rounded">@variableName</code> เพื่อรับค่าจาก request data เช่น <code className="bg-slate-100 px-1 rounded">@ref</code> หรือ <code className="bg-slate-100 px-1 rounded">@customerId</code>
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

// ─── Connections section (extracted) ─────────────────────────────────────────

const ConnectionsSection: React.FC = () => {
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
    } catch (err: any) {
      setTestResult({ success: false, message: err.message || 'Connection failed' });
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

// ─── Root view ────────────────────────────────────────────────────────────────

export const DatasourcesView: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'connections' | 'datasets'>('connections');
  const { connections, fetchConnections } = useDataConnections();

  useEffect(() => {
    if (activeTab === 'datasets') fetchConnections();
  }, [activeTab, fetchConnections]);

  const tabs = [
    { id: 'connections', label: 'DataConnections', count: connections.length },
    { id: 'datasets',    label: 'Datasets' },
  ];

  return (
    <div className="h-full flex flex-col p-6 max-w-6xl mx-auto space-y-4">
      <div className="flex items-center gap-3 pb-3 border-b border-border">
        <div className="w-8 h-8 rounded-sm bg-slate-100 text-slate-600 flex items-center justify-center border border-slate-200 shrink-0">
          <Database className="w-4 h-4" />
        </div>
        <div>
          <p className="text-sm font-medium text-textPrimary">Data Sources</p>
          <p className="text-[11px] text-textMuted">DataConnection → Dataset → FieldMapping (SQL)</p>
        </div>
      </div>

      <Tabs
        tabs={tabs}
        activeTab={activeTab}
        onChange={id => setActiveTab(id as 'connections' | 'datasets')}
        className="-mx-6 px-6"
      />

      <div className="space-y-4">
        {activeTab === 'connections' && <ConnectionsSection />}
        {activeTab === 'datasets'    && <DatasetsSection connections={connections} />}
      </div>
    </div>
  );
};

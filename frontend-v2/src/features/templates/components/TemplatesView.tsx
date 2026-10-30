'use client';

import React, { useState, useEffect } from 'react';
import {
  Search,
  Plus,
  Edit3,
  GitFork,
  Play,
  FileText,
  Download,
  Trash2,
  History,
  Copy,
  AlertCircle,
  RefreshCw,
  LayoutGrid,
  List,
} from 'lucide-react';
import { useTemplates } from '../hooks/useTemplates';
import { DocumentCategory, DocumentFormat, tokens } from '@/tokens';
import {
  Button,
  Badge,
  CardBlock,
  Input,
  Select,
  Toolbar,
  EmptyState,
  StatBlock,
  Dropdown,
  Table,
  Column,
} from '@/components/ui';
import { templatesApi } from '../api/templates.api';
import { TemplateDto } from '../schemas/template.schema';
import { CreateTemplateChoiceModal } from './CreateTemplateChoiceModal';

export interface TemplatesViewProps {
  onOpenStudio: (templateId: string) => void;
  onOpenMapping: (templateId: string) => void;
  onOpenGenerator: (templateSlug: string) => void;
  onNavigateUpload?: () => void;
}

export const TemplatesView: React.FC<TemplatesViewProps> = ({
  onOpenStudio,
  onOpenMapping,
  onOpenGenerator,
  onNavigateUpload,
}) => {
  const {
    filteredTemplates,
    templates,
    loading,
    error,
    searchQuery,
    setSearchQuery,
    selectedCategory,
    setSelectedCategory,
    refresh,
  } = useTemplates();

  const [isChoiceModalOpen, setIsChoiceModalOpen] = useState(false);
  const [viewMode, setViewMode] = useState<'cards' | 'table'>('table');

  useEffect(() => {
    try {
      const saved = localStorage.getItem('smk_templates_view_mode');
      if (saved === 'cards' || saved === 'table') {
        setViewMode(saved);
      }
    } catch {
      // localStorage unavailable or restricted
    }
  }, []);

  const handleViewModeChange = (mode: 'cards' | 'table') => {
    setViewMode(mode);
    try {
      localStorage.setItem('smk_templates_view_mode', mode);
    } catch {
      // ignore
    }
  };

  const handleDeactivate = async (id: string, name: string) => {
    if (!confirm(`ต้องการยกเลิกการใช้งานแม่แบบ "${name}" หรือไม่?`)) return;
    try {
      await templatesApi.deactivateTemplate(id);
      await refresh();
    } catch (err: any) {
      alert(err.message || 'ปิดใช้งานล้มเหลว');
    }
  };

  const handleDownload = async (id: string, slug: string) => {
    try {
      const { blob, fileName } = await templatesApi.downloadTemplate(id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName || `${slug}-template.html`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (err: any) {
      alert(err.message || 'ดาวน์โหลดแม่แบบล้มเหลว');
    }
  };

  const categoryOptions = [
    { value: 'all', label: 'ทุกหมวดหมู่' },
    { value: 'contract', label: 'สัญญา/นิติกรรม' },
    { value: 'financial', label: 'การเงิน/ใบเสร็จ' },
    { value: 'official', label: 'หนังสือสำคัญ' },
    { value: 'hr', label: 'บุคคล/ภายใน' },
    { value: 'operations', label: 'ปฏิบัติการทั่วไป' },
  ];

  const tableColumns: Column<TemplateDto>[] = [
    {
      header: 'ชื่อแม่แบบเอกสาร',
      accessor: (item) => {
        const cat = (item.category || 'operations') as DocumentCategory;
        const fmt = (item.fileFormat || 'html') as DocumentFormat;
        return (
          <div className="flex items-center gap-2">
            <Badge category={cat} dot size="sm">
              {tokens.categories[cat]?.label || item.category}
            </Badge>
            <span
              className="font-bold text-textPrimary hover:text-sky-600 cursor-pointer truncate max-w-xs transition-colors"
              onClick={() => (fmt === 'html' ? onOpenStudio(item.id) : onOpenMapping(item.id))}
              title={item.name}
            >
              {item.name}
            </span>
          </div>
        );
      },
    },
    {
      header: 'Slug',
      accessor: (item) => (
        <span className="font-mono text-xs text-sky-700 bg-sky-50 px-2 py-0.5 rounded-sm border border-sky-200">
          {item.slug}
        </span>
      ),
    },
    {
      header: 'รูปแบบ / Engine',
      accessor: (item) => {
        const fmt = (item.fileFormat || 'html') as DocumentFormat;
        return (
          <Badge format={fmt} size="sm">
            {tokens.formats[fmt]?.label || fmt.toUpperCase()}
          </Badge>
        );
      },
      align: 'center',
    },
    {
      header: 'สถานะ',
      accessor: (item) => (
        <div className="flex items-center justify-center gap-1.5 text-[11px] font-medium text-textSecondary">
          <span className={`w-2 h-2 rounded-full ${item.isActive !== false ? 'bg-emerald-500' : 'bg-slate-300'}`} />
          <span>{item.isActive !== false ? 'Active' : 'Inactive'}</span>
        </div>
      ),
      align: 'center',
    },
    {
      header: 'แก้ไขล่าสุด',
      accessor: (item) => (
        <span className="text-[11px] font-mono text-textMuted">
          {new Date(item.updatedAt).toLocaleDateString('th-TH', { day: 'numeric', month: 'short', year: 'numeric' })}
        </span>
      ),
      align: 'center',
    },
    {
      header: 'การจัดการ',
      accessor: (item) => {
        const fmt = (item.fileFormat || 'html') as DocumentFormat;
        return (
          <div className="flex items-center justify-end gap-1.5">
            <Button
              variant="primary"
              size="sm"
              icon={Play}
              onClick={() => onOpenGenerator(item.slug)}
            >
              สร้างเอกสาร
            </Button>
            <Dropdown
              align="right"
              items={
                fmt === 'html'
                  ? [
                      { id: 'studio', label: 'แก้ไขโค้ด (Monaco Studio)', icon: Edit3, onClick: () => onOpenStudio(item.id) },
                      { id: 'duplicate', label: 'ทำสำเนา', icon: Copy, onClick: () => console.log('Duplicate', item.id) },
                      { id: 'download', label: 'ดาวน์โหลดไฟล์ HTML', icon: Download, onClick: () => handleDownload(item.id, item.slug) },
                      { id: 'deactivate', label: item.isActive !== false ? 'ระงับการใช้งาน' : 'เปิดใช้งาน', icon: Trash2, danger: item.isActive !== false, onClick: () => handleDeactivate(item.id, item.name) },
                    ]
                  : [
                      { id: 'mapping', label: 'กำหนดฟิลด์ (Field Mapping)', icon: GitFork, onClick: () => onOpenMapping(item.id) },
                      { id: 'history', label: 'ประวัติเวอร์ชัน', icon: History, onClick: () => console.log('Version history', item.id) },
                      { id: 'download', label: 'ดาวน์โหลดไฟล์ต้นฉบับ', icon: Download, onClick: () => handleDownload(item.id, item.slug) },
                      { id: 'deactivate', label: item.isActive !== false ? 'ระงับการใช้งาน' : 'เปิดใช้งาน', icon: Trash2, danger: item.isActive !== false, onClick: () => handleDeactivate(item.id, item.name) },
                    ]
              }
            />
          </div>
        );
      },
      align: 'right',
    },
  ];

  return (
    <div className="space-y-4">
      {/* Metric Tiles */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <StatBlock
          label="แม่แบบทั้งหมด"
          value={templates.length}
          category="official"
          icon={FileText}
        />
        <StatBlock
          label="สัญญา / นิติกรรม"
          value={templates.filter((t) => t.category === 'contract').length}
          category="contract"
        />
        <StatBlock
          label="การเงิน / ใบเสร็จ"
          value={templates.filter((t) => t.category === 'financial').length}
          category="financial"
        />
        <StatBlock
          label="ปฏิบัติการ / ทั่วไป"
          value={templates.filter((t) => t.category === 'operations' || t.category === 'hr').length}
          category="operations"
        />
      </div>

      {/* Toolbar */}
      <Toolbar
        leftSlot={
          <>
            <div className="w-56">
              <Input
                placeholder="ค้นหาแม่แบบ..."
                icon={Search}
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
            <div className="w-48">
              <Select
                options={categoryOptions}
                value={selectedCategory}
                onChange={(e) => setSelectedCategory(e.target.value as any)}
              />
            </div>
          </>
        }
        rightSlot={
          <div className="flex items-center gap-2">
            {/* View Switcher: Table vs Cards */}
            <div className="flex items-center border border-border rounded-sm overflow-hidden bg-surface">
              <button
                type="button"
                onClick={() => handleViewModeChange('table')}
                className={`p-1.5 transition-colors cursor-pointer ${
                  viewMode === 'table'
                    ? 'bg-primaryLight text-primary font-semibold'
                    : 'text-textMuted hover:text-textPrimary hover:bg-surfaceSubtle'
                }`}
                title="มุมมองตาราง (Table View)"
              >
                <List className="w-4 h-4" />
              </button>
              <button
                type="button"
                onClick={() => handleViewModeChange('cards')}
                className={`p-1.5 transition-colors cursor-pointer ${
                  viewMode === 'cards'
                    ? 'bg-primaryLight text-primary font-semibold'
                    : 'text-textMuted hover:text-textPrimary hover:bg-surfaceSubtle'
                }`}
                title="มุมมองการ์ด (Card Grid)"
              >
                <LayoutGrid className="w-4 h-4" />
              </button>
            </div>

            <Button
              variant="primary"
              icon={Plus}
              onClick={() => setIsChoiceModalOpen(true)}
            >
              สร้างแม่แบบใหม่
            </Button>
          </div>
        }
      />

      {/* Cards Grid */}
      {error ? (
        <div className="p-12 text-center flex flex-col items-center justify-center gap-3 bg-red-50/50 border border-red-100 rounded-sm">
          <AlertCircle className="w-8 h-8 text-red-500" />
          <div className="space-y-1">
            <h3 className="text-sm font-bold text-red-700">เกิดข้อผิดพลาดในการโหลดข้อมูล</h3>
            <p className="text-xs text-red-600/80">{error}</p>
          </div>
          <Button variant="outline" size="sm" icon={RefreshCw} onClick={refresh} className="mt-2 bg-white">
            ลองอีกครั้ง
          </Button>
        </div>
      ) : loading ? (
        <div className="p-12 text-center text-xs text-textMuted flex items-center justify-center gap-2">
          <span className="w-4 h-4 border-2 border-primary border-t-transparent rounded-full animate-spin" />
          <span>กำลังโหลด...</span>
        </div>
      ) : filteredTemplates.length === 0 ? (
        <EmptyState
          title="ไม่พบข้อมูล"
          description="ไม่มีแม่แบบในหมวดหมู่นี้"
          actionLabel="สร้างแม่แบบ"
          onAction={() => setIsChoiceModalOpen(true)}
        />
      ) : viewMode === 'table' ? (
        <Table<TemplateDto>
          columns={tableColumns}
          data={filteredTemplates}
          keyExtractor={(t) => t.id}
          emptyMessage="ไม่มีแม่แบบในหมวดหมู่นี้"
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
          {filteredTemplates.map((t) => {
            const cat = (t.category || 'operations') as DocumentCategory;
            const fmt = (t.fileFormat || 'html') as DocumentFormat;

            return (
              <CardBlock key={t.id} category={cat} hoverable className="flex flex-col justify-between p-3.5 space-y-3">
                {/* Header */}
                <div className="space-y-1.5">
                  <div className="flex items-center justify-between gap-1.5">
                    <Badge category={cat} dot size="sm">
                      {tokens.categories[cat]?.label || t.category}
                    </Badge>
                    <div className="flex items-center gap-1">
                      <Badge format={fmt} size="sm">
                        {tokens.formats[fmt]?.label || fmt.toUpperCase()}
                      </Badge>
                    </div>
                  </div>

                  <h3
                    className="text-xs font-bold text-textPrimary truncate cursor-pointer hover:text-sky-600 transition-colors"
                    title={t.name}
                    onClick={() => (fmt === 'html' ? onOpenStudio(t.id) : onOpenMapping(t.id))}
                  >
                    {t.name}
                  </h3>

                  <div className="flex items-center justify-between">
                    <p className="text-[11px] font-mono text-textMuted truncate">
                      slug: <span className="text-sky-700">{t.slug}</span>
                    </p>
                    <div className="flex items-center gap-1 text-[10px] text-textMuted font-medium">
                      <span className={`w-1.5 h-1.5 rounded-full ${t.isActive !== false ? 'bg-emerald-500' : 'bg-slate-300'}`} />
                      {t.isActive !== false ? 'Active' : 'Inactive'}
                    </div>
                  </div>
                  
                  <div className="text-[10px] text-textMuted font-mono">
                    {new Date(t.updatedAt).toLocaleDateString('th-TH', { day: 'numeric', month: 'short', year: 'numeric' })}
                  </div>
                </div>

                {/* Footer Actions */}
                <div className="pt-2 border-t border-border flex items-center justify-between gap-1">
                  <Dropdown
                    align="left"
                    items={
                      fmt === 'html'
                        ? [
                            { id: 'studio', label: 'แก้ไขโค้ด (Monaco Studio)', icon: Edit3, onClick: () => onOpenStudio(t.id) },
                            { id: 'duplicate', label: 'ทำสำเนา', icon: Copy, onClick: () => console.log('Duplicate', t.id) },
                            { id: 'download', label: 'ดาวน์โหลดไฟล์ HTML', icon: Download, onClick: () => handleDownload(t.id, t.slug) },
                            { id: 'deactivate', label: t.isActive !== false ? 'ระงับการใช้งาน' : 'เปิดใช้งาน', icon: Trash2, danger: t.isActive !== false, onClick: () => handleDeactivate(t.id, t.name) },
                          ]
                        : [
                            { id: 'mapping', label: 'กำหนดฟิลด์ (Field Mapping)', icon: GitFork, onClick: () => onOpenMapping(t.id) },
                            { id: 'history', label: 'ประวัติเวอร์ชัน', icon: History, onClick: () => console.log('Version history', t.id) },
                            { id: 'download', label: 'ดาวน์โหลดไฟล์ต้นฉบับ', icon: Download, onClick: () => handleDownload(t.id, t.slug) },
                            { id: 'deactivate', label: t.isActive !== false ? 'ระงับการใช้งาน' : 'เปิดใช้งาน', icon: Trash2, danger: t.isActive !== false, onClick: () => handleDeactivate(t.id, t.name) },
                          ]
                    }
                  />

                  <Button
                    variant="primary"
                    size="sm"
                    icon={Play}
                    onClick={() => onOpenGenerator(t.slug)}
                  >
                    สร้างเอกสาร
                  </Button>
                </div>
              </CardBlock>
            );
          })}
        </div>
      )}

      {/* Choice Modal */}
      <CreateTemplateChoiceModal
        isOpen={isChoiceModalOpen}
        onClose={() => setIsChoiceModalOpen(false)}
        onSelectOffice={() => {
          if (onNavigateUpload) onNavigateUpload();
        }}
        onOpenStudio={(templateId) => onOpenStudio(templateId)}
        onCreated={() => refresh()}
      />
    </div>
  );
};

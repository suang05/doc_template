'use client';

import React, { useState } from 'react';
import dynamic from 'next/dynamic';
import {
  GitFork,
  History,
  RotateCcw,
  CheckCircle2,
  AlertTriangle,
  FileCode,
  Calendar,
  User,
  ArrowRight,
} from 'lucide-react';
import { useVersionHistory } from '../hooks/useVersionHistory';
import { PageHeader, Button, Select, Badge, EmptyState } from '@/components/ui';

const DiffEditor = dynamic(
  () => import('@monaco-editor/react').then((mod) => mod.DiffEditor),
  { ssr: false }
);

export const VersionHistoryView: React.FC = () => {
  const {
    templates,
    templatesLoading,
    selectedTemplate,
    selectedTemplateId,
    setSelectedTemplateId,
    versions,
    selectedVersion,
    setSelectedVersion,
    currentHtml,
    loading,
    rollingBack,
    error,
    successMessage,
    handleRollback,
  } = useVersionHistory();

  const [sideBySide, setSideBySide] = useState(true);

  const templateOptions = templates.map((t) => ({
    value: t.id,
    label: `${t.name} (${t.slug})`,
  }));

  const activeVersionObj = versions.find((v) => v.version === selectedVersion);
  const previousVersionObj = versions.find((v) => v.version === (selectedVersion ? selectedVersion - 1 : 0));

  // Placeholder diff content if raw historical HTML is stored on MinIO
  const modifiedCode = currentHtml || `<!-- v${selectedVersion || 1} HTML Content -->\n<html>\n  <body>\n    <h1>${selectedTemplate?.name || 'Template'}</h1>\n  </body>\n</html>`;
  const originalCode = previousVersionObj
    ? `<!-- v${previousVersionObj.version} Previous HTML Content -->\n<html>\n  <body>\n    <!-- Earlier revision -->\n  </body>\n</html>`
    : `<!-- Initial commit -->`;

  return (
    <div className="space-y-4">
      {/* Header */}
      <PageHeader
        title="ประวัติเวอร์ชันแม่แบบ (Template Version History & Diff)"
        description="เปรียบเทียบการเปลี่ยนแปลงโค้ด HTML ของแม่แบบแต่ละเวอร์ชัน และย้อนคืนค่าเดิม (Rollback)"
        icon={GitFork}
        iconColor="indigo"
        rightSlot={
          <div className="w-72">
            <Select
              options={templateOptions}
              value={selectedTemplateId}
              onChange={(e) => setSelectedTemplateId(e.target.value)}
              disabled={templatesLoading}
            />
          </div>
        }
      />

      {successMessage && (
        <div className="p-3 bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs rounded-sm flex items-center gap-2">
          <CheckCircle2 className="w-4 h-4 shrink-0 text-emerald-600" />
          <span>{successMessage}</span>
        </div>
      )}

      {error && (
        <div className="p-3 bg-red-50 border border-red-200 text-red-700 text-xs rounded-sm flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 shrink-0 text-red-500" />
          <span>{error}</span>
        </div>
      )}

      {/* Main Split Layout: Left Timeline (30%), Right Diff Editor (70%) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 h-[calc(100vh-13rem)] min-h-[500px]">
        {/* Left Column: Revision Timeline */}
        <div className="lg:col-span-4 bg-surface border border-border rounded-md shadow-xs flex flex-col overflow-hidden">
          <div className="p-3 border-b border-border bg-surfaceSubtle flex items-center justify-between shrink-0">
            <span className="text-xs font-bold text-textPrimary flex items-center gap-1.5">
              <History className="w-3.5 h-3.5 text-primary" />
              ลำดับเวอร์ชัน ({versions.length})
            </span>
            {selectedTemplate && (
              <Badge format={selectedTemplate.fileFormat || 'html'} size="sm">
                {(selectedTemplate.fileFormat || 'html').toUpperCase()}
              </Badge>
            )}
          </div>

          <div className="flex-1 overflow-y-auto p-2 space-y-1.5 custom-scrollbar">
            {loading ? (
              <div className="py-12 text-center text-xs text-textMuted flex items-center justify-center gap-2">
                <span className="w-3.5 h-3.5 border-2 border-primary border-t-transparent rounded-full animate-spin" />
                <span>กำลังโหลดประวัติ...</span>
              </div>
            ) : versions.length === 0 ? (
              <EmptyState
                title="ไม่พบประวัติเวอร์ชัน"
                description="แม่แบบนี้ยังไม่มีการบันทึกประวัติเพิ่มเติม"
              />
            ) : (
              versions.map((ver) => {
                const isSelected = selectedVersion === ver.version;
                const isLatest = ver.version === Math.max(...versions.map((v) => v.version));

                return (
                  <div
                    key={ver.id}
                    onClick={() => setSelectedVersion(ver.version)}
                    className={`p-3 rounded-sm border transition-all cursor-pointer ${
                      isSelected
                        ? 'bg-sky-50/70 border-sky-300 shadow-xs'
                        : 'bg-surface border-border hover:bg-surfaceSubtle hover:border-slate-300'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <span className="font-mono font-bold text-xs text-sky-800 bg-sky-100 px-1.5 py-0.5 rounded-xs border border-sky-200">
                          v{ver.version}
                        </span>
                        {isLatest && (
                          <span className="text-[10px] font-semibold text-emerald-700 bg-emerald-50 px-1.5 py-0.2 rounded-xs border border-emerald-200">
                            ล่าสุด
                          </span>
                        )}
                      </div>

                      <span className="text-[10px] font-mono text-textMuted flex items-center gap-1">
                        <Calendar className="w-3 h-3" />
                        {new Date(ver.createdAt).toLocaleDateString('th-TH')}
                      </span>
                    </div>

                    <p className="text-xs text-textPrimary mt-2 font-medium line-clamp-2">
                      {ver.commitMessage || 'บันทึกการแก้ไขแม่แบบ'}
                    </p>

                    <div className="mt-2.5 pt-2 border-t border-border/60 flex items-center justify-between text-[10px] text-textMuted font-mono">
                      <span className="flex items-center gap-1">
                        <User className="w-3 h-3" />
                        {ver.createdBy || 'ระบบอัตโนมัติ'}
                      </span>
                      <span className="truncate max-w-[120px]">
                        {ver.storageKey.split('/').pop()}
                      </span>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>

        {/* Right Column: Monaco Diff Editor */}
        <div className="lg:col-span-8 bg-surface border border-border rounded-md shadow-xs flex flex-col overflow-hidden">
          {/* Diff Header Controls */}
          <div className="p-3 border-b border-border bg-surfaceSubtle flex items-center justify-between flex-wrap gap-2 shrink-0">
            <div className="flex items-center gap-2 text-xs">
              <FileCode className="w-4 h-4 text-primary" />
              <span className="font-semibold text-textPrimary">
                เปรียบเทียบความแตกต่าง:
              </span>
              <span className="font-mono text-textMuted">
                {previousVersionObj ? `v${previousVersionObj.version}` : 'ฉบับเริ่มต้น'}
              </span>
              <ArrowRight className="w-3 h-3 text-textMuted" />
              <span className="font-mono font-bold text-sky-700">
                v{selectedVersion || 1}
              </span>
            </div>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setSideBySide(!sideBySide)}
                className="text-[11px] font-medium px-2 py-1 rounded-sm border border-border bg-surface text-textSecondary hover:bg-surfaceSubtle transition-colors cursor-pointer"
              >
                {sideBySide ? 'โหมด Unified' : 'โหมด Side-by-Side'}
              </button>

              {activeVersionObj && (
                <Button
                  variant="outline"
                  size="sm"
                  icon={RotateCcw}
                  loading={rollingBack}
                  onClick={() => handleRollback(activeVersionObj.version)}
                >
                  ย้อนกลับเป็น v{activeVersionObj.version}
                </Button>
              )}
            </div>
          </div>

          {/* Monaco Diff Viewer Canvas */}
          <div className="flex-1 min-h-0 bg-zinc-950">
            <DiffEditor
              original={originalCode}
              modified={modifiedCode}
              language="html"
              theme="vs-dark"
              options={{
                readOnly: true,
                renderSideBySide: sideBySide,
                minimap: { enabled: false },
                fontSize: 12,
                scrollBeyondLastLine: false,
                wordWrap: 'on',
              }}
              height="100%"
            />
          </div>
        </div>
      </div>
    </div>
  );
};


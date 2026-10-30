'use client';

import React from 'react';
import {
  BarChart3,
  CheckCircle2,
  Clock,
  FileText,
  Layers,
  RefreshCw,
  AlertTriangle,
  Server,
  FileSpreadsheet,
} from 'lucide-react';
import { useAnalytics } from '../hooks/useAnalytics';
import { PageHeader, Button, Badge } from '@/components/ui';
import { DocumentFormat } from '@/types/api';

export const AnalyticsView: React.FC = () => {
  const {
    totalGenerated,
    successRate,
    avgDurationMs,
    activeTemplatesCount,
    formatCounts,
    callerAppCounts,
    recentLogs,
    loading,
    error,
    refresh,
  } = useAnalytics();

  const totalFormatSum = (formatCounts.pdf + formatCounts.docx + formatCounts.xlsx) || 1;
  const pdfPct = Math.round((formatCounts.pdf / totalFormatSum) * 100);
  const docxPct = Math.round((formatCounts.docx / totalFormatSum) * 100);
  const xlsxPct = Math.round((formatCounts.xlsx / totalFormatSum) * 100);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <PageHeader
        title="สถิติและการใช้งานระบบ (System Analytics & Telemetry)"
        description="ภาพรวมอัตราการสร้างเอกสาร, ความเร็วในการแปลงผล Gotenberg, และปริมาณงานของระบบ"
        icon={BarChart3}
        iconColor="sky"
        rightSlot={
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            loading={loading}
            onClick={refresh}
          >
            รีเฟรชข้อมูล
          </Button>
        }
      />

      {error && (
        <div className="p-3 bg-red-50 border border-red-200 text-red-700 text-xs rounded-sm flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 shrink-0 text-red-500" />
          <span>{error}</span>
        </div>
      )}

      {/* 4 KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Total Generated */}
        <div className="p-4 bg-surface border border-border rounded-md shadow-xs flex flex-col justify-between">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-textMuted">เอกสารที่สร้างทั้งหมด</span>
            <div className="w-7 h-7 rounded-sm bg-sky-50 text-sky-600 flex items-center justify-center border border-sky-200">
              <FileText className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-bold font-mono text-textPrimary tracking-tight">
              {totalGenerated.toLocaleString()}
            </div>
            <p className="text-[11px] text-emerald-600 mt-1 font-medium">บันทึกลง MinIO Storage</p>
          </div>
        </div>

        {/* Success Rate */}
        <div className="p-4 bg-surface border border-border rounded-md shadow-xs flex flex-col justify-between">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-textMuted">อัตราความสำเร็จ (SLA)</span>
            <div className="w-7 h-7 rounded-sm bg-emerald-50 text-emerald-600 flex items-center justify-center border border-emerald-200">
              <CheckCircle2 className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-bold font-mono text-textPrimary tracking-tight">
              {successRate}%
            </div>
            <p className="text-[11px] text-textMuted mt-1">เป้าหมายระบบ &gt;= 99.5%</p>
          </div>
        </div>

        {/* Latency */}
        <div className="p-4 bg-surface border border-border rounded-md shadow-xs flex flex-col justify-between">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-textMuted">ความเร็วเฉลี่ย (Avg Latency)</span>
            <div className="w-7 h-7 rounded-sm bg-amber-50 text-amber-600 flex items-center justify-center border border-amber-200">
              <Clock className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-bold font-mono text-textPrimary tracking-tight">
              {avgDurationMs} <span className="text-sm font-normal text-textMuted">ms</span>
            </div>
            <p className="text-[11px] text-textMuted mt-1">Gotenberg &amp; OpenXML engine</p>
          </div>
        </div>

        {/* Active Templates */}
        <div className="p-4 bg-surface border border-border rounded-md shadow-xs flex flex-col justify-between">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-textMuted">แม่แบบที่เปิดใช้งาน</span>
            <div className="w-7 h-7 rounded-sm bg-indigo-50 text-indigo-600 flex items-center justify-center border border-indigo-200">
              <Layers className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-bold font-mono text-textPrimary tracking-tight">
              {activeTemplatesCount} <span className="text-sm font-normal text-textMuted">แม่แบบ</span>
            </div>
            <p className="text-[11px] text-textMuted mt-1">พร้อมรองรับ API Calls</p>
          </div>
        </div>
      </div>

      {/* Grid: Throughput Breakdown & Caller Apps (2 Columns) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Output Format Throughput Breakdown */}
        <div className="lg:col-span-7 bg-surface border border-border rounded-md p-5 shadow-xs flex flex-col justify-between">
          <div>
            <h3 className="text-sm font-bold text-textPrimary">สัดส่วนการสร้างเอกสารแยกตามรูปแบบ (Format Distribution)</h3>
            <p className="text-xs text-textMuted mt-0.5">ปริมาณงานที่ประมวลผลผ่าน Gotenberg และ LibreOffice/OpenXML</p>
          </div>

          <div className="space-y-4 my-6">
            {/* PDF */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between text-xs">
                <span className="font-semibold text-textPrimary flex items-center gap-1.5">
                  <FileText className="w-3.5 h-3.5 text-rose-500" />
                  PDF Documents
                </span>
                <span className="font-mono text-textSecondary">{formatCounts.pdf} ไฟล์ ({pdfPct}%)</span>
              </div>
              <div className="w-full h-2 bg-slate-100 rounded-sm overflow-hidden">
                <div className="h-full bg-rose-500 rounded-sm transition-all duration-500" style={{ width: `${pdfPct}%` }} />
              </div>
            </div>

            {/* Word DOCX */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between text-xs">
                <span className="font-semibold text-textPrimary flex items-center gap-1.5">
                  <FileText className="w-3.5 h-3.5 text-blue-500" />
                  Microsoft Word (.docx)
                </span>
                <span className="font-mono text-textSecondary">{formatCounts.docx} ไฟล์ ({docxPct}%)</span>
              </div>
              <div className="w-full h-2 bg-slate-100 rounded-sm overflow-hidden">
                <div className="h-full bg-blue-500 rounded-sm transition-all duration-500" style={{ width: `${docxPct}%` }} />
              </div>
            </div>

            {/* Excel XLSX */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between text-xs">
                <span className="font-semibold text-textPrimary flex items-center gap-1.5">
                  <FileSpreadsheet className="w-3.5 h-3.5 text-emerald-500" />
                  Microsoft Excel (.xlsx)
                </span>
                <span className="font-mono text-textSecondary">{formatCounts.xlsx} ไฟล์ ({xlsxPct}%)</span>
              </div>
              <div className="w-full h-2 bg-slate-100 rounded-sm overflow-hidden">
                <div className="h-full bg-emerald-500 rounded-sm transition-all duration-500" style={{ width: `${xlsxPct}%` }} />
              </div>
            </div>
          </div>

          <div className="pt-3 border-t border-border flex items-center justify-between text-xs text-textMuted">
            <span>เครื่องมือเรนเดอร์: Chromium 132 / LibreOffice 24.8</span>
            <span className="font-mono">Gotenberg 8 Engine</span>
          </div>
        </div>

        {/* Caller Application Volume */}
        <div className="lg:col-span-5 bg-surface border border-border rounded-md p-5 shadow-xs flex flex-col justify-between">
          <div>
            <h3 className="text-sm font-bold text-textPrimary">ปริมาณเรียกใช้งานตาม Caller App</h3>
            <p className="text-xs text-textMuted mt-0.5">ระบบภายนอกและโมดูลภายในที่เชื่อมต่อ API</p>
          </div>

          <div className="space-y-3 my-4">
            {Object.keys(callerAppCounts).length === 0 ? (
              <p className="text-xs text-textMuted py-4 text-center">ยังไม่มีประวัติการเรียกใช้งาน</p>
            ) : (
              Object.entries(callerAppCounts).map(([app, count]) => (
                <div key={app} className="flex items-center justify-between p-2.5 rounded-sm bg-surfaceSubtle border border-border text-xs">
                  <div className="flex items-center gap-2">
                    <Server className="w-3.5 h-3.5 text-primary" />
                    <span className="font-mono font-semibold text-textPrimary">{app}</span>
                  </div>
                  <span className="font-mono text-sky-700 bg-sky-50 px-2 py-0.5 rounded-sm border border-sky-200">
                    {count} ครั้ง
                  </span>
                </div>
              ))
            )}
          </div>

          <div className="pt-3 border-t border-border text-[11px] text-textMuted">
            ตรวจสอบสิทธิ์และสร้าง Key ได้ที่เมนู <strong>API Keys</strong>
          </div>
        </div>
      </div>

      {/* Recent Generation Logs Feed */}
      <div className="bg-surface border border-border rounded-md p-5 shadow-xs space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-sm font-bold text-textPrimary">บันทึกการสร้างเอกสารล่าสุด (Recent Activity Feed)</h3>
            <p className="text-xs text-textMuted mt-0.5">การประมวลผล 10 รายการล่าสุดในระบบ</p>
          </div>
          <a
            href="/logs"
            className="text-xs font-semibold text-primary hover:underline underline-offset-2"
          >
            ดูทั้งหมดใน Logs &rarr;
          </a>
        </div>

        {recentLogs.length === 0 ? (
          <div className="py-8 text-center text-xs text-textMuted">
            {loading ? 'กำลังโหลดข้อมูล...' : 'ยังไม่มีประวัติการสร้างเอกสาร'}
          </div>
        ) : (
          <div className="divide-y divide-border">
            {recentLogs.map((log) => (
              <div key={log.id} className="py-2.5 flex items-center justify-between text-xs">
                <div className="flex items-center gap-3">
                  <Badge
                    status={log.status === 'success' ? 'success' : 'failed'}
                    dot
                    size="sm"
                  >
                    {log.status === 'success' ? 'สำเร็จ' : 'ล้มเหลว'}
                  </Badge>

                  <div>
                    <span className="font-mono font-semibold text-textPrimary">
                      {log.callerApp || 'direct-portal'}
                    </span>
                    <span className="text-textMuted ml-2 text-[11px]">
                      {new Date(log.createdAt).toLocaleTimeString('th-TH')}
                    </span>
                  </div>
                </div>

                <div className="flex items-center gap-3">
                  <Badge
                    format={(log.outputFormat?.toLowerCase() || 'pdf') as DocumentFormat}
                    size="sm"
                  >
                    {(log.outputFormat || 'pdf').toUpperCase()}
                  </Badge>
                  <span className="font-mono text-[11px] text-textMuted">
                    {log.durationMs || 0} ms
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};


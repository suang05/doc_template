'use client';

import React, { useState, useEffect } from 'react';
import {
  Settings,
  Server,
  Database,
  Key,
  Palette,
  RefreshCw,
  HardDrive,
  FileText,
} from 'lucide-react';
import { getStoredApiKey, setStoredApiKey } from '@/lib/api/client';
import { checkHealth as checkHealthApi } from '../api/health.api';
import { tokens } from '@/tokens';
import {
  CardBlock,
  Input,
  Button,
  Badge,
  PageHeader,
  Tabs,
} from '@/components/ui';
import type { SystemStatus, HealthCheck, HealthStatus, HealthReport } from '@/types/api';

function toSystemStatus(s: HealthStatus | null): SystemStatus {
  if (s === 'healthy') return 'success';
  if (s === 'degraded') return 'pending';
  if (s === 'unhealthy') return 'failed';
  return 'idle';
}

function statusLabel(s: HealthStatus | null): string {
  if (s === 'healthy') return 'Healthy';
  if (s === 'degraded') return 'Degraded';
  if (s === 'unhealthy') return 'Unhealthy';
  return 'Unchecked';
}

export const SettingsView: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'health' | 'env' | 'tokens'>('health');
  const [apiKey, setApiKey] = useState('');
  const [apiUrl, setApiUrl] = useState('');
  const [saved, setSaved] = useState(false);
  const [checkingHealth, setCheckingHealth] = useState(false);
  const [healthReport, setHealthReport] = useState<HealthReport | null>(null);
  const [apiReachable, setApiReachable] = useState<boolean | null>(null);

  const settingsTabs = [
    { id: 'health', label: '1. สถานะบริการ (Health & Diagnostics)' },
    { id: 'env',    label: '2. สภาพแวดล้อม (Environment & Keys)' },
    { id: 'tokens', label: '3. โทเคนการออกแบบ (Design Tokens)' },
  ];

  useEffect(() => {
    setApiKey(getStoredApiKey());
    setApiUrl(process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080');
  }, []);

  const checkHealth = async () => {
    setCheckingHealth(true);
    setHealthReport(null);
    try {
      const data = await checkHealthApi();
      setApiReachable(true);
      setHealthReport(data);
    } catch {
      setApiReachable(false);
    } finally {
      setCheckingHealth(false);
    }
  };

  const handleSaveApiKey = () => {
    setStoredApiKey(apiKey);
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  };

  const getCheck = (name: string): HealthCheck | undefined =>
    healthReport?.checks.find(c => c.name === name);

  const apiStatus: SystemStatus =
    apiReachable === true ? 'success' : apiReachable === false ? 'failed' : 'idle';
  const apiStatusLabel =
    apiReachable === true ? 'Online' : apiReachable === false ? 'Offline' : 'Unchecked';

  const pgCheck = getCheck('postgres');
  const gotenbergCheck = getCheck('gotenberg');
  const minioCheck = getCheck('minio');

  const serviceCards = [
    {
      label: 'Backend API (.NET 10)',
      icon: <Server className="w-3.5 h-3.5 text-sky-600" />,
      status: apiStatus,
      statusText: apiStatusLabel,
      detail: apiUrl,
      durationMs: healthReport?.totalDurationMs,
    },
    {
      label: 'PostgreSQL 15',
      icon: <Database className="w-3.5 h-3.5 text-indigo-600" />,
      status: toSystemStatus(pgCheck?.status ?? null),
      statusText: statusLabel(pgCheck?.status ?? null),
      detail: pgCheck?.description ?? 'Database connection',
      durationMs: pgCheck?.durationMs,
    },
    {
      label: 'Gotenberg 8 (PDF)',
      icon: <FileText className="w-3.5 h-3.5 text-amber-600" />,
      status: toSystemStatus(gotenbergCheck?.status ?? null),
      statusText: statusLabel(gotenbergCheck?.status ?? null),
      detail: gotenbergCheck?.description ?? 'Chromium PDF engine',
      durationMs: gotenbergCheck?.durationMs,
    },
    {
      label: 'MinIO Storage',
      icon: <HardDrive className="w-3.5 h-3.5 text-emerald-600" />,
      status: toSystemStatus(minioCheck?.status ?? null),
      statusText: statusLabel(minioCheck?.status ?? null),
      detail: minioCheck?.description ?? 'Object storage buckets',
      durationMs: minioCheck?.durationMs,
    },
  ];

  return (
    <div className="space-y-4 max-w-4xl mx-auto">
      {/* Header */}
      <PageHeader
        title="การตั้งค่าระบบ (System Settings & Diagnostics)"
        description="ตรวจสอบสถานะการเชื่อมต่อบริการ, กำหนดสภาพแวดล้อม และตรวจสอบโทเคนการออกแบบ"
        icon={Settings}
        iconColor="slate"
        rightSlot={
          <>
            {healthReport && (
              <span className="text-[11px] text-textMuted font-mono mr-1">
                ใช้เวลา {healthReport.totalDurationMs} ms
              </span>
            )}
            <Button
              variant="outline"
              size="sm"
              icon={RefreshCw}
              loading={checkingHealth}
              onClick={checkHealth}
            >
              ตรวจสอบสถานะระบบ
            </Button>
          </>
        }
      />

      {/* Tabs Navigation */}
      <div className="bg-surface border border-border rounded-sm overflow-hidden">
        <Tabs
          tabs={settingsTabs}
          activeTab={activeTab}
          onChange={(id) => setActiveTab(id as 'health' | 'env' | 'tokens')}
        />
      </div>

      {/* Tab 1: Services Health Status */}
      {activeTab === 'health' && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
            {serviceCards.map(card => (
              <CardBlock key={card.label} className="p-3.5 space-y-2">
                <div className="flex items-center justify-between gap-1">
                  <span className="text-xs font-semibold text-textPrimary flex items-center gap-1.5 min-w-0">
                    {card.icon}
                    <span className="truncate">{card.label}</span>
                  </span>
                  <Badge status={card.status} dot size="sm" className="shrink-0">
                    {card.statusText}
                  </Badge>
                </div>
                <p className="text-[11px] font-mono text-textMuted truncate">{card.detail}</p>
                {card.durationMs !== undefined && (
                  <p className="text-[10px] text-textMuted">{card.durationMs} ms</p>
                )}
              </CardBlock>
            ))}
          </div>

          <CardBlock className="p-4 bg-slate-50 border border-border text-xs text-textSecondary space-y-2">
            <h4 className="font-bold text-textPrimary">ข้อมูลสถาปัตยกรรมระบบ (Architecture Specifications):</h4>
            <ul className="list-disc pl-5 space-y-1 text-[11.5px]">
              <li><strong>Backend Gateway:</strong> .NET 10 Web API พร้อม RFC 9457 Problem Details Exception Handler</li>
              <li><strong>Persistence:</strong> PostgreSQL 15 พร้อม EF Core Global Query Filter แยก Tenant ปลอดภัย 100%</li>
              <li><strong>Document Renderer:</strong> Gotenberg 8 (Chromium 132 Engine สำหรับ PDF และ LibreOffice 24.8 สำหรับ Office)</li>
              <li><strong>Object Storage:</strong> MinIO S3-compatible พร้อม Bounded 81,920-byte Streaming Egress</li>
            </ul>
          </CardBlock>
        </div>
      )}

      {/* Tab 2: Environment & API Keys */}
      {activeTab === 'env' && (
        <div className="space-y-4">
          <CardBlock className="p-4 space-y-3">
            <div className="flex items-center gap-2 text-xs font-bold text-textPrimary border-b border-border pb-2">
              <Server className="w-4 h-4 text-primary" />
              <span>สภาพแวดล้อมระบบ (Environment Endpoints)</span>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs">
              <div className="p-2.5 rounded-sm bg-surfaceSubtle border border-border space-y-1">
                <span className="text-[11px] text-textMuted font-medium">Backend API URL</span>
                <p className="font-mono text-textPrimary font-semibold">{apiUrl}</p>
              </div>
              <div className="p-2.5 rounded-sm bg-surfaceSubtle border border-border space-y-1">
                <span className="text-[11px] text-textMuted font-medium">Server Proxy Route</span>
                <p className="font-mono text-textPrimary font-semibold">/api/proxy/[...path]</p>
              </div>
            </div>
          </CardBlock>

          <CardBlock className="p-4 space-y-3">
            <div className="flex items-center gap-2 text-xs font-bold text-textPrimary border-b border-border pb-2">
              <Key className="w-4 h-4 text-primary" />
              <span>กำหนด Master API Key ประจำบราวเซอร์ (Developer Testing)</span>
            </div>

            <p className="text-xs text-textSecondary">
              กุญแจนี้จะถูกบันทึกไว้ใน LocalStorage เพื่อใช้ทดสอบเรียก API ในกรณีที่ไม่ผ่าน Session Login:
            </p>

            <div className="flex items-end gap-2 max-w-lg">
              <div className="flex-1">
                <Input
                  label="Active Master Key"
                  type="password"
                  placeholder="smk_..."
                  value={apiKey}
                  onChange={(e) => setApiKey(e.target.value)}
                />
              </div>
              <Button variant="primary" size="md" onClick={handleSaveApiKey}>
                {saved ? 'บันทึกแล้ว ✓' : 'บันทึกคีย์'}
              </Button>
            </div>
          </CardBlock>
        </div>
      )}

      {/* Tab 3: SSoT Design Tokens Palette Reference */}
      {activeTab === 'tokens' && (
        <CardBlock className="p-4 space-y-3">
          <div className="flex items-center gap-2 text-xs font-bold text-textPrimary border-b border-border pb-2">
            <Palette className="w-4 h-4 text-indigo-600" />
            <span>SSoT Design Token Color Reference (ตาม DESIGN.md)</span>
          </div>

          <p className="text-xs text-textSecondary">
            สี Functional Categories ทั้ง 5 หมวดหมู่ ตามระเบียบข้อบังคับการออกแบบ ไม่มีสีกรมท่าเดิม:
          </p>

          <div className="grid grid-cols-1 sm:grid-cols-5 gap-2 pt-1">
            {Object.entries(tokens.categories).map(([key, cat]) => (
              <div
                key={key}
                className="p-2.5 rounded-sm border flex flex-col justify-between space-y-2"
                style={{
                  backgroundColor: cat.light,
                  borderColor: cat.border,
                }}
              >
                <div className="flex items-center justify-between">
                  <span className="w-3 h-3 rounded-full" style={{ backgroundColor: cat.primary }} />
                  <span className="text-[10px] font-mono text-textMuted">{cat.primary}</span>
                </div>
                <div>
                  <p className="text-xs font-bold" style={{ color: cat.text }}>
                    {cat.label}
                  </p>
                  <p className="text-[10px] text-textMuted uppercase font-mono">{key}</p>
                </div>
              </div>
            ))}
          </div>

          <div className="pt-3 border-t border-border flex items-center justify-between text-xs text-textMuted">
            <span>Primary Brand: #0284c7 (Sky-600) • Canvas: #f8fbfe</span>
            <span>Typography: Sarabun (Thai) + Inter (Latin)</span>
          </div>
        </CardBlock>
      )}
    </div>
  );
};

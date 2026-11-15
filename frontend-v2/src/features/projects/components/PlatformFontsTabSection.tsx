'use client';

import React, { useEffect, useRef, useState } from 'react';
import { RefreshCw, Upload } from 'lucide-react';
import {
  Badge,
  Button,
  Input,
  Modal,
  Select,
  Table,
  Column,
} from '@/components/ui';
import { ApiError, extractErrorMessage } from '@/lib/api/client';
import type { PlatformFontDto } from '@/lib/api/types';
import type { RegisterPlatformFontPayload } from '../api/projects.api';
import {
  ACCEPTED_FONT_EXTENSIONS,
  FONT_STYLE_OPTIONS,
  FONT_WEIGHT_OPTIONS,
  MAX_PLATFORM_FONT_BYTES,
  registerPlatformFontSchema,
  toFieldErrorMap,
} from '../schemas/project.schema';

interface PlatformFontsTabSectionProps {
  fonts: PlatformFontDto[];
  loading: boolean;
  uploading: boolean;
  onLoadFonts: (projectId?: string) => Promise<void>;
  onUploadFont: (
    payload: RegisterPlatformFontPayload,
    idempotencyKey: string,
  ) => Promise<PlatformFontDto>;
  onNotify: (type: 'success' | 'error' | 'info', message: string) => void;
}

export const PlatformFontsTabSection: React.FC<PlatformFontsTabSectionProps> = ({
  fonts,
  loading,
  uploading,
  onLoadFonts,
  onUploadFont,
  onNotify,
}) => {
  const [isUploadOpen, setIsUploadOpen] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [familyName, setFamilyName] = useState('');
  const [weight, setWeight] = useState('normal');
  const [style, setStyle] = useState<'normal' | 'italic' | 'oblique'>('normal');
  const [fileError, setFileError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [loadError, setLoadError] = useState<string | null>(null);
  const idempotencyKeyRef = useRef<string | null>(null);

  useEffect(() => {
    setLoadError(null);
    onLoadFonts().catch((err) => {
      setLoadError(extractErrorMessage(err, 'โหลดรายการฟอนต์แพลตฟอร์มไม่สำเร็จ'));
    });
  }, [onLoadFonts]);

  const openUploadModal = () => {
    setSelectedFile(null);
    setFamilyName('');
    setWeight('normal');
    setStyle('normal');
    setFileError(null);
    setFieldErrors({});
    idempotencyKeyRef.current = crypto.randomUUID();
    setIsUploadOpen(true);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0] ?? null;
    if (!file) {
      setSelectedFile(null);
      return;
    }

    const lower = file.name.toLowerCase();
    const hasAcceptedExt = ACCEPTED_FONT_EXTENSIONS.some((ext) => lower.endsWith(ext));
    if (!hasAcceptedExt) {
      setFileError('รองรับเฉพาะไฟล์ .ttf, .otf, .woff หรือ .woff2 เท่านั้น');
      setSelectedFile(null);
      return;
    }
    if (file.size > MAX_PLATFORM_FONT_BYTES) {
      setFileError('ขนาดไฟล์ฟอนต์ต้องไม่เกิน 8 MB');
      setSelectedFile(null);
      return;
    }

    setFileError(null);
    setSelectedFile(file);
  };

  const handleUploadSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedFile) {
      setFileError('กรุณาเลือกไฟล์ฟอนต์ที่ต้องการอัปโหลด');
      return;
    }

    const parsed = registerPlatformFontSchema.safeParse({
      familyName: familyName.trim() || undefined,
      weight,
      style,
    });
    if (!parsed.success) {
      setFieldErrors(toFieldErrorMap(parsed.error));
      return;
    }

    setFieldErrors({});
    const attemptKey = idempotencyKeyRef.current ?? crypto.randomUUID();
    idempotencyKeyRef.current = attemptKey;

    try {
      const created = await onUploadFont(
        {
          file: selectedFile,
          familyName: parsed.data.familyName,
          weight: parsed.data.weight,
          style: parsed.data.style,
        },
        attemptKey,
      );
      setIsUploadOpen(false);
      idempotencyKeyRef.current = null;
      onNotify('success', `ลงทะเบียนฟอนต์ "${created.familyName}" สำเร็จ`);
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        setFieldErrors(err.fieldErrors);
      }
      onNotify('error', extractErrorMessage(err, 'อัปโหลดฟอนต์แพลตฟอร์มไม่สำเร็จ'));
    }
  };

  const columns: Column<PlatformFontDto>[] = [
    {
      header: 'ตระกูลฟอนต์ (Family)',
      accessor: (item) => (
        <div className="space-y-0.5">
          <p className="text-xs font-semibold text-textPrimary">{item.familyName}</p>
          <p className="text-[10px] font-mono text-textMuted">{item.storageKey}</p>
        </div>
      ),
    },
    {
      header: 'Weight / Style',
      accessor: (item) => (
        <span className="font-mono text-[11px] text-textSecondary">
          {item.weight} / {item.style}
        </span>
      ),
      align: 'center',
    },
    {
      header: 'Format',
      accessor: (item) => (
        <span className="font-mono text-[11px] uppercase px-2 py-0.5 rounded-xs bg-slate-100 border border-slate-200">
          {item.format}
        </span>
      ),
      align: 'center',
    },
    {
      header: 'ขอบเขต',
      accessor: (item) => (
        <Badge status={item.isSystemFont ? 'success' : 'idle'} size="sm">
          {item.isSystemFont ? 'System-Wide' : 'Tenant Scoped'}
        </Badge>
      ),
      align: 'center',
    },
    {
      header: 'SHA-256',
      accessor: (item) => (
        <span className="font-mono text-[10px] text-textMuted" title={item.checksumSha256}>
          {item.checksumSha256.slice(0, 14)}...
        </span>
      ),
    },
    {
      header: 'ลงทะเบียนเมื่อ',
      accessor: (item) => (
        <span className="text-[11px] font-mono text-textMuted">
          {new Date(item.createdAt).toLocaleString('th-TH')}
        </span>
      ),
    },
  ];

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-2 flex-wrap">
        <p className="text-xs text-textSecondary">
          ฟอนต์มาตรฐานส่วนกลางที่เปิดให้ทุกเทแนนต์ใช้งานระหว่างสร้างเอกสาร PDF (รองรับ .ttf, .otf, .woff, .woff2 สูงสุด 8 MB)
        </p>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            loading={loading}
            onClick={() => void onLoadFonts()}
          >
            รีเฟรช
          </Button>
          <Button variant="primary" size="sm" icon={Upload} onClick={openUploadModal}>
            ลงทะเบียนฟอนต์ระบบ
          </Button>
        </div>
      </div>

      {loadError && (
        <div className="p-3 bg-red-50 border border-red-200 text-red-700 text-xs rounded-sm">
          {loadError}
        </div>
      )}

      <Table
        columns={columns}
        data={fonts}
        keyExtractor={(item) => item.id}
        loading={loading}
        emptyMessage="ยังไม่มีฟอนต์ระดับแพลตฟอร์ม"
      />

      <Modal
        isOpen={isUploadOpen}
        onClose={() => setIsUploadOpen(false)}
        title="ลงทะเบียน Platform Font (Idempotent Multipart)"
        maxWidth="md"
      >
        <form onSubmit={handleUploadSubmit} className="space-y-3">
          <div className="space-y-1">
            <label className="block text-xs font-medium text-textSecondary">
              ไฟล์ฟอนต์ (.ttf, .otf, .woff, .woff2 สูงสุด 8 MB)
            </label>
            <input
              type="file"
              accept=".ttf,.otf,.woff,.woff2"
              onChange={handleFileChange}
              className="block w-full text-xs text-textSecondary file:mr-3 file:py-1.5 file:px-3 file:rounded-sm file:border file:border-border file:text-xs file:font-medium file:bg-surfaceSubtle file:text-textPrimary hover:file:bg-slate-200/70"
            />
            {fileError && <p className="text-[11px] text-red-600">{fileError}</p>}
          </div>

          <Input
            label="Font Family Name (ไม่บังคับ — อ่านจากชื่อไฟล์หากเว้นว่าง)"
            value={familyName}
            onChange={(e) => setFamilyName(e.target.value)}
            placeholder="เช่น Sarabun"
            error={fieldErrors.familyName}
          />

          <div className="grid grid-cols-2 gap-3">
            <Select
              label="Weight"
              value={weight}
              options={[...FONT_WEIGHT_OPTIONS]}
              onChange={(e) => setWeight(e.target.value)}
              error={fieldErrors.weight}
            />
            <Select
              label="Style"
              value={style}
              options={[...FONT_STYLE_OPTIONS]}
              onChange={(e) =>
                setStyle(e.target.value as 'normal' | 'italic' | 'oblique')
              }
              error={fieldErrors.style}
            />
          </div>

          <div className="flex justify-end gap-2 pt-2 border-t border-border">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setIsUploadOpen(false)}
            >
              ยกเลิก
            </Button>
            <Button type="submit" size="sm" loading={uploading}>
              อัปโหลดและลงทะเบียน
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
};

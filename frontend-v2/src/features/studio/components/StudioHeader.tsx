import React from 'react';
import {
  ArrowLeft,
  Upload,
  History,
  CheckCircle,
  Save,
} from 'lucide-react';
import { Button } from '@/components/ui';

export interface StudioHeaderProps {
  templateName?: string;
  isDirty: boolean;
  currentVersion: number;
  versionsCount: number;
  validating: boolean;
  fileInputRef: React.RefObject<HTMLInputElement | null>;
  onBack: () => void;
  onFileImport: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onOpenHistory: () => void;
  onValidate: () => void;
  onOpenSave: () => void;
}

export const StudioHeader: React.FC<StudioHeaderProps> = ({
  templateName,
  isDirty,
  currentVersion,
  versionsCount,
  validating,
  fileInputRef,
  onBack,
  onFileImport,
  onOpenHistory,
  onValidate,
  onOpenSave,
}) => {
  return (
    <div className="h-11 bg-surfaceSubtle border-b border-border px-3 flex items-center justify-between shrink-0">
      <div className="flex items-center gap-2">
        <button
          onClick={onBack}
          className="p-1 rounded-sm text-textSecondary hover:text-textPrimary hover:bg-slate-200/50 transition-colors cursor-pointer"
          title="กลับหน้ารายการ"
        >
          <ArrowLeft className="w-4 h-4" />
        </button>

        <span className="text-xs font-bold text-textPrimary flex items-center gap-1">
          {templateName || 'สตูดิโอแม่แบบ (ยังไม่บันทึก)'}
          {isDirty && <span className="text-amber-500 text-sm leading-none" title="มีการแก้ไขที่ยังไม่บันทึก">*</span>}
        </span>

        <span className="text-[11px] font-mono text-textMuted bg-surface px-1.5 py-0.5 rounded-sm border border-border">
          v{currentVersion}
        </span>
      </div>

      <div className="flex items-center gap-2">
        <input
          type="file"
          ref={fileInputRef}
          accept=".html,.htm"
          className="hidden"
          onChange={onFileImport}
        />

        <Button
          variant="outline"
          size="sm"
          icon={Upload}
          onClick={() => fileInputRef.current?.click()}
          title="เลือกไฟล์ .html จากเครื่องแล้วใส่ลง Monaco Editor"
        >
          นำเข้า HTML
        </Button>

        <Button
          variant="ghost"
          size="sm"
          icon={History}
          onClick={onOpenHistory}
        >
          ประวัติ (v{versionsCount})
        </Button>

        <Button
          variant="secondary"
          size="sm"
          icon={CheckCircle}
          loading={validating}
          onClick={onValidate}
        >
          ตรวจสอบโค้ด
        </Button>

        <Button
          variant="primary"
          size="sm"
          icon={Save}
          disabled={!isDirty}
          onClick={onOpenSave}
        >
          บันทึก v{currentVersion + 1}
        </Button>
      </div>
    </div>
  );
};


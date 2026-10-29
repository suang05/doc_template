import React from 'react';
import { Save, AlertCircle } from 'lucide-react';
import { CardBlock, Input, Select, Button } from '@/components/ui';
import { DocumentCategory } from '@/tokens';

export interface UploadConfirmStepProps {
  name: string;
  slug: string;
  category: DocumentCategory;
  categories: { value: string; label: string }[];
  fileName?: string;
  mappingCount: number;
  saving: boolean;
  error: string | null;
  onNameChange: (val: string) => void;
  onSlugChange: (val: string) => void;
  onCategoryChange: (cat: DocumentCategory) => void;
  onCommit: () => void;
}

export const UploadConfirmStep: React.FC<UploadConfirmStepProps> = ({
  name,
  slug,
  category,
  categories,
  fileName,
  mappingCount,
  saving,
  error,
  onNameChange,
  onSlugChange,
  onCategoryChange,
  onCommit,
}) => {
  return (
    <>
      <CardBlock className="p-4 space-y-4">
        <h3 className="text-xs font-bold text-textPrimary border-b border-border pb-2">
          ข้อมูลแม่แบบ
        </h3>
        <Input
          label="ชื่อแม่แบบ"
          value={name}
          onChange={(e) => onNameChange(e.target.value)}
          placeholder="เช่น สัญญาจ้างงานทั่วไป"
          required
        />
        <Input
          label="Slug"
          value={slug}
          onChange={(e) => onSlugChange(e.target.value)}
          placeholder="เช่น general-contract"
          helperText="รหัสเรียก API — ต้องไม่ซ้ำ"
          required
        />
        <Select
          label="หมวดหมู่"
          options={categories}
          value={category}
          onChange={(e) => onCategoryChange(e.target.value as DocumentCategory)}
        />
      </CardBlock>

      {/* Summary */}
      <CardBlock className="p-4 space-y-2">
        <h3 className="text-xs font-bold text-textPrimary border-b border-border pb-2">
          สรุป
        </h3>
        <div className="flex items-center justify-between text-xs">
          <span className="text-textMuted">ไฟล์</span>
          <span className="font-medium text-textPrimary truncate ml-2">{fileName}</span>
        </div>
        <div className="flex items-center justify-between text-xs">
          <span className="text-textMuted">Placeholder</span>
          <span className="font-medium text-textPrimary">{mappingCount} รายการ</span>
        </div>
      </CardBlock>

      {error && (
        <div className="p-2.5 bg-red-50 border border-red-200 text-red-800 text-xs rounded-sm flex items-center gap-2 shrink-0">
          <AlertCircle className="w-4 h-4 shrink-0 text-red-600" />
          <span>{error}</span>
        </div>
      )}

      <div className="flex items-center justify-end gap-2 pt-1 shrink-0">
        <Button
          variant="primary"
          icon={Save}
          onClick={onCommit}
          loading={saving}
          disabled={!name || !slug}
        >
          บันทึก
        </Button>
      </div>
    </>
  );
};


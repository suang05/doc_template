import React from 'react';
import { FileText, RefreshCw, Play } from 'lucide-react';
import { Button } from '@/components/ui';

export interface GeneratorActionBarProps {
  generating: boolean;
  previewLoading: boolean;
  onRefreshPreview: () => void;
  onGenerate: () => void;
}

export const GeneratorActionBar: React.FC<GeneratorActionBarProps> = ({
  generating,
  previewLoading,
  onRefreshPreview,
  onGenerate,
}) => {
  return (
    <div className="flex items-center justify-between pb-3 border-b border-border shrink-0">
      <div className="flex items-center gap-3">
        <div className="w-8 h-8 rounded-sm bg-sky-50 text-sky-600 flex items-center justify-center border border-sky-200 shrink-0">
          <FileText className="w-4 h-4" />
        </div>
        <div className="flex items-center flex-wrap gap-2">
          <span className="text-xs text-textMuted">
            เลือกแม่แบบ ➝ กรอกข้อมูล ➝ ดู Preview ➝ สร้างเอกสารจริง
          </span>
        </div>
      </div>

      <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="md"
          icon={RefreshCw}
          loading={previewLoading}
          onClick={onRefreshPreview}
        >
          รีเฟรช Preview
        </Button>
        <Button
          variant="primary"
          size="md"
          icon={Play}
          loading={generating}
          onClick={onGenerate}
        >
          สร้างเอกสาร
        </Button>
      </div>
    </div>
  );
};


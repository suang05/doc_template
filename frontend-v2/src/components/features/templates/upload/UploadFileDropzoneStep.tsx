import React from 'react';
import { UploadCloud, X, AlertCircle, ChevronRight } from 'lucide-react';
import { CardBlock, Button } from '@/components/ui';

export interface UploadFileDropzoneStepProps {
  file: File | null;
  isDragging: boolean;
  uploading: boolean;
  error: string | null;
  fileInputRef: React.RefObject<HTMLInputElement | null>;
  onDragOver: (e: React.DragEvent) => void;
  onDragLeave: () => void;
  onDrop: (e: React.DragEvent) => void;
  onFileChange: (file: File | null) => void;
  onParse: () => void;
  onCancel?: () => void;
  getFileIcon: (filename: string) => React.ReactNode;
}

export const UploadFileDropzoneStep: React.FC<UploadFileDropzoneStepProps> = ({
  file,
  isDragging,
  uploading,
  error,
  fileInputRef,
  onDragOver,
  onDragLeave,
  onDrop,
  onFileChange,
  onParse,
  onCancel,
  getFileIcon,
}) => {
  return (
    <>
      <CardBlock className="p-4 flex flex-col gap-3">
        <h3 className="text-xs font-bold text-textPrimary border-b border-border pb-2">
          ไฟล์แม่แบบ Office
        </h3>
        <div
          className={`flex flex-col items-center justify-center border-2 border-dashed rounded-sm p-6 transition-colors cursor-pointer min-h-[200px] ${
            isDragging
              ? 'border-indigo-500 bg-indigo-50/50'
              : 'border-slate-300 bg-surfaceSubtle hover:bg-slate-50 hover:border-slate-400'
          }`}
          onDragOver={onDragOver}
          onDragLeave={onDragLeave}
          onDrop={onDrop}
          onClick={() => !file && fileInputRef.current?.click()}
        >
          <input
            type="file"
            ref={fileInputRef}
            onChange={(e) => onFileChange(e.target.files?.[0] || null)}
            accept=".docx,.xlsx"
            className="hidden"
          />
          {!file ? (
            <div className="text-center flex flex-col items-center gap-2">
              <div className="w-12 h-12 rounded-sm bg-slate-100 flex items-center justify-center">
                <UploadCloud className="w-6 h-6 text-slate-400" />
              </div>
              <p className="text-sm font-semibold text-textPrimary">
                คลิก หรือ ลากไฟล์ Word / Excel มาวาง
              </p>
              <p className="text-[11px] text-textMuted">
                .docx (Word) · .xlsx (Excel) · สูงสุด 20 MB
              </p>
            </div>
          ) : (
            <div className="text-center flex flex-col items-center gap-2 w-full">
              {getFileIcon(file.name)}
              <p className="text-sm font-semibold text-textPrimary truncate w-full px-4">
                {file.name}
              </p>
              <p className="text-[11px] text-textMuted">{(file.size / 1024).toFixed(1)} KB</p>
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onFileChange(null);
                }}
                className="mt-1 text-xs font-medium text-red-600 hover:text-red-700 bg-red-50 hover:bg-red-100 px-3 py-1.5 rounded-sm flex items-center gap-1 transition-colors cursor-pointer"
              >
                <X className="w-3.5 h-3.5" /> ลบไฟล์
              </button>
            </div>
          )}
        </div>
      </CardBlock>

      {error && (
        <div className="p-2.5 bg-red-50 border border-red-200 text-red-800 text-xs rounded-sm flex items-center gap-2">
          <AlertCircle className="w-4 h-4 shrink-0 text-red-600" />
          <span>{error}</span>
        </div>
      )}

      <div className="flex items-center justify-end gap-2 pt-1">
        {onCancel && (
          <Button variant="outline" onClick={onCancel}>
            ยกเลิก
          </Button>
        )}
        <Button
          variant="primary"
          onClick={onParse}
          loading={uploading}
          icon={uploading ? undefined : ChevronRight}
          disabled={!file}
        >
          {uploading ? 'กำลังวิเคราะห์...' : 'วิเคราะห์ Field'}
        </Button>
      </div>
    </>
  );
};


import React from 'react';
import { Button, Modal } from '@/components/ui';

export interface StudioSaveModalProps {
  isOpen: boolean;
  nextVersion: number;
  changeNote: string;
  saving: boolean;
  onChangeNote: (note: string) => void;
  onConfirm: () => void;
  onClose: () => void;
}

export const StudioSaveModal: React.FC<StudioSaveModalProps> = ({
  isOpen,
  nextVersion,
  changeNote,
  saving,
  onChangeNote,
  onConfirm,
  onClose,
}) => {
  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={`ยืนยันการบันทึกแม่แบบ v${nextVersion}`}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            ยกเลิก
          </Button>
          <Button variant="primary" onClick={onConfirm} loading={saving}>
            บันทึกเวอร์ชันใหม่
          </Button>
        </>
      }
    >
      <div className="space-y-2">
        <p className="text-xs text-textSecondary">
          ระบบจะสร้างเวอร์ชันใหม่ (v{nextVersion}) และจัดเก็บประวัติการแก้ไขเดิมไว้ใน MinIO
        </p>
        <label className="block text-xs font-medium text-textPrimary">
          บันทึกข้อความสรุปการแก้ไข (Change Note):
        </label>
        <input
          type="text"
          className="w-full h-8 px-2.5 text-xs rounded-sm border border-border bg-surface outline-none focus:border-primary"
          placeholder="เช่น ปรับตำแหน่งลายเซ็น, แก้ไขรูปแบบวันที่..."
          value={changeNote}
          onChange={(e) => onChangeNote(e.target.value)}
        />
      </div>
    </Modal>
  );
};


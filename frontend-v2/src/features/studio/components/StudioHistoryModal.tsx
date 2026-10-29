import React from 'react';
import { Modal } from '@/components/ui';
import { TemplateVersionDto } from '@/types/api';

export interface StudioHistoryModalProps {
  isOpen: boolean;
  versions: TemplateVersionDto[];
  onClose: () => void;
}

export const StudioHistoryModal: React.FC<StudioHistoryModalProps> = ({
  isOpen,
  versions,
  onClose,
}) => {
  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="ประวัติการแก้ไขแม่แบบ (Revision History)"
      maxWidth="lg"
    >
      <div className="space-y-2">
        {versions.length === 0 ? (
          <p className="text-xs text-textMuted text-center py-4">ไม่พบประวัติเวอร์ชันเดิม</p>
        ) : (
          <div className="divide-y divide-border border border-border rounded-sm">
            {versions.map((v) => (
              <div key={v.id} className="p-2.5 flex items-center justify-between text-xs hover:bg-slate-50">
                <div className="space-y-0.5">
                  <div className="flex items-center gap-2">
                    <span className="font-bold text-textPrimary">v{v.version}</span>
                    <span className="text-textMuted text-[11px]">
                      {new Date(v.createdAt).toLocaleString('th-TH')}
                    </span>
                  </div>
                  <p className="text-textSecondary text-[11px]">{v.commitMessage || 'ไม่มีรายละเอียด'}</p>
                </div>
                <span className="text-[10px] font-mono text-textMuted bg-surfaceSubtle px-1.5 py-0.5 border rounded-xs">
                  {v.storageKey.split('/').pop()}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>
    </Modal>
  );
};


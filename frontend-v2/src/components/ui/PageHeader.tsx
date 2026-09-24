import React from 'react';
import { LucideIcon } from 'lucide-react';

export interface PageHeaderProps {
  title: string;
  description?: string;
  icon?: LucideIcon;
  iconColor?: 'indigo' | 'sky' | 'emerald' | 'amber' | 'slate';
  rightSlot?: React.ReactNode;
}

export const PageHeader: React.FC<PageHeaderProps> = ({
  title,
  description,
  icon: Icon,
  iconColor = 'indigo',
  rightSlot,
}) => {
  const colorMap = {
    indigo: 'bg-indigo-50 text-indigo-600 border-indigo-200',
    sky: 'bg-sky-50 text-sky-600 border-sky-200',
    emerald: 'bg-emerald-50 text-emerald-600 border-emerald-200',
    amber: 'bg-amber-50 text-amber-600 border-amber-200',
    slate: 'bg-slate-50 text-slate-600 border-slate-200',
  };

  return (
    <div className="bg-surface border border-border rounded-sm p-4 flex items-center justify-between">
      {/* ฝั่งซ้าย: Icon + Title + Description */}
      <div className="flex items-center gap-2.5">
        {Icon && (
          <div className={`w-8 h-8 rounded-sm flex items-center justify-center border ${colorMap[iconColor]}`}>
            <Icon className="w-4 h-4" />
          </div>
        )}
        <div>
          <h2 className="text-xs font-bold text-textPrimary">{title}</h2>
          {description && (
            <p className="text-[11px] text-textMuted">{description}</p>
          )}
        </div>
      </div>
      
      {/* ฝั่งขวา: รองรับ Component หลายๆ ตัว */}
      {rightSlot && (
        <div className="flex items-center gap-2">
          {rightSlot}
        </div>
      )}
    </div>
  );
};

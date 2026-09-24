import React from 'react';

export interface ToolbarProps {
  leftSlot?: React.ReactNode;
  rightSlot?: React.ReactNode;
  className?: string;
}

export const Toolbar: React.FC<ToolbarProps> = ({
  leftSlot,
  rightSlot,
  className = '',
}) => {
  return (
    <div
      className={`flex flex-wrap items-center justify-between gap-2.5 p-2 bg-surface border border-border rounded-sm ${className}`}
    >
      <div className="flex flex-wrap items-center gap-2 flex-1 min-w-[240px]">
        {leftSlot}
      </div>

      <div className="flex items-center gap-2 shrink-0">
        {rightSlot}
      </div>
    </div>
  );
};

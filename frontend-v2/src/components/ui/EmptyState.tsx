import React from 'react';
import { LucideIcon, Inbox } from 'lucide-react';
import { Button } from './Button';

export interface EmptyStateProps {
  icon?: LucideIcon;
  title: string;
  description?: string;
  actionLabel?: string;
  onAction?: () => void;
}

export const EmptyState: React.FC<EmptyStateProps> = ({
  icon: Icon = Inbox,
  title,
  description,
  actionLabel,
  onAction,
}) => {
  return (
    <div className="flex flex-col items-center justify-center p-8 text-center bg-surface border border-dashed border-border rounded-sm">
      <div className="w-10 h-10 rounded-sm bg-surfaceSubtle border border-border flex items-center justify-center text-textMuted mb-3">
        <Icon className="w-5 h-5" />
      </div>

      <h4 className="text-xs font-semibold text-textPrimary">{title}</h4>
      {description && (
        <p className="text-[11px] text-textMuted mt-1 max-w-sm">{description}</p>
      )}

      {actionLabel && onAction && (
        <div className="mt-4">
          <Button variant="outline" size="sm" onClick={onAction}>
            {actionLabel}
          </Button>
        </div>
      )}
    </div>
  );
};

import { ReactNode } from "react";

interface EmptyStateProps {
  icon?: ReactNode;
  title: string;
  description?: string;
  action?: ReactNode;
}

export function EmptyState({ icon, title, description, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 py-8 text-center">
      {icon && <div className="text-[var(--t3)] mb-1">{icon}</div>}
      <div className="text-[13px] font-semibold text-[var(--t2)]">{title}</div>
      {description && <div className="text-xs text-[var(--t3)] max-w-xs">{description}</div>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

import React from 'react';
import { tokens, DocumentCategory, DocumentFormat, SystemStatus } from '@/tokens';

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  category?: DocumentCategory;
  format?: DocumentFormat;
  status?: SystemStatus;
  dot?: boolean;
  size?: 'sm' | 'md';
}

export const Badge: React.FC<BadgeProps> = ({
  children,
  category,
  format,
  status,
  dot = false,
  size = 'md',
  className = '',
  ...props
}) => {
  let bgClass = 'bg-slate-100 text-slate-700 border-slate-200';
  let dotColor: string = tokens.statuses.idle.primary;

  if (category && tokens.categories[category]) {
    const cat = tokens.categories[category];
    bgClass = {
      contract: 'bg-indigo-50 text-indigo-700 border-indigo-200',
      financial: 'bg-emerald-50 text-emerald-700 border-emerald-200',
      official: 'bg-sky-50 text-sky-700 border-sky-200',
      hr: 'bg-amber-50 text-amber-800 border-amber-200',
      operations: 'bg-cyan-50 text-cyan-800 border-cyan-200',
    }[category];
    dotColor = cat.primary;
  } else if (format && tokens.formats[format]) {
    const fmt = tokens.formats[format];
    bgClass = {
      pdf: 'bg-rose-50 text-rose-700 border-rose-200 font-semibold',
      docx: 'bg-blue-50 text-blue-700 border-blue-200 font-semibold',
      xlsx: 'bg-emerald-50 text-emerald-700 border-emerald-200 font-semibold',
      html: 'bg-amber-50 text-amber-800 border-amber-200 font-semibold',
    }[format];
    dotColor = fmt.primary;
  } else if (status && tokens.statuses[status]) {
    const st = tokens.statuses[status];
    bgClass = {
      success: 'bg-emerald-50 text-emerald-700 border-emerald-200',
      failed: 'bg-red-50 text-red-700 border-red-200',
      pending: 'bg-amber-50 text-amber-800 border-amber-200',
      idle: 'bg-slate-50 text-slate-700 border-slate-200',
    }[status];
    dotColor = st.primary;
  }

  const sizeClasses = size === 'sm' ? 'px-1.5 py-0.5 text-[10px]' : 'px-2 py-0.5 text-xs';

  return (
    <span
      className={`inline-flex items-center gap-1.5 font-sans rounded-sm border ${sizeClasses} ${bgClass} ${className}`}
      {...props}
    >
      {dot && (
        <span
          className="w-1.5 h-1.5 rounded-full shrink-0"
          style={{ backgroundColor: dotColor }}
        />
      )}
      <span>{children}</span>
    </span>
  );
};

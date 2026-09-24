import React from 'react';
import { DocumentCategory, tokens } from '@/tokens';

export interface CardBlockProps extends React.HTMLAttributes<HTMLDivElement> {
  category?: DocumentCategory;
  hoverable?: boolean;
}

export const CardBlock: React.FC<CardBlockProps> = ({
  children,
  category,
  hoverable = false,
  className = '',
  ...props
}) => {
  const accentColor = category && tokens.categories[category] ? tokens.categories[category].primary : undefined;

  return (
    <div
      className={`relative bg-surface rounded-sm border border-border transition-all duration-150 ${
        hoverable ? 'hover:border-slate-300 hover:shadow-sm' : ''
      } ${className}`}
      {...props}
    >
      {accentColor && (
        <div
          className="h-1 w-full shrink-0 rounded-t-[1px]"
          style={{ backgroundColor: accentColor }}
        />
      )}
      {children}
    </div>
  );
};

import React from 'react';
import { LucideIcon } from 'lucide-react';
import { DocumentCategory, tokens } from '@/tokens';

export interface StatBlockProps {
  label: string;
  value: string | number;
  icon?: LucideIcon;
  category?: DocumentCategory;
  trend?: string;
  trendPositive?: boolean;
}

export const StatBlock: React.FC<StatBlockProps> = ({
  label,
  value,
  icon: Icon,
  category,
  trend,
  trendPositive = true,
}) => {
  const cat = category && tokens.categories[category] ? tokens.categories[category] : null;

  return (
    <div className="bg-surface rounded-sm border border-border p-4 flex items-center justify-between shadow-xs">
      <div className="space-y-1">
        <p className="text-xs font-medium text-textMuted uppercase tracking-wider">{label}</p>
        <div className="flex items-baseline gap-2">
          <span className="text-2xl font-bold tracking-tight text-textPrimary">{value}</span>
          {trend && (
            <span
              className={`text-xs font-medium ${
                trendPositive ? 'text-emerald-600' : 'text-rose-600'
              }`}
            >
              {trend}
            </span>
          )}
        </div>
      </div>

      {Icon && (
        <div
          className="w-10 h-10 rounded-sm flex items-center justify-center shrink-0 border"
          style={{
            backgroundColor: cat ? cat.light : tokens.colors.surfaceSubtle,
            borderColor: cat ? cat.border : tokens.colors.border,
            color: cat ? cat.primary : tokens.colors.textSecondary,
          }}
        >
          <Icon className="w-5 h-5" />
        </div>
      )}
    </div>
  );
};

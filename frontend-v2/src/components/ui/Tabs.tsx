import React from 'react';

export interface TabItem {
  id: string;
  label: string;
  count?: number;
}

export interface TabsProps {
  tabs: TabItem[];
  activeTab: string;
  onChange: (id: string) => void;
  className?: string;
}

export const Tabs: React.FC<TabsProps> = ({
  tabs,
  activeTab,
  onChange,
  className = '',
}) => {
  return (
    <div className={`flex items-center gap-1 border-b border-border bg-surfaceSubtle/50 px-2 pt-1 ${className}`}>
      {tabs.map((tab) => {
        const isActive = tab.id === activeTab;
        return (
          <button
            key={tab.id}
            onClick={() => onChange(tab.id)}
            className={`flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium border-b-2 transition-all cursor-pointer rounded-t-sm ${
              isActive
                ? 'border-primary text-primary bg-surface shadow-xs font-semibold'
                : 'border-transparent text-textMuted hover:text-textPrimary hover:bg-surface/50'
            }`}
          >
            <span>{tab.label}</span>
            {typeof tab.count === 'number' && (
              <span
                className={`px-1.5 py-0.2 rounded-[2px] text-[10px] font-mono ${
                  isActive
                    ? 'bg-primaryLight text-primaryDark'
                    : 'bg-slate-200/60 text-textSecondary'
                }`}
              >
                {tab.count}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
};

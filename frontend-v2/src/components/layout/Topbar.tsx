'use client';

import React, { useEffect } from 'react';
import {
  PanelLeftClose,
  PanelLeftOpen,
  ChevronLeft,
  ChevronRight,
  Home,
  Search,
  Bell,
  HelpCircle,
} from 'lucide-react';

export interface TopbarProps {
  isSidebarOpen: boolean;
  onToggleSidebar: () => void;
  onNavigateHome?: () => void;
  onOpenSearch?: () => void;
  activeViewTitle?: string;
}

export const Topbar: React.FC<TopbarProps> = ({
  isSidebarOpen,
  onToggleSidebar,
  onNavigateHome,
  onOpenSearch,
  activeViewTitle,
}) => {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        onOpenSearch?.();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onOpenSearch]);

  return (
    <header className="h-12 bg-surface border-b border-border px-3 flex items-center justify-between shrink-0 select-none z-20">
      {/* Left controls */}
      <div className="flex items-center gap-1.5">
        <button
          onClick={onToggleSidebar}
          className="p-1.5 rounded-sm text-textSecondary hover:text-textPrimary hover:bg-surfaceSubtle transition-colors cursor-pointer"
          title={isSidebarOpen ? 'ย่อแถบข้าง' : 'ขยายแถบข้าง'}
        >
          {isSidebarOpen ? (
            <PanelLeftClose className="w-4 h-4" />
          ) : (
            <PanelLeftOpen className="w-4 h-4" />
          )}
        </button>

        <div className="h-4 w-[1px] bg-border mx-1" />

        <button
          onClick={() => window.history.back()}
          className="p-1.5 rounded-sm text-textMuted hover:text-textPrimary hover:bg-surfaceSubtle transition-colors cursor-pointer"
          title="ย้อนกลับ"
        >
          <ChevronLeft className="w-4 h-4" />
        </button>

        <button
          onClick={() => window.history.forward()}
          className="p-1.5 rounded-sm text-textMuted hover:text-textPrimary hover:bg-surfaceSubtle transition-colors cursor-pointer"
          title="ไปข้างหน้า"
        >
          <ChevronRight className="w-4 h-4" />
        </button>

        {onNavigateHome && (
          <div className="flex items-center ml-1">
            <button
              onClick={onNavigateHome}
              className="p-1.5 rounded-sm text-textSecondary hover:text-primary hover:bg-surfaceSubtle transition-colors cursor-pointer flex items-center"
              title="หน้าแรก"
            >
              <Home className="w-4 h-4" />
            </button>
            
            {activeViewTitle && (
              <>
                <span className="text-textMuted text-xs select-none mx-1">/</span>
                <span className="text-xs font-medium text-textSecondary hover:text-textPrimary transition-colors cursor-default px-2 py-1 rounded-sm hover:bg-surfaceSubtle">
                  {activeViewTitle}
                </span>
              </>
            )}
          </div>
        )}
      </div>

      {/* Right controls */}
      <div className="flex items-center gap-2">
        {/* Active Project Context */}
        <div className="hidden sm:flex items-center gap-1.5 px-2.5 py-1 rounded-sm bg-surfaceSubtle border border-border text-[11px] text-textSecondary select-none">
          <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 shrink-0" />
          <span className="text-textMuted">Project:</span>
          <span className="font-semibold text-textPrimary">Default Workspace</span>
        </div>

        {/* Quick search shortcut */}
        <button
          onClick={onOpenSearch}
          className="hidden md:flex items-center gap-1.5 bg-surfaceSubtle border border-border rounded-sm px-2 py-1 text-xs text-textMuted hover:border-slate-300 hover:text-textPrimary transition-colors cursor-pointer"
        >
          <Search className="w-3.5 h-3.5" />
          <span className="text-[11px]">ค้นหา...</span>
          <kbd className="px-1 py-0.2 bg-surface rounded-xs border border-border text-[9px] font-mono">
            ⌘K
          </kbd>
        </button>

        <button
          className="p-1.5 rounded-sm text-textSecondary hover:text-textPrimary hover:bg-surfaceSubtle transition-colors relative cursor-pointer"
          title="การแจ้งเตือน"
        >
          <Bell className="w-4 h-4" />
          <span className="absolute top-1.5 right-1.5 w-1.5 h-1.5 bg-sky-500 rounded-full" />
        </button>

        <button
          className="p-1.5 rounded-sm text-textSecondary hover:text-textPrimary hover:bg-surfaceSubtle transition-colors cursor-pointer"
          title="คู่มือ"
        >
          <HelpCircle className="w-4 h-4" />
        </button>
      </div>
    </header>
  );
};

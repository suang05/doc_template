'use client';

import React, { useState, useEffect } from 'react';
import {
  PanelLeftClose,
  PanelLeftOpen,
  ChevronLeft,
  ChevronRight,
  Home,
  Key,
  Search,
  Bell,
  HelpCircle,
  Check,
} from 'lucide-react';
import { getStoredApiKey, setStoredApiKey } from '@/lib/api/client';

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
  const [apiKey, setApiKey] = useState('');
  const [keySaved, setKeySaved] = useState(false);

  useEffect(() => {
    setApiKey(getStoredApiKey());

    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        onOpenSearch?.();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onOpenSearch]);

  const handleKeySave = (val: string) => {
    setApiKey(val);
    setStoredApiKey(val);
    setKeySaved(true);
    setTimeout(() => setKeySaved(false), 2000);
  };

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
        {/* Quick API Key input */}
        <div className="flex items-center gap-1 bg-surfaceSubtle border border-border rounded-sm px-2 py-0.5 text-xs">
          <Key className="w-3.5 h-3.5 text-textMuted" />
          <input
            type="password"
            value={apiKey}
            onChange={(e) => handleKeySave(e.target.value)}
            placeholder="X-API-Key..."
            className="w-28 bg-transparent text-xs font-mono text-textPrimary outline-none placeholder:text-textMuted"
            title="ใส่ API Key"
          />
          {keySaved && <Check className="w-3 h-3 text-emerald-600 animate-in zoom-in-50" />}
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

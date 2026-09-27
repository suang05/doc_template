'use client';

import React, { useState, useRef, useEffect } from 'react';
import {
  FileText,
  History,
  LayoutTemplate,
  Code2,
  GitFork,
  Key,
  BookOpen,
  ListOrdered,
  Layers,
  Settings,
  Upload,
  BarChart,
  Database,
  Users,
  Sliders,
  MoreVertical,
  LogOut,
} from 'lucide-react';
import { useAuthContext } from '@/contexts/AuthContext';
import { canAccess } from '@/lib/rbac';

export type NavigationItemId =
  | 'templates'
  | 'generator'
  | 'audit'
  | 'logs'
  | 'studio'
  | 'mapping'
  | 'upload'
  | 'version-history'
  | 'analytics'
  | 'datasources'
  | 'projects'
  | 'apidocs'
  | 'users'
  | 'settings';

export const navLabels: Record<NavigationItemId, string> = {
  templates: 'แม่แบบทั้งหมด',
  generator: 'สร้างเอกสาร',
  audit: 'ประวัติการสร้าง',
  logs: 'ประวัติการใช้งาน',
  studio: 'Template Editor',
  mapping: 'กำหนดฟิลด์',
  upload: 'อัปโหลด Template',
  'version-history': 'Version History',
  analytics: 'Analytics',
  datasources: 'Datasources',
  projects: 'API Keys',
  apidocs: 'API Docs',
  users: 'จัดการผู้ใช้',
  settings: 'ตั้งค่าระบบ',
};

interface NavSection {
  title: string;
  badge?: string;
  items: {
    id: NavigationItemId;
    label: string;
    icon: React.ComponentType<{ className?: string }>;
    tag?: string;
  }[];
}

export const navSections: NavSection[] = [
  {
    title: 'เอกสาร',
    items: [
      { id: 'templates', label: navLabels['templates'], icon: LayoutTemplate },
      { id: 'generator', label: navLabels['generator'], icon: FileText },
      { id: 'audit', label: navLabels['audit'], icon: History },
    ],
  },
  {
    title: 'จัดการ',
    items: [
      { id: 'studio', label: navLabels['studio'], icon: Code2, tag: 'v2' },
      { id: 'upload', label: navLabels['upload'], icon: Upload },
      { id: 'mapping', label: navLabels['mapping'], icon: Sliders },
      { id: 'version-history', label: navLabels['version-history'], icon: GitFork },
      { id: 'analytics', label: navLabels['analytics'], icon: BarChart },
    ],
  },
  {
    title: 'ผู้ดูแลระบบ',
    items: [
      { id: 'datasources', label: navLabels['datasources'], icon: Database },
      { id: 'projects', label: navLabels['projects'], icon: Key },
      { id: 'users', label: navLabels['users'], icon: Users },
      { id: 'settings', label: navLabels['settings'], icon: Settings },
    ],
  },
];

export interface SidebarProps {
  isOpen: boolean;
  activeItem: NavigationItemId;
  onSelectItem: (id: NavigationItemId) => void;
}

export const Sidebar: React.FC<SidebarProps> = ({
  isOpen,
  activeItem,
  onSelectItem,
}) => {
  const { user, role, logout } = useAuthContext();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false);
      }
    };
    if (menuOpen) document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [menuOpen]);

  const firstName = user?.firstName?.trim() || '';
  const lastName = user?.lastName?.trim() || '';
  const initials = firstName || lastName
    ? `${firstName[0] ?? ''}${lastName[0] ?? ''}`.toUpperCase() || 'AU'
    : user?.email?.[0]?.toUpperCase() ?? 'AU';
  const displayName = firstName || lastName
    ? `${firstName} ${lastName}`.trim()
    : user?.email ?? 'Admin User';

  return (
    <aside
      className={`bg-surface border-r border-border flex flex-col justify-between transition-all duration-200 select-none z-10 ${
        isOpen ? 'w-56' : 'w-14'
      }`}
    >
      {/* Brand header */}
      <div className="h-12 border-b border-border flex items-center px-3.5 gap-2.5 bg-surface">
        <div className="w-7 h-7 rounded-sm bg-primary text-white flex items-center justify-center shrink-0 shadow-xs">
          <Layers className="w-4 h-4" />
        </div>
        {isOpen && (
          <div className="flex flex-col overflow-hidden">
            <span className="text-xs font-bold tracking-tight text-textPrimary leading-tight">
              SMK DOC V2
            </span>
            <span className="text-[10px] text-textMuted tracking-tight leading-tight truncate">
              Document Microservice
            </span>
          </div>
        )}
      </div>

      {/* Nav Menu */}
      <div className="flex-1 overflow-y-auto py-2.5 px-2 space-y-4">
        {navSections.map((section, sIdx) => {
          const visibleItems = section.items.filter((item) => canAccess(role, item.id));
          if (visibleItems.length === 0) return null;
          return (
          <div key={sIdx} className="space-y-1">
            {isOpen && (
              <p className="px-2 text-[10px] font-semibold uppercase tracking-wider text-textMuted">
                {section.title}
              </p>
            )}

            <div className="space-y-0.5">
              {visibleItems.map((item) => {
                const Icon = item.icon;
                const isActive = activeItem === item.id;

                return (
                  <button
                    key={item.id}
                    onClick={() => onSelectItem(item.id)}
                    title={!isOpen ? item.label : undefined}
                    className={`w-full flex items-center gap-2.5 px-2.5 py-1.5 rounded-sm text-xs font-medium transition-colors cursor-pointer ${
                      isActive
                        ? 'bg-primaryLight text-primaryDark font-semibold border-l-2 border-primary'
                        : 'text-textSecondary hover:text-textPrimary hover:bg-surfaceSubtle'
                    } ${!isOpen ? 'justify-center px-0' : ''}`}
                  >
                    <Icon className={`w-4 h-4 shrink-0 ${isActive ? 'text-primary' : 'text-textMuted'}`} />
                    {isOpen && (
                      <div className="flex-1 flex items-center justify-between text-left">
                        <span className="truncate">{item.label}</span>
                        {item.tag && (
                          <span className="px-1 py-0.2 rounded-xs text-[9px] font-semibold bg-primaryLight text-primaryDark">
                            {item.tag}
                          </span>
                        )}
                      </div>
                    )}
                  </button>
                );
              })}
            </div>
          </div>
          );
        })}
      </div>

      {/* User profile footer */}
      <div className="p-2 border-t border-border bg-surfaceSubtle/60 relative" ref={menuRef}>
        <div className={`flex items-center gap-2.5 p-1.5 rounded-sm ${!isOpen ? 'justify-center' : ''}`}>
          {/* Avatar */}
          <div className="relative w-6 h-6 rounded-sm bg-primary text-white font-semibold text-[10px] flex items-center justify-center shrink-0">
            {initials}
            <span className="absolute -bottom-0.5 -right-0.5 w-2 h-2 bg-emerald-500 border border-surface rounded-[2px]" />
          </div>

          {isOpen && (
            <>
              <div className="flex-1 flex flex-col text-left overflow-hidden min-w-0">
                <span className="text-[11px] font-semibold text-textPrimary leading-tight truncate">
                  {displayName}
                </span>
                <span className="text-[9px] text-textMuted leading-tight truncate">
                  {user?.email ?? 'ผู้ดูแลระบบ'}
                </span>
              </div>

              {/* Three-dot menu button */}
              <button
                onClick={() => setMenuOpen((v) => !v)}
                className="p-1 rounded-sm text-textMuted hover:text-textPrimary hover:bg-surface transition-colors cursor-pointer shrink-0"
                title="เมนู"
              >
                <MoreVertical className="w-3.5 h-3.5" />
              </button>
            </>
          )}
        </div>

        {/* Dropdown — opens upward */}
        {menuOpen && isOpen && (
          <div className="absolute bottom-full left-2 right-2 mb-1 bg-surface border border-border rounded-sm shadow-md z-50">
            <div className="px-3 py-2 border-b border-border">
              <div className="flex items-center justify-between gap-2">
                <p className="text-[11px] font-semibold text-textPrimary truncate">{displayName}</p>
                {role && (
                  <span className="shrink-0 px-1.5 py-0.5 rounded-xs text-[9px] font-semibold bg-primaryLight text-primaryDark">
                    {role}
                  </span>
                )}
              </div>
              <p className="text-[10px] text-textMuted truncate mt-0.5">{user?.email}</p>
            </div>
            <div className="py-1">
              <button
                onClick={() => { setMenuOpen(false); onSelectItem('settings'); }}
                className="w-full text-left px-3 py-1.5 text-xs font-medium flex items-center gap-2 text-textPrimary hover:bg-surfaceSubtle cursor-pointer"
              >
                <Settings className="w-3.5 h-3.5 shrink-0" />
                ตั้งค่า
              </button>
              <button
                onClick={() => { setMenuOpen(false); logout(); }}
                className="w-full text-left px-3 py-1.5 text-xs font-medium flex items-center gap-2 text-red-600 hover:bg-surfaceSubtle hover:text-red-700 cursor-pointer"
              >
                <LogOut className="w-3.5 h-3.5 shrink-0" />
                ออกจากระบบ
              </button>
            </div>
          </div>
        )}
      </div>
    </aside>
  );
};

'use client';

import React from 'react';
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
} from 'lucide-react';

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
        {navSections.map((section, sIdx) => (
          <div key={sIdx} className="space-y-1">
            {isOpen && (
              <p className="px-2 text-[10px] font-semibold uppercase tracking-wider text-textMuted">
                {section.title}
              </p>
            )}

            <div className="space-y-0.5">
              {section.items.map((item) => {
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
        ))}
      </div>

      {/* User profile footer */}
      <div className="p-2 border-t border-border bg-surfaceSubtle/60">
        <div
          className={`flex items-center gap-2.5 p-1.5 rounded-sm hover:bg-surface transition-colors cursor-pointer ${
            !isOpen ? 'justify-center' : ''
          }`}
        >
          <div className="relative w-6 h-6 rounded-sm bg-slate-300 text-slate-700 font-semibold text-[11px] flex items-center justify-center shrink-0">
            SMK
            <span className="absolute -bottom-0.5 -right-0.5 w-2 h-2 bg-primary border border-surface rounded-[2px]" />
          </div>

          {isOpen && (
            <div className="flex flex-col text-left overflow-hidden">
              <span className="text-[11px] font-semibold text-textPrimary leading-tight truncate">
                Admin Console
              </span>
              <span className="text-[9px] text-textMuted leading-tight truncate">
                {`Connected • Port ${process.env.NEXT_PUBLIC_API_URL?.split(':').pop() ?? '8080'}`}
              </span>
            </div>
          )}
        </div>
      </div>
    </aside>
  );
};

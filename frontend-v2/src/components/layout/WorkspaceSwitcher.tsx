'use client';

import React, { useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, ChevronDown, Loader2, RefreshCw } from 'lucide-react';
import { useAuthContext } from '@/contexts/AuthContext';
import { extractErrorMessage } from '@/lib/api/client';
import type { ProjectDto } from '@/lib/api/types';
import { projectsApi } from '@/features/projects/api/projects.api';

export const PORTAL_PROJECTS_QUERY_KEY = ['portal-projects'] as const;

export const WorkspaceSwitcher: React.FC = () => {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { projectId, switchProject, isSuperAdmin } = useAuthContext();

  const [isOpen, setIsOpen] = useState(false);
  const [isSwitching, setIsSwitching] = useState(false);
  const [switchError, setSwitchError] = useState<string | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const {
    data,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery({
    queryKey: PORTAL_PROJECTS_QUERY_KEY,
    queryFn: () => projectsApi.listAccessible({ limit: 100, includeTotal: false }),
    staleTime: 30_000,
  });

  const projects: ProjectDto[] = data?.data ?? [];
  const activeProject = projects.find(
    (p) => projectId && p.id.toLowerCase() === projectId.toLowerCase(),
  );

  useEffect(() => {
    if (!isOpen) return;

    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen]);

  const handleSelectProject = async (target: ProjectDto) => {
    if (target.id.toLowerCase() === (projectId ?? '').toLowerCase()) {
      setIsOpen(false);
      return;
    }

    setSwitchError(null);
    setIsSwitching(true);
    try {
      await switchProject(target.id);
      await queryClient.invalidateQueries();
      router.refresh();
      setIsOpen(false);
    } catch (err) {
      setSwitchError(extractErrorMessage(err, 'ไม่สามารถสลับโปรเจกต์ได้'));
    } finally {
      setIsSwitching(false);
    }
  };

  const displayLabel = activeProject
    ? activeProject.name
    : projectId
      ? isSuperAdmin
        ? `Control Plane (${projectId.slice(0, 8)})`
        : `Workspace (${projectId.slice(0, 8)})`
      : isLoading
        ? 'กำลังโหลด...'
        : 'Default Workspace';

  return (
    <div ref={containerRef} className="relative hidden sm:block">
      <button
        type="button"
        onClick={() => setIsOpen((prev) => !prev)}
        disabled={isSwitching}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-label="สลับโปรเจกต์ที่กำลังทำงาน"
        className="flex items-center gap-1.5 px-2.5 py-1 rounded-sm bg-surfaceSubtle hover:bg-slate-200/70 border border-border text-[11px] text-textSecondary transition-colors cursor-pointer disabled:opacity-60"
      >
        {isSwitching || isLoading ? (
          <Loader2 className="w-3 h-3 text-primary animate-spin shrink-0" />
        ) : (
          <span
            className={`w-1.5 h-1.5 rounded-full shrink-0 ${
              isError || switchError
                ? 'bg-red-500'
                : activeProject?.isActive === false
                  ? 'bg-amber-500'
                  : 'bg-emerald-500'
            }`}
          />
        )}
        <span className="text-textMuted">Project:</span>
        <span className="font-semibold text-textPrimary max-w-[170px] truncate">
          {displayLabel}
        </span>
        <ChevronDown className="w-3 h-3 text-textMuted shrink-0" />
      </button>

      {isOpen && (
        <div
          role="listbox"
          aria-label="รายการโปรเจกต์ที่เข้าถึงได้"
          className="absolute right-0 top-full mt-1.5 w-72 bg-surface border border-border rounded-sm shadow-md z-50 overflow-hidden"
        >
          <div className="px-3 py-2 border-b border-border bg-surfaceSubtle flex items-center justify-between">
            <span className="text-[11px] font-semibold text-textSecondary">
              โปรเจกต์ที่เข้าถึงได้ ({projects.length})
            </span>
            <button
              type="button"
              onClick={() => void refetch()}
              aria-label="รีเฟรชรายการโปรเจกต์"
              className="p-1 rounded-sm text-textMuted hover:text-textPrimary hover:bg-slate-200/60 cursor-pointer"
            >
              <RefreshCw className={`w-3 h-3 ${isLoading ? 'animate-spin' : ''}`} />
            </button>
          </div>

          {(isError || switchError) && (
            <div className="p-2.5 bg-red-50 border-b border-red-200 text-red-700 text-[11px] space-y-1">
              <p>
                {switchError ??
                  extractErrorMessage(error, 'ไม่สามารถโหลดรายชื่อโปรเจกต์ที่เข้าถึงได้')}
              </p>
              <button
                type="button"
                onClick={() => {
                  setSwitchError(null);
                  void refetch();
                }}
                className="underline font-medium hover:text-red-800 cursor-pointer"
              >
                ลองโหลดใหม่อีกครั้ง
              </button>
            </div>
          )}

          <div className="max-h-64 overflow-y-auto divide-y divide-border/60">
            {isLoading && projects.length === 0 ? (
              <div className="px-3 py-4 text-center text-[11px] text-textMuted">
                กำลังโหลดรายชื่อโปรเจกต์...
              </div>
            ) : projects.length === 0 ? (
              <div className="px-3 py-4 text-center text-[11px] text-textMuted">
                {isSuperAdmin
                  ? 'ไม่มีสมาชิกหรือ Break-Glass Grant ที่เปิดอยู่ (กำลังดู Control Plane)'
                  : 'ไม่พบโปรเจกต์ที่ได้รับสิทธิ์เข้าถึง'}
              </div>
            ) : (
              projects.map((item) => {
                const isSelected =
                  Boolean(projectId) && item.id.toLowerCase() === (projectId ?? '').toLowerCase();
                return (
                  <button
                    key={item.id}
                    type="button"
                    role="option"
                    aria-selected={isSelected}
                    disabled={isSwitching}
                    onClick={() => void handleSelectProject(item)}
                    className={`w-full text-left px-3 py-2 text-xs flex items-center justify-between gap-2 transition-colors cursor-pointer ${
                      isSelected
                        ? 'bg-primaryLight/60 text-primaryDark font-semibold'
                        : 'hover:bg-surfaceSubtle text-textPrimary'
                    }`}
                  >
                    <div className="min-w-0">
                      <div className="flex items-center gap-1.5">
                        <span className="truncate">{item.name}</span>
                        {!item.isActive && (
                          <span className="px-1 py-0.2 text-[9px] rounded-xs bg-red-50 text-red-700 border border-red-200">
                            Suspended
                          </span>
                        )}
                      </div>
                      <p className="text-[10px] font-mono text-textMuted truncate">{item.slug}</p>
                    </div>
                    {isSelected && <Check className="w-3.5 h-3.5 text-primary shrink-0" />}
                  </button>
                );
              })
            )}
          </div>
        </div>
      )}
    </div>
  );
};

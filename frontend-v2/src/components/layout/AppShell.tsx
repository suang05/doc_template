import React, { useState } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { KeyRound, X } from 'lucide-react';
import { Topbar } from './Topbar';
import { Sidebar, NavigationItemId, navLabels } from './Sidebar';
import { CommandPalette } from './CommandPalette';
import { useStoredApiKey } from '@/hooks/useStoredApiKey';

export interface AppShellProps {
  children: React.ReactNode;
}

const titles = navLabels;

export const AppShell: React.FC<AppShellProps> = ({ children }) => {
  const pathname = usePathname();
  const router = useRouter();
  const activeTab = (pathname.split('/')[1] || 'templates') as NavigationItemId;
  const navigate = (id: NavigationItemId) => router.push(`/${id}`);

  const [isSidebarOpen, setIsSidebarOpen] = useState(true);
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [bannerDismissed, setBannerDismissed] = useState(false);
  const { hasApiKey } = useStoredApiKey();
  const missingKey = !hasApiKey && !bannerDismissed;

  return (
    <div className="flex h-screen w-screen overflow-hidden bg-canvas font-sans antialiased text-textPrimary">
      {/* Sidebar */}
      <Sidebar
        isOpen={isSidebarOpen}
        activeItem={activeTab}
        onSelectItem={navigate}
      />

      {/* Main Area */}
      <div className="flex-1 flex flex-col h-full overflow-hidden">
        <Topbar
          isSidebarOpen={isSidebarOpen}
          onToggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)}
          onNavigateHome={() => navigate('templates')}
          onOpenSearch={() => setIsSearchOpen(true)}
          activeViewTitle={titles[activeTab]}
        />

        {/* Missing API Key banner */}
        {missingKey && (
          <div className="shrink-0 flex items-center justify-between gap-3 px-4 py-2 bg-amber-50 border-b border-amber-200 text-amber-900">
            <div className="flex items-center gap-2 text-xs">
              <KeyRound className="w-3.5 h-3.5 text-amber-600 shrink-0" />
              <span>ยังไม่ได้ตั้งค่า API Key — ระบบจะไม่สามารถเรียก API ได้จนกว่าจะกำหนดคีย์</span>
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <button
                onClick={() => navigate('settings')}
                className="text-[11px] font-semibold text-amber-800 underline underline-offset-2 hover:text-amber-900 cursor-pointer"
              >
                ไปตั้งค่าเลย
              </button>
              <button
                onClick={() => setBannerDismissed(true)}
                className="p-0.5 rounded-sm hover:bg-amber-100 text-amber-600 cursor-pointer"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        )}

        {/* Scrollable View Canvas */}
        <main className="flex-1 overflow-y-auto p-4 md:p-6 bg-canvas">
          <div className="max-w-7xl mx-auto w-full">{children}</div>
        </main>
      </div>

      {/* Command Palette Modal (Ctrl+K) */}
      <CommandPalette
        isOpen={isSearchOpen}
        onClose={() => setIsSearchOpen(false)}
        onNavigate={navigate}
      />
    </div>
  );
};

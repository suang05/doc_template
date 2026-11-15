import React, { useState } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { Topbar } from './Topbar';
import { Sidebar, NavigationItemId, navLabels } from './Sidebar';
import { CommandPalette } from './CommandPalette';

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

  const isFixedViewport =
    pathname.startsWith('/studio') ||
    pathname.startsWith('/generator') ||
    pathname.startsWith('/mapping');

  return (
    <div className="flex h-screen w-screen overflow-hidden bg-canvas font-sans antialiased text-textPrimary">
      <Sidebar
        isOpen={isSidebarOpen}
        activeItem={activeTab}
        onSelectItem={navigate}
      />

      <div className="flex-1 flex flex-col h-full overflow-hidden">
        <Topbar
          isSidebarOpen={isSidebarOpen}
          onToggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)}
          onNavigateHome={() => navigate('templates')}
          onOpenSearch={() => setIsSearchOpen(true)}
          activeViewTitle={titles[activeTab]}
        />

        {isFixedViewport ? (
          <main className="flex-1 overflow-hidden p-2 md:p-3 bg-canvas flex flex-col min-h-0">
            {children}
          </main>
        ) : (
          <main className="flex-1 overflow-y-auto p-4 md:p-6 bg-canvas">
            <div className="max-w-7xl mx-auto w-full">{children}</div>
          </main>
        )}
      </div>

      <CommandPalette
        isOpen={isSearchOpen}
        onClose={() => setIsSearchOpen(false)}
        onNavigate={navigate}
      />
    </div>
  );
};

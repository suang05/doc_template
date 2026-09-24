import React, { useState, useEffect } from 'react';
import { Search, ArrowRight, KeyRound, X } from 'lucide-react';
import { Topbar } from './Topbar';
import { Sidebar, NavigationItemId, navLabels, navSections } from './Sidebar';
import { Modal } from '../ui/Modal';
import { getStoredApiKey } from '@/lib/api/client';

export interface AppShellProps {
  children: (activeTab: NavigationItemId, setActiveTab: (id: NavigationItemId) => void) => React.ReactNode;
  initialTab?: NavigationItemId;
}

const titles = navLabels;

// Build searchItems dynamically from navSections
const searchItems: { id: NavigationItemId; title: string; category: string }[] = [];

navSections.forEach((section) => {
  section.items.forEach((item) => {
    searchItems.push({
      id: item.id,
      title: item.label,
      category: section.title,
    });
  });
});

// Add hidden items manually to search
const hiddenItems: NavigationItemId[] = ['logs', 'mapping', 'apidocs'];
hiddenItems.forEach((id) => {
  searchItems.push({
    id,
    title: navLabels[id],
    category: 'ซ่อน (Hidden)',
  });
});

export const AppShell: React.FC<AppShellProps> = ({
  children,
  initialTab = 'templates',
}) => {
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);
  const [activeTab, setActiveTab] = useState<NavigationItemId>(initialTab);
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [missingKey, setMissingKey] = useState(false);

  useEffect(() => {
    setMissingKey(!getStoredApiKey());
  }, [activeTab]);

  const filteredItems = searchItems.filter(
    (item) =>
      item.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      item.category.toLowerCase().includes(searchQuery.toLowerCase())
  );

  return (
    <div className="flex h-screen w-screen overflow-hidden bg-canvas font-sans antialiased text-textPrimary">
      {/* Sidebar */}
      <Sidebar
        isOpen={isSidebarOpen}
        activeItem={activeTab}
        onSelectItem={(tab) => setActiveTab(tab)}
      />

      {/* Main Area */}
      <div className="flex-1 flex flex-col h-full overflow-hidden">
        <Topbar
          isSidebarOpen={isSidebarOpen}
          onToggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)}
          onNavigateHome={() => setActiveTab('templates')}
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
                onClick={() => setActiveTab('settings')}
                className="text-[11px] font-semibold text-amber-800 underline underline-offset-2 hover:text-amber-900 cursor-pointer"
              >
                ไปตั้งค่าเลย
              </button>
              <button
                onClick={() => setMissingKey(false)}
                className="p-0.5 rounded-sm hover:bg-amber-100 text-amber-600 cursor-pointer"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        )}

        {/* Scrollable View Canvas */}
        <main className="flex-1 overflow-y-auto p-4 md:p-6 bg-canvas">
          <div className="max-w-7xl mx-auto w-full">
            {children(activeTab, setActiveTab)}
          </div>
        </main>
      </div>

      {/* Command Palette Modal (Ctrl+K) */}
      <Modal
        isOpen={isSearchOpen}
        onClose={() => setIsSearchOpen(false)}
        title="ค้นหาด่วน & นำทาง (Quick Command Palette)"
        maxWidth="md"
      >
        <div className="space-y-3">
          <div className="flex items-center gap-2 px-2.5 py-1.5 bg-surfaceSubtle border border-border rounded-sm">
            <Search className="w-4 h-4 text-textMuted" />
            <input
              type="text"
              autoFocus
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="พิมพ์คำค้นหาเมนู หรือการทำงาน..."
              className="flex-1 bg-transparent text-xs text-textPrimary outline-none placeholder:text-textMuted"
            />
          </div>

          <div className="space-y-1 max-h-64 overflow-y-auto">
            {filteredItems.length === 0 ? (
              <p className="text-xs text-textMuted text-center py-4">ไม่พบรายการที่ค้นหา</p>
            ) : (
              filteredItems.map((item) => (
                <button
                  key={item.id}
                  onClick={() => {
                    setActiveTab(item.id);
                    setIsSearchOpen(false);
                    setSearchQuery('');
                  }}
                  className="w-full flex items-center justify-between p-2 rounded-sm text-xs hover:bg-slate-100 transition-colors text-left cursor-pointer group"
                >
                  <div className="flex items-center gap-2">
                    <span className="px-1.5 py-0.2 rounded-xs text-[10px] font-medium bg-surface border border-border text-textMuted">
                      {item.category}
                    </span>
                    <span className="font-semibold text-textPrimary group-hover:text-primary transition-colors">
                      {item.title}
                    </span>
                  </div>
                  <ArrowRight className="w-3.5 h-3.5 text-textMuted group-hover:text-primary transition-colors" />
                </button>
              ))
            )}
          </div>
        </div>
      </Modal>
    </div>
  );
};

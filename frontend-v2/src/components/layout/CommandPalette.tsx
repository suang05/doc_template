'use client';

import React, { useState } from 'react';
import { Search, ArrowRight } from 'lucide-react';
import { Modal } from '../ui/Modal';
import { NavigationItemId, navLabels, navSections } from './Sidebar';

// Build searchItems once from navSections
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

export interface CommandPaletteProps {
  isOpen: boolean;
  onClose: () => void;
  onNavigate: (id: NavigationItemId) => void;
}

export const CommandPalette: React.FC<CommandPaletteProps> = ({ isOpen, onClose, onNavigate }) => {
  const [searchQuery, setSearchQuery] = useState('');

  const filteredItems = searchItems.filter(
    (item) =>
      item.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      item.category.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const handleClose = () => {
    onClose();
    setSearchQuery('');
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleClose}
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
                  onNavigate(item.id);
                  handleClose();
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
  );
};

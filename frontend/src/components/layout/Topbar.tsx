"use client";
import { ReactNode } from "react";
import { cn } from "@/utils/cn";

interface TopbarProps {
  sidebarOpen: boolean;
  onToggle: () => void;
  onHome?: () => void;
  canGoBack?: boolean;
  canGoForward?: boolean;
  onBack?: () => void;
  onForward?: () => void;
  onSearch?: () => void;
  notificationCount?: number;
  onNotification?: () => void;
  onHelp?: () => void;
  workspaceName?: string;
  apiKey?: string;
  onApiKeyChange?: (key: string) => void;
}

export function Topbar({
  sidebarOpen,
  onToggle,
  onHome,
  canGoBack,
  canGoForward,
  onBack,
  onForward,
  onSearch,
  notificationCount = 0,
  onNotification,
  onHelp,
  workspaceName = "DocService",
  apiKey = "",
  onApiKeyChange,
}: TopbarProps) {
  return (
    <header
      className="fixed top-0 left-0 right-0 flex items-stretch border-b border-[var(--border)] bg-[var(--surf)]"
      style={{ height: "var(--topbar-h)", zIndex: 100 }}
    >
      {/* Sidebar zone — width + border ต้องตรงกับ Sidebar ทุกจุด */}
      <div
        className="flex items-center gap-2 px-3 flex-none overflow-hidden"
        style={{
          width: sidebarOpen ? "var(--sw)" : 0,
          paddingLeft: sidebarOpen ? undefined : 0,
          paddingRight: sidebarOpen ? undefined : 0,
          borderRight: sidebarOpen ? "1px solid var(--border)" : "none",
          background: "var(--sidebar-zone-bg)",
          transition: "width 0.18s ease, padding 0.18s ease, border-color 0.18s ease",
        }}
      >
        <div
          className="w-[22px] h-[22px] rounded flex items-center justify-center flex-none text-white"
          style={{ background: "var(--blue)" }}
        >
          <DocIcon />
        </div>
        <span className="text-[13px] font-semibold text-[var(--t1)] flex-1 truncate">{workspaceName}</span>
      </div>

      {/* Main zone */}
      <div className="flex-1 flex items-center justify-between px-2 min-w-0">
        {/* Left: toggle | < > | home */}
        <div className="flex items-center gap-0.5">
          <IconBtn onClick={onToggle} title="Toggle sidebar">
            <ToggleIcon open={sidebarOpen} />
          </IconBtn>
          <IconBtn onClick={onBack} disabled={!canGoBack} title="ย้อนกลับ">
            <ChevronLeft />
          </IconBtn>
          <IconBtn onClick={onForward} disabled={!canGoForward} title="ไปข้างหน้า">
            <ChevronRight />
          </IconBtn>
          <div className="w-px h-4 bg-[var(--border)] mx-1" />
          <IconBtn onClick={onHome} title="หน้าหลัก">
            <HomeIcon />
          </IconBtn>
        </div>

        {/* Right: api key input | search | bell | ? */}
        <div className="flex items-center gap-1.5">
          <div className="flex items-center gap-1 border border-[var(--border)] rounded-[var(--rb)] bg-[var(--sep)] px-2 h-[26px]">
            <KeyIcon />
            <input
              type="text"
              value={apiKey}
              onChange={e => onApiKeyChange?.(e.target.value)}
              placeholder="API Key..."
              spellCheck={false}
              className="w-[160px] bg-transparent text-[11.5px] font-mono text-[var(--t1)] placeholder:text-[var(--t3)] outline-none"
            />
          </div>
          <IconBtn onClick={onSearch} title="ค้นหา">
            <SearchIcon />
          </IconBtn>
          <IconBtn onClick={onNotification} badge={notificationCount > 0} title="แจ้งเตือน">
            <BellIcon />
          </IconBtn>
          <IconBtn onClick={onHelp} title="ช่วยเหลือ">
            <HelpIcon />
          </IconBtn>
        </div>
      </div>
    </header>
  );
}

function IconBtn({
  onClick,
  disabled,
  title,
  badge,
  children,
}: {
  onClick?: () => void;
  disabled?: boolean;
  title?: string;
  badge?: boolean;
  children: ReactNode;
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      title={title}
      className={cn(
        "relative w-[30px] h-[30px] flex items-center justify-center rounded-icon text-[var(--t2)] transition-colors",
        disabled ? "opacity-30 cursor-default" : "hover:bg-[var(--sep)] hover:text-[var(--t1)]"
      )}
    >
      {children}
      {badge && (
        <span className="absolute top-1 right-1 w-1.5 h-1.5 rounded-full bg-[var(--rose)]" />
      )}
    </button>
  );
}

const DocIcon = () => (
  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
    <path d="M14 2v6h6"/>
    <path d="M9 13h6M9 17h4"/>
  </svg>
);

const ToggleIcon = ({ open }: { open: boolean }) => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
    {open ? (
      <>
        <rect x="3" y="3" width="18" height="18" rx="2" />
        <path d="M9 3v18" />
      </>
    ) : (
      <>
        <rect x="3" y="3" width="18" height="18" rx="2" />
        <path d="M9 3v18M3 9h6M3 15h6" />
      </>
    )}
  </svg>
);

const ChevronLeft = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
    <path d="M15 18l-6-6 6-6" />
  </svg>
);

const ChevronRight = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
    <path d="M9 18l6-6-6-6" />
  </svg>
);

const HomeIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>
    <polyline points="9 22 9 12 15 12 15 22"/>
  </svg>
);

const SearchIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
    <circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/>
  </svg>
);

const BellIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
    <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
  </svg>
);

const HelpIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="10"/>
    <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"/>
    <line x1="12" y1="17" x2="12.01" y2="17"/>
  </svg>
);

const KeyIcon = () => (
  <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-[var(--t3)] flex-none">
    <circle cx="7.5" cy="15.5" r="5.5"/>
    <path d="M21 2l-9.6 9.6"/>
    <path d="M15.5 7.5l3 3L22 7l-3-3"/>
  </svg>
);

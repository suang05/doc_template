"use client";
import { ReactNode, useState, useRef, useEffect } from "react";
import { ChevronDown } from "lucide-react";
import type { NavItem, RoleType } from "@/types/api";
import { Pill } from "@/components/ui/Pill";
import { cn } from "@/utils/cn";

interface SidebarProps {
  open: boolean;
  activeId: string;
  onNavigate: (id: string) => void;
  mainItems: NavItem[];
  roleItems: NavItem[];
  adminItems: NavItem[];
  userName: string;
  userInitials: string;
  onSettings?: () => void;
  onLogout?: () => void;
}

export function Sidebar({
  open,
  activeId,
  onNavigate,
  mainItems,
  roleItems,
  adminItems,
  userName,
  userInitials,
  onSettings,
  onLogout,
}: SidebarProps) {
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menuOpen) return;
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, [menuOpen]);

  return (
    <aside
      className="flex-none bg-[var(--surf)] flex flex-col overflow-hidden"
      style={{
        width: open ? "var(--sw)" : 0,
        borderRight: open ? "1px solid var(--border)" : "none",
        transition: "width 0.18s ease, border-color 0.18s ease",
        height: "100%",
      }}
    >
      {/* เอกสาร + จัดการ — scrollable */}
      <nav className="flex-1 overflow-y-auto min-h-0 px-2 pb-2 flex flex-col gap-0.5">
        <SectionLabel>เอกสาร</SectionLabel>
        {mainItems.map((item) => (
          <NavRow key={item.id} item={item} active={activeId === item.id} onClick={() => onNavigate(item.id)} activeId={activeId} onNavigate={onNavigate} />
        ))}
        <SectionLabel className="mt-2" badge="DEV">จัดการ</SectionLabel>
        {roleItems.map((item) => (
          <NavRow key={item.id} item={item} active={activeId === item.id} onClick={() => onNavigate(item.id)} activeId={activeId} onNavigate={onNavigate} />
        ))}
      </nav>

      {/* ผู้ดูแล — pinned */}
      <div className="flex-none px-2 pb-1 border-t border-[var(--sep)]">
        <SectionLabel className="mt-2" badge="ADMIN">ผู้ดูแล</SectionLabel>
        {adminItems.map((item) => (
          <NavRow key={item.id} item={item} active={activeId === item.id} onClick={() => onNavigate(item.id)} activeId={activeId} onNavigate={onNavigate} />
        ))}
      </div>

      {/* Footer */}
      <div className="flex-none h-[44px] border-t border-[var(--border)] flex items-center justify-between px-3 relative">
        <div className="flex items-center gap-2">
          <Avatar initials={userInitials} />
          <span className="text-[12.5px] font-medium text-[var(--t1)] truncate">{userName}</span>
        </div>
        <div ref={menuRef} className="relative">
          <button
            onClick={() => setMenuOpen((v) => !v)}
            className="w-7 h-7 flex items-center justify-center rounded-icon text-[var(--t3)] hover:bg-[var(--sep)] hover:text-[var(--t1)] transition-colors"
          >
            <MoreIcon />
          </button>
          {menuOpen && (
            <div className="absolute bottom-full right-0 mb-1 w-[160px] bg-[var(--surf)] border border-[var(--border)] rounded-lg shadow-lg py-1 z-50">
              <button
                onClick={() => { setMenuOpen(false); onSettings?.(); }}
                className="w-full text-left px-3 py-1.5 text-[12.5px] text-[var(--t1)] hover:bg-[var(--sep)] transition-colors"
              >
                เปลี่ยน API Key
              </button>
              <div className="my-1 border-t border-[var(--sep)]" />
              <button
                onClick={() => { setMenuOpen(false); onLogout?.(); }}
                className="w-full text-left px-3 py-1.5 text-[12.5px] text-[var(--rose,#e53e3e)] hover:bg-[var(--sep)] transition-colors"
              >
                ออกจากระบบ
              </button>
            </div>
          )}
        </div>
      </div>
    </aside>
  );
}

function SectionLabel({ children, className, badge }: { children: ReactNode; className?: string; badge?: RoleType }) {
  return (
    <div className={cn("px-2 pt-2.5 pb-0.5 flex items-center gap-1.5", className)}>
      <span className="text-[10px] font-bold text-[var(--nav-section)] uppercase tracking-[.5px]">{children}</span>
      {badge && <Pill intent={badge} className="text-[9px] px-1.5 py-px">{badge}</Pill>}
    </div>
  );
}

function NavRow({ item, active, onClick, activeId, onNavigate, isChild }: {
  item: NavItem;
  active: boolean;
  onClick: () => void;
  activeId?: string;
  onNavigate?: (id: string) => void;
  isChild?: boolean;
}) {
  const hasChildren = !!item.children?.length;
  const isChildActive = hasChildren && item.children!.some(c => c.id === activeId);
  const [expanded, setExpanded] = useState(isChildActive);

  useEffect(() => {
    if (isChildActive) setExpanded(true);
  }, [isChildActive]);

  // ── Child item (dot style, no chip) ──────────────────────────────
  if (isChild) {
    return (
      <button
        onClick={() => !item.soon && onClick()}
        className={cn(
          "w-full flex items-center gap-2 pl-8 pr-2 py-[6px] rounded-[6px] text-left transition-colors",
          active ? "bg-[var(--nav-active)]" : "hover:bg-[var(--sep)]",
          item.soon && "opacity-40 cursor-default"
        )}
      >
        <span className={cn(
          "w-1.5 h-1.5 rounded-full flex-none transition-colors",
          active ? "bg-[var(--blue)]" : "bg-[var(--border)]"
        )} />
        <span className={cn(
          "text-[12px] flex-1 truncate",
          active ? "font-semibold text-[var(--nav-active-text)]" : "font-medium text-[var(--nav-label)]"
        )}>
          {item.label}
        </span>
        {item.soon && (
          <span className="text-[9px] font-bold text-[var(--t3)] bg-[var(--sep)] px-1.5 py-0.5 rounded">Soon</span>
        )}
      </button>
    );
  }

  // ── Parent with children (dropdown) ──────────────────────────────
  if (hasChildren) {
    return (
      <>
        <button
          onClick={() => setExpanded(v => !v)}
          className={cn(
            "w-full flex items-center gap-[10px] px-2 py-[7px] rounded-[6px] text-left transition-colors",
            isChildActive ? "bg-[var(--nav-active)]" : "hover:bg-[var(--sep)]",
          )}
        >
          <NavChip label={item.icon ?? item.id[0].toUpperCase()} active={isChildActive} />
          <span className={cn(
            "text-[12.5px] flex-1 truncate",
            isChildActive ? "font-semibold text-[var(--nav-active-text)]" : "font-medium text-[var(--nav-label)]"
          )}>
            {item.label}
          </span>
          <ChevronDown className={cn("w-3.5 h-3.5 text-[var(--t3)] flex-none transition-transform duration-150", expanded && "rotate-180")} />
        </button>
        {expanded && (
          <div className="flex flex-col gap-0.5 mb-0.5">
            {item.children!.map(child => (
              <NavRow
                key={child.id}
                item={child}
                active={activeId === child.id}
                onClick={() => onNavigate?.(child.id)}
                activeId={activeId}
                onNavigate={onNavigate}
                isChild
              />
            ))}
          </div>
        )}
      </>
    );
  }

  // ── Regular nav row ───────────────────────────────────────────────
  return (
    <button
      onClick={() => !item.soon && onClick()}
      className={cn(
        "w-full flex items-center gap-[10px] px-2 py-[7px] rounded-[6px] text-left transition-colors",
        active ? "bg-[var(--nav-active)]" : "hover:bg-[var(--sep)]",
        item.soon && "opacity-40 cursor-default"
      )}
    >
      <NavChip label={item.icon ?? item.id[0].toUpperCase()} active={active} />
      <span
        className={cn(
          "text-[12.5px] flex-1 truncate",
          active ? "font-semibold text-[var(--nav-active-text)]" : "font-medium text-[var(--nav-label)]"
        )}
      >
        {item.label}
      </span>
      {item.soon && (
        <span className="text-[9px] font-bold text-[var(--t3)] bg-[var(--sep)] px-1.5 py-0.5 rounded">Soon</span>
      )}
      {!item.soon && item.badge != null && item.badge > 0 && (
        <span className="text-[9px] font-bold text-white bg-[var(--blue)] px-1.5 py-0.5 rounded-full min-w-[18px] text-center">
          {item.badge}
        </span>
      )}
    </button>
  );
}

function NavChip({ label, active }: { label: string; active: boolean }) {
  return (
    <div
      className={cn(
        "w-5 h-5 flex-none flex items-center justify-center rounded-[5px] text-[10.5px] font-bold",
        active ? "bg-[var(--blue)] text-white" : "bg-[var(--nav-chip-bg)] text-[var(--nav-chip-color)]"
      )}
    >
      {label}
    </div>
  );
}

function Avatar({ initials }: { initials: string }) {
  return (
    <div className="w-[22px] h-[22px] rounded-full flex items-center justify-center text-[10px] font-bold text-white flex-none"
      style={{ background: "var(--blue)" }}>
      {initials}
    </div>
  );
}


const MoreIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
    <circle cx="5" cy="12" r="1" fill="currentColor" stroke="none" />
    <circle cx="12" cy="12" r="1" fill="currentColor" stroke="none" />
    <circle cx="19" cy="12" r="1" fill="currentColor" stroke="none" />
  </svg>
);

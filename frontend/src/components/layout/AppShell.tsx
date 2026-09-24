"use client";
import { ReactNode } from "react";
import { Topbar } from "./Topbar";
import { Sidebar } from "./Sidebar";
import type { NavItem } from "@/types/api";

interface AppShellProps {
  sidebarOpen: boolean;
  onToggleSidebar: () => void;
  activePageId: string;
  onNavigate: (id: string) => void;
  mainItems: NavItem[];
  roleItems: NavItem[];
  adminItems: NavItem[];
  userName: string;
  userInitials: string;
  onSettings?: () => void;
  onLogout?: () => void;
  onHome?: () => void;
  notificationCount?: number;
  apiKey?: string;
  onApiKeyChange?: (key: string) => void;
  children: ReactNode;
}

export function AppShell({
  sidebarOpen,
  onToggleSidebar,
  activePageId,
  onNavigate,
  mainItems,
  roleItems,
  adminItems,
  userName,
  userInitials,
  onSettings,
  onLogout,
  onHome,
  notificationCount = 0,
  apiKey,
  onApiKeyChange,
  children,
}: AppShellProps) {
  return (
    <div className="h-screen flex flex-col overflow-hidden">
      <Topbar
        sidebarOpen={sidebarOpen}
        onToggle={onToggleSidebar}
        onHome={onHome}
        notificationCount={notificationCount}
        apiKey={apiKey}
        onApiKeyChange={onApiKeyChange}
      />

      <div
        className="flex flex-1 min-h-0"
        style={{ paddingTop: "var(--topbar-h)" }}
      >
        <Sidebar
          open={sidebarOpen}
          activeId={activePageId}
          onNavigate={onNavigate}
          mainItems={mainItems}
          roleItems={roleItems}
          adminItems={adminItems}
          userName={userName}
          userInitials={userInitials}
          onSettings={onSettings}
          onLogout={onLogout}
        />
        <main className="flex-1 min-w-0 overflow-y-auto bg-[var(--bg)]">
          {children}
        </main>
      </div>
    </div>
  );
}

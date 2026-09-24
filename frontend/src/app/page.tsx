"use client";
import { useEffect, useState } from "react";
import { AppShell } from "@/components/layout/AppShell";
import { useSidebar } from "@/hooks/useSidebar";
import { useProjects } from "@/hooks/useProjects";
import { useTemplates } from "@/hooks/useTemplates";
import { ToastContainer } from "@/components/ui/Toast";
import { useToast } from "@/hooks/useToast";
import type { NavItem } from "@/types/api";
import { api } from "@/lib/api-client";

import { GeneratorView }    from "@/components/features/generator/GeneratorView";
import { TemplatesView }    from "@/components/features/templates/TemplatesView";
import { ProjectsView }     from "@/components/features/projects/ProjectsView";
import { AuditView }        from "@/components/features/audit/AuditView";
import { FieldMappingView } from "@/components/features/field-mapping/FieldMappingView";
import { ApiDocsTab }       from "@/components/tabs/ApiDocsTab";
import { TiptapEditorView }       from "@/components/features/tiptap/TiptapEditorView";
import { TemplateStudioView }     from "@/components/features/studio/TemplateStudioView";
import { ReportBroDesignerView }  from "@/components/features/reportbro/ReportBroDesignerView";
import { HtmlToPdfView }          from "@/components/features/html-to-pdf/HtmlToPdfView";

// ── Nav config ─────────────────────────────────────────────────────────────
const MAIN_ITEMS: NavItem[] = [
  { id: "generator", label: "ดาวน์โหลดเอกสาร",     icon: "ด" },
  { id: "audit",     label: "ประวัติการสร้าง",      icon: "ป", badge: 12 },
];
const ROLE_ITEMS: NavItem[] = [
  { id: "templates", label: "Template ทั้งหมด", icon: "T" },
  { id: "studio",    label: "อัปโหลด template", icon: "อ" },
  {
    id: "editor-group", label: "Template editor", icon: "E",
    children: [
      { id: "tiptap",      label: "Tiptap editor",    icon: "E" },
      { id: "reportbro",   label: "Report Designer",  icon: "R" },
      { id: "html-to-pdf", label: "HTML → PDF",       icon: "H" },
    ],
  },
  { id: "mapping",   label: "กำหนดฟิลด์", icon: "ฟ" },
];
const ADMIN_ITEMS: NavItem[] = [
  { id: "projects",  label: "API Keys",        icon: "K" },
  { id: "apidocs",   label: "คู่มือนักพัฒนา",  icon: "ค" },
  { id: "settings",  label: "ตั้งค่าระบบ",     icon: "ต", soon: true },
];

const PAGE_LABELS: Record<string, string> = {
  generator: "ดาวน์โหลดเอกสาร",
  audit:     "ประวัติการสร้าง",
  templates: "Template ทั้งหมด",
  studio:    "อัปโหลด Template",
  tiptap:    "Template Editor",
  reportbro:   "Report Designer",
  "html-to-pdf": "HTML → PDF",
  mapping:     "กำหนดฟิลด์",
  projects:  "API Keys",
  apidocs:   "คู่มือนักพัฒนา",
};

function SettingsModal({ apiKey, onSave, onClose }: { apiKey: string; onSave: (k: string) => void; onClose: () => void }) {
  const [val, setVal] = useState(apiKey);
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div className="bg-[var(--bg)] rounded-xl shadow-xl p-6 w-[380px] flex flex-col gap-4 max-h-[90vh] overflow-y-auto" onClick={e => e.stopPropagation()}>
        <div className="text-[15px] font-semibold text-[var(--t1)]">การตั้งค่า</div>
        <div className="flex flex-col gap-1">
          <label className="text-[12px] font-medium text-[var(--t2)]">API Key</label>
          <input
            className="border border-[var(--border)] rounded-lg px-3 py-2 text-[13px] bg-[var(--bg-2)] text-[var(--t1)] focus:outline-none focus:ring-2 focus:ring-[var(--primary)]"
            type="password"
            placeholder="กรอก API Key..."
            value={val}
            onChange={e => setVal(e.target.value)}
            autoFocus
          />
        </div>
        <div className="flex justify-end gap-2">
          <button className="px-4 py-2 text-[13px] rounded-lg text-[var(--t2)] hover:bg-[var(--bg-2)]" onClick={onClose}>ยกเลิก</button>
          <button className="px-4 py-2 text-[13px] rounded-lg bg-[var(--primary)] text-white hover:opacity-90" onClick={() => { onSave(val); onClose(); }}>บันทึก</button>
        </div>
      </div>
    </div>
  );
}

// ── Main shell ─────────────────────────────────────────────────────────────
export default function Home() {
  const { open, toggle }               = useSidebar();
  const { projects, refresh: refreshProjects }   = useProjects();
  const { templates, refresh: refreshTemplates } = useTemplates();
  const { toasts, toast, dismiss }     = useToast();
  const [activePage, setActivePage]    = useState<string>("generator");
  const DEV_DEFAULT_KEY = "secret-smk-key-2026";
  const [apiKey, setApiKey]            = useState<string>(DEV_DEFAULT_KEY);
  const [showSettings, setShowSettings] = useState<boolean>(false);

  useEffect(() => { refreshProjects();  }, [refreshProjects]);
  useEffect(() => { refreshTemplates(); }, [refreshTemplates]);

  // Read stored API key on mount (client-only — avoids hydration mismatch)
  useEffect(() => {
    try {
      const savedKey = localStorage.getItem("smk_api_key") ?? DEV_DEFAULT_KEY;
      setApiKey(savedKey);
      api.setApiKey(savedKey);
      refreshProjects();
      refreshTemplates();
    } catch {}
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSelectApiKey = (key: string) => {
    setApiKey(key);
    api.setApiKey(key);
    try { localStorage.setItem("smk_api_key", key); } catch {}
    refreshProjects();
    refreshTemplates();
  };

  const handleLogout = () => {
    setApiKey("");
    api.setApiKey("");
    try { localStorage.removeItem("smk_api_key"); } catch {}
  };

  return (
    <AppShell
      sidebarOpen={open}
      onToggleSidebar={toggle}
      activePageId={activePage}
      onNavigate={setActivePage}
      mainItems={MAIN_ITEMS}
      roleItems={ROLE_ITEMS}
      adminItems={ADMIN_ITEMS}
      userName="Narong D."
      userInitials="ND"
      onSettings={() => setShowSettings(true)}
      onLogout={handleLogout}
      onHome={() => setActivePage("generator")}
      apiKey={apiKey}
      onApiKeyChange={handleSelectApiKey}
    >
      {showSettings && (
        <SettingsModal
          apiKey={apiKey}
          onSave={handleSelectApiKey}
          onClose={() => setShowSettings(false)}
        />
      )}
      <div className={
        activePage === "templates"    ? "p-5 h-[calc(100vh-var(--topbar-h))] flex flex-col" :
        activePage === "mapping"      ? "p-5 h-[calc(100vh-var(--topbar-h))] flex flex-col" :
        activePage === "reportbro"    ? "p-6 h-[calc(100vh-var(--topbar-h))] flex flex-col overflow-hidden" :
        activePage === "html-to-pdf"  ? "h-[calc(100vh-var(--topbar-h))] flex flex-col overflow-hidden" :
        "p-6"
      }>
        {activePage === "generator" && (
          <GeneratorView
            templates={templates}
            onRefreshTemplates={refreshTemplates}
            onGenerationSuccess={() => {}}
            onToast={toast}
          />
        )}
        {activePage === "templates" && (
          <TemplatesView templates={templates} loading={false} onRefresh={refreshTemplates} />
        )}
        {activePage === "mapping" && (
          <FieldMappingView templates={templates} onToast={toast} />
        )}
        {activePage === "projects" && (
          <ProjectsView
            projects={projects}
            onRefresh={refreshProjects}
            onSelectApiKey={handleSelectApiKey}
          />
        )}
        {activePage === "audit" && <AuditView projects={projects} />}
        {activePage === "apidocs" && <ApiDocsTab apiKey={apiKey} />}
        {activePage === "tiptap"  && <TiptapEditorView apiKey={apiKey} onToast={toast} />}
        {activePage === "studio"     && <TemplateStudioView apiKey={apiKey} onToast={toast} />}
        {activePage === "reportbro"   && <ReportBroDesignerView apiKey={apiKey} onToast={toast} />}
        {activePage === "html-to-pdf" && <HtmlToPdfView apiKey={apiKey} onToast={toast} />}
        {activePage === "reports" && (
          <div className="flex items-center justify-center h-64 text-[var(--t3)] text-[13px]">
            หน้า Reports กำลังพัฒนา
          </div>
        )}
      </div>
      <ToastContainer toasts={toasts} onDismiss={dismiss} />
    </AppShell>
  );
}

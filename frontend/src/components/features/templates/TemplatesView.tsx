"use client";
// Pattern: features/templates — composed from ui/ primitives (Toolbar, FilterTabs) + card grid layout
import { useState, useCallback } from "react";
import { X } from "lucide-react";
import { Toolbar }    from "@/components/ui/Toolbar";
import { FormatBadge } from "@/components/ui/Badge";
import { cn }          from "@/utils/cn";
import { api }        from "@/lib/api-client";
import type { TemplateDetail, TemplateVersion, TemplateVariable } from "@/types/api";

// ── helpers ──────────────────────────────────────────────────────────────────

function getFormat(t: TemplateDetail): string {
  if (t.format) return t.format.replace(/^\./, "").toUpperCase();
  return t.fileName.split(".").pop()?.toUpperCase() ?? "";
}

function relativeTime(dateStr?: string): string {
  if (!dateStr) return "";
  const diff = Date.now() - new Date(dateStr).getTime();
  const m = Math.floor(diff / 60000);
  if (m < 60) return `${m} นาทีที่แล้ว`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h} ชั่วโมงที่แล้ว`;
  const d = Math.floor(h / 24);
  if (d < 30) return `${d} วันที่แล้ว`;
  const mo = Math.floor(d / 30);
  if (mo < 12) return `${mo} เดือนที่แล้ว`;
  return `${Math.floor(mo / 12)} ปีที่แล้ว`;
}

function formatDate(dateStr?: string): string {
  if (!dateStr) return "";
  return new Date(dateStr).toLocaleDateString("th-TH", {
    day: "numeric", month: "short", year: "numeric",
  });
}

// ── sub-components ────────────────────────────────────────────────────────────


function ProjectPill({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center px-2.5 py-1 rounded-[6px] text-[11.5px] font-medium text-[var(--t2)] bg-[var(--sep)]">
      {label}
    </span>
  );
}

function PlaceholderChip({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center px-[9px] py-[3px] rounded-[5px] text-[11px] font-medium text-[var(--t2)] border border-[var(--border)] bg-[var(--surf)]">
      {label}
    </span>
  );
}

// ── main component ────────────────────────────────────────────────────────────

interface Props {
  templates: TemplateDetail[];
  loading: boolean;
  onRefresh: () => void;
}

type FormatFilter = "all" | "docx" | "xlsx" | "pdf";

const FORMAT_TABS = [
  { key: "all",  label: "ทั้งหมด" },
  { key: "docx", label: "DOCX" },
  { key: "xlsx", label: "XLSX" },
  { key: "pdf",  label: "PDF" },
];

interface DrawerData {
  variables?: TemplateVariable[];
  versions?: TemplateVersion[];
  loadingSchema: boolean;
  loadingVersions: boolean;
}

export function TemplatesView({ templates, loading, onRefresh }: Props) {
  const [search,    setSearch]    = useState("");
  const [filter,    setFilter]    = useState<FormatFilter>("all");
  const [selected,  setSelected]  = useState<TemplateDetail | null>(null);
  const [drawer,    setDrawer]    = useState<DrawerData>({ loadingSchema: false, loadingVersions: false });
  const filtered = templates.filter((t) => {
    const fmt = getFormat(t).toLowerCase();
    const matchFmt = filter === "all" || fmt === filter;
    const matchSearch =
      t.fileName.toLowerCase().includes(search.toLowerCase()) ||
      (t.projectCode ?? "").toLowerCase().includes(search.toLowerCase());
    return matchFmt && matchSearch;
  });

  const handleSelect = useCallback(async (t: TemplateDetail) => {
    setSelected(t);
    setDrawer({ loadingSchema: true, loadingVersions: true });
    const [schemaRes, versionsRes] = await Promise.allSettled([
      api.getTemplateSchema(t.fileName),
      api.getTemplateVersions(t.fileName),
    ]);
    setDrawer({
      variables: schemaRes.status === "fulfilled" ? schemaRes.value.variables : [],
      versions: versionsRes.status === "fulfilled" ? versionsRes.value.versions : [],
      loadingSchema: false,
      loadingVersions: false,
    });
  }, []);

  return (
    <div className="flex flex-col h-full min-h-0">
      {/* toolbar */}
      <div className="rounded-[var(--rb)] border border-[var(--border)] bg-[var(--surf)] mb-3 overflow-hidden">
        <Toolbar
          count={filtered.length}
          search={search} onSearch={setSearch}
          searchPlaceholder="Search templates..."
          filters={FORMAT_TABS}
          activeFilter={filter}
          onFilter={(k) => setFilter(k as FormatFilter)}
          actions={undefined}
        />
      </div>

      {/* body */}
      <div className="flex gap-3 flex-1 min-h-0">
        {/* card grid */}
        <div className="flex-1 min-h-0 overflow-y-auto">
          {loading ? (
            <SkeletonGrid />
          ) : filtered.length === 0 ? (
            <EmptyState />
          ) : (
            <div className="grid grid-cols-2 gap-3 pb-4">
              {filtered.map((t) => (
                <TemplateCard
                  key={t.fileName}
                  template={t}
                  active={selected?.fileName === t.fileName}
                  onClick={() => handleSelect(t)}
                />
              ))}
            </div>
          )}
        </div>

        {/* right drawer */}
        {selected && (
          <TemplateDrawer template={selected} data={drawer} onClose={() => setSelected(null)} />
        )}
      </div>
    </div>
  );
}

// ── TemplateCard ──────────────────────────────────────────────────────────────

function TemplateCard({ template: t, active, onClick }: {
  template: TemplateDetail; active: boolean; onClick: () => void;
}) {
  const fmt = getFormat(t);
  const displayName = (t.originalName ?? t.fileName).replace(/\.[^.]+$/, "");
  const timeAgo = relativeTime(t.uploadedAt ?? t.createdAt);

  return (
    <button
      onClick={onClick}
      className={cn(
        "text-left p-3 rounded-[var(--r)] border bg-[var(--surf)] flex flex-col gap-2 transition-colors hover:border-[var(--blue)]",
        active ? "border-[var(--blue)]" : "border-[var(--border)]"
      )}
    >
      <div className="flex items-center justify-between">
        <FormatBadge format={fmt} showIcon />
        <span className="text-t-xs text-[var(--t3)]">v{t.version ?? 1}</span>
      </div>
      <div>
        <p className="text-t-lg font-bold text-[var(--t1)] leading-snug">{displayName}</p>
        <p className="text-t-xs text-[var(--t3)] mt-0.5">
          {t.versionsCount ? `${t.versionsCount} versions` : ""}
          {t.versionsCount && timeAgo ? " · " : ""}
          {timeAgo}
        </p>
      </div>
      {t.projectCode && <ProjectPill label={t.projectCode} />}
    </button>
  );
}

// ── TemplateDrawer ────────────────────────────────────────────────────────────

function TemplateDrawer({ template: t, data, onClose }: {
  template: TemplateDetail; data: DrawerData; onClose: () => void;
}) {
  const fmt = getFormat(t);
  const displayName = (t.originalName ?? t.fileName).replace(/\.[^.]+$/, "");

  return (
    <aside className="w-[340px] flex-none flex flex-col border border-[var(--border)] rounded-[var(--r)] bg-[var(--surf)] overflow-hidden">
      <div className="flex items-start justify-between px-4 py-3 border-b border-[var(--sep)]">
        <div className="flex items-center gap-2.5 min-w-0">
          <FormatBadge format={fmt} showIcon />
          <div className="min-w-0">
            <p className="text-[var(--text-lg)] font-bold text-[var(--t1)] truncate">{displayName}</p>
            <p className="text-[var(--text-xs)] text-[var(--t3)] mt-0.5">
              {t.projectCode ?? "Global"} · v{t.version ?? 1}
            </p>
          </div>
        </div>
        <button onClick={onClose}
          className="w-6 h-6 flex items-center justify-center rounded text-[var(--t3)] hover:text-[var(--t1)] hover:bg-[var(--sep)] flex-none ml-2 transition-colors">
          <X className="w-3.5 h-3.5" />
        </button>
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3 flex flex-col gap-4">
        {t.projectCode && (
          <p className="text-[var(--text-sm)] text-[var(--t2)] leading-relaxed">
            Template สำหรับโครงการ {t.projectCode}
          </p>
        )}

        {/* placeholders */}
        <div>
          <p className="text-[9.5px] font-bold tracking-[.6px] uppercase text-[var(--nav-section)] mb-2">Placeholders</p>
          {data.loadingSchema ? (
            <div className="flex flex-wrap gap-1.5">
              {[80, 96, 72, 108, 64].map((w) => (
                <span key={w} className="h-6 rounded-[5px] bg-[var(--sep)] animate-pulse" style={{ width: w }} />
              ))}
            </div>
          ) : data.variables && data.variables.length > 0 ? (
            <div className="flex flex-wrap gap-1.5">
              {data.variables.map((v) => <PlaceholderChip key={v.key} label={v.key} />)}
            </div>
          ) : (
            <p className="text-[var(--text-xs)] text-[var(--t3)]">ไม่พบ placeholders</p>
          )}
        </div>

        {/* version history */}
        <div>
          <p className="text-[9.5px] font-bold tracking-[.6px] uppercase text-[var(--nav-section)] mb-2">Version History</p>
          {data.loadingVersions ? (
            <div className="flex flex-col gap-2">
              {[1, 2].map((i) => <div key={i} className="h-10 rounded-[var(--rp)] bg-[var(--sep)] animate-pulse" />)}
            </div>
          ) : data.versions && data.versions.length > 0 ? (
            <div className="flex flex-col gap-0">
              {data.versions.slice().reverse().map((v, idx) => (
                <div key={v.id} className="flex items-start gap-3 py-2.5">
                  <div className="flex flex-col items-center flex-none mt-1">
                    <div className={cn(
                      "w-2 h-2 rounded-full flex-none",
                      idx === 0 ? "bg-[var(--blue)]" : "bg-[var(--border)]"
                    )} />
                    {idx < (data.versions?.length ?? 0) - 1 && (
                      <div className="w-px flex-1 min-h-[16px] bg-[var(--border)] mt-1" />
                    )}
                  </div>
                  <div className="min-w-0">
                    <p className="text-[var(--text-sm)] font-semibold text-[var(--t1)]">v{v.versionNumber}</p>
                    <p className="text-[var(--text-xs)] text-[var(--t3)]">{formatDate(v.uploadedAt)}</p>
                    {v.note && <p className="text-[var(--text-xs)] text-[var(--t2)] mt-0.5">{v.note}</p>}
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-[var(--text-xs)] text-[var(--t3)]">ไม่พบประวัติ</p>
          )}
        </div>
      </div>
    </aside>
  );
}

// ── skeleton / empty ──────────────────────────────────────────────────────────

function SkeletonGrid() {
  return (
    <div className="grid grid-cols-2 gap-3">
      {[1, 2, 3, 4].map((i) => (
        <div key={i} className="p-4 rounded-[var(--r)] border border-[var(--border)] bg-[var(--surf)] flex flex-col gap-3">
          <div className="w-14 h-5 rounded-[5px] bg-[var(--sep)] animate-pulse" />
          <div className="w-3/4 h-4 rounded bg-[var(--sep)] animate-pulse" />
          <div className="w-1/2 h-3 rounded bg-[var(--sep)] animate-pulse" />
          <div className="w-20 h-6 rounded-[6px] bg-[var(--sep)] animate-pulse" />
        </div>
      ))}
    </div>
  );
}

function EmptyState() {
  return (
    <div className="flex flex-col items-center justify-center py-20 text-center gap-3">
      <div className="w-12 h-12 rounded-xl bg-[var(--sep)] flex items-center justify-center text-[var(--t3)]">
        <FileIcon size={22} />
      </div>
      <p className="text-[var(--text-base)] font-semibold text-[var(--t1)]">ไม่พบ template</p>
      <p className="text-[var(--text-sm)] text-[var(--t3)]">ลองเปลี่ยน filter หรืออัปโหลด template ใหม่</p>
    </div>
  );
}

// ── icons (document-specific — not in lucide) ─────────────────────────────────

function FileIcon({ size = 14 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <path d="M14 2v6h6" /><path d="M9 13h6M9 17h4" />
    </svg>
  );
}


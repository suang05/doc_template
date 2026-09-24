"use client";
import { useEffect, useState, useCallback } from "react";
import { Activity, CheckCircle2, XCircle, Clock, RefreshCw, Download, Copy, Check } from "lucide-react";
import { StatTile }    from "@/components/ui/StatTile";
import { Toolbar }     from "@/components/ui/Toolbar";
import { Table }       from "@/components/ui/Table";
import { Pagination }  from "@/components/ui/Pagination";
import { FormatBadge } from "@/components/ui/Badge";
import { StatusPill }  from "@/components/ui/Pill";
import { cn }          from "@/utils/cn";
import { api }         from "@/lib/api-client";
import type { MetricSummary, GenerationLog, Project, TableColumn } from "@/types/api";

interface AuditViewProps {
  projects: Project[];
}

type StatusFilter = "all" | "success" | "failed";
const PAGE_SIZE = 20;

const STATUS_TABS = [
  { key: "all",     label: "ทั้งหมด" },
  { key: "success", label: "สำเร็จ" },
  { key: "failed",  label: "ล้มเหลว" },
];

function fmtTime(ms: number) {
  return ms >= 1000 ? `${(ms / 1000).toFixed(1)} s` : `${Math.round(ms)} ms`;
}
function fmtSize(bytes?: number) {
  if (!bytes) return "—";
  return bytes >= 1048576 ? `${(bytes / 1048576).toFixed(1)} MB` : `${Math.round(bytes / 1024)} KB`;
}
function fmtDate(ts: string) {
  return new Date(ts).toLocaleString("th-TH", { dateStyle: "short", timeStyle: "short" });
}

function FormatIcon({ fmt }: { fmt: string }) {
  const f = fmt.toUpperCase();
  if (f === "XLSX") return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" className="flex-none">
      <rect width="24" height="24" rx="4" fill="var(--emerald)" opacity=".15"/>
      <path d="M8 8h8M8 12h8M8 16h5" stroke="var(--emerald)" strokeWidth="1.8" strokeLinecap="round"/>
      <rect x="14" y="12" width="4" height="4" rx="1" fill="var(--emerald)" opacity=".5"/>
    </svg>
  );
  if (f === "DOCX") return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" className="flex-none">
      <rect width="24" height="24" rx="4" fill="var(--blue)" opacity=".15"/>
      <path d="M8 9h8M8 12h8M8 15h5" stroke="var(--blue)" strokeWidth="1.8" strokeLinecap="round"/>
    </svg>
  );
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" className="flex-none">
      <rect width="24" height="24" rx="4" fill="var(--rose)" opacity=".15"/>
      <path d="M8 9h8M8 12h8M8 15h5" stroke="var(--rose)" strokeWidth="1.8" strokeLinecap="round"/>
    </svg>
  );
}


function CopyBtn({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);
  const handle = async () => {
    try { await navigator.clipboard.writeText(text); } catch { /* ignore */ }
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };
  return (
    <button onClick={handle} title="คัดลอกชื่อไฟล์"
      className="p-1 rounded text-[var(--t3)] hover:text-[var(--t1)] hover:bg-[var(--sep)] transition-colors">
      {copied ? <Check className="w-3.5 h-3.5 text-[var(--emerald)]" /> : <Copy className="w-3.5 h-3.5" />}
    </button>
  );
}

const COLUMNS: TableColumn<GenerationLog>[] = [
  {
    key: "timestamp",
    header: "วันเวลา",
    width: "130px",
    render: (log) => (
      <span className="font-mono text-t-xs text-[var(--t3)] whitespace-nowrap">{fmtDate(log.createdAt)}</span>
    ),
  },
  {
    key: "project",
    header: "โปรเจกต์",
    width: "90px",
    render: (log) => (
      <span className="text-t-xs font-semibold text-[var(--navy)]">{log.project?.code ?? "—"}</span>
    ),
  },
  {
    key: "templateName",
    header: "แม่แบบ",
    render: (log) => (
      <div className="flex items-center gap-1.5 min-w-0">
        <FormatIcon fmt={log.outputFormat} />
        <span className="font-mono text-t-xs text-[var(--t2)] truncate" title={log.outputFileName ?? log.templateName}>
          {log.outputFileName ?? log.templateName}
        </span>
      </div>
    ),
  },
  {
    key: "outputFormat",
    header: "รูปแบบ",
    width: "64px",
    render: (log) => <FormatBadge format={log.outputFormat} />,
  },
  {
    key: "executionTimeMs",
    header: "เวลา",
    width: "72px",
    render: (log) => (
      <span className="font-mono text-t-xs text-[var(--t2)] tabular-nums whitespace-nowrap">{fmtTime(log.executionTimeMs)}</span>
    ),
  },
  {
    key: "fileSizeBytes",
    header: "ขนาด",
    width: "80px",
    render: (log) => (
      <span className="font-mono text-t-xs text-[var(--t3)] tabular-nums">{fmtSize(log.fileSizeBytes)}</span>
    ),
  },
  {
    key: "status",
    header: "สถานะ",
    width: "88px",
    render: (log) => <StatusPill status={log.status} />,
  },
  {
    key: "actions",
    header: "",
    width: "64px",
    render: (log) => (
      <div className="flex items-center gap-0.5">
        <CopyBtn text={log.outputFileName ?? log.templateName} />
        <button
          disabled
          title="ยังไม่รองรับการดาวน์โหลดซ้ำ"
          className="p-1 rounded text-[var(--t3)] opacity-30 cursor-not-allowed"
        >
          <Download className="w-3.5 h-3.5" />
        </button>
      </div>
    ),
  },
];

export function AuditView({ projects }: AuditViewProps) {
  const [metrics,        setMetrics]        = useState<MetricSummary | null>(null);
  const [logs,           setLogs]           = useState<GenerationLog[]>([]);
  const [total,          setTotal]          = useState(0);
  const [totalPgs,       setTotalPgs]       = useState(1);
  const [page,           setPage]           = useState(1);
  const [status,         setStatus]         = useState<StatusFilter>("all");
  const [projectId,      setProjectId]      = useState("");
  const [search,         setSearch]         = useState("");
  const [loading,        setLoading]        = useState(true);
  const [metricsLoading, setMetricsLoading] = useState(true);

  const fetchMetrics = useCallback(async () => {
    setMetricsLoading(true);
    try { setMetrics(await api.getMetrics()); } catch { /* ignore */ }
    finally { setMetricsLoading(false); }
  }, []);

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.getLogs({
        page,
        pageSize: PAGE_SIZE,
        status: status !== "all" ? status.toUpperCase() : undefined,
        projectId: projectId || undefined,
      });
      setLogs(res.logs ?? []);
      setTotal(res.totalCount ?? 0);
      setTotalPgs(res.totalPages ?? 1);
    } catch { setLogs([]); }
    finally { setLoading(false); }
  }, [page, status, projectId]);

  useEffect(() => { fetchMetrics(); }, [fetchMetrics]);
  useEffect(() => { fetchLogs(); }, [fetchLogs]);

  const filtered = search
    ? logs.filter((l) =>
        l.templateName?.toLowerCase().includes(search.toLowerCase()) ||
        l.project?.code?.toLowerCase().includes(search.toLowerCase())
      )
    : logs;

  const projectSelect = (
    <select
      value={projectId}
      onChange={(e) => { setProjectId(e.target.value); setPage(1); }}
      className="text-t-xs bg-[var(--sep)] border border-[var(--border)] rounded-[var(--rp)] px-2 py-1 text-[var(--t1)] focus:outline-none focus:border-[var(--blue)] transition"
    >
      <option value="">ทุกโปรเจกต์</option>
      {projects.map((p) => <option key={p.id} value={p.id}>{p.code}</option>)}
    </select>
  );

  const toolbarActions = (
    <>
      {projectSelect}
      <button
        onClick={() => { fetchLogs(); fetchMetrics(); }}
        className="p-1.5 rounded-[var(--rp)] text-[var(--t3)] hover:text-[var(--t1)] hover:bg-[var(--sep)] border border-[var(--border)] transition-colors"
      >
        <RefreshCw className={cn("w-3.5 h-3.5", loading && "animate-spin text-[var(--blue)]")} />
      </button>
      <button className="flex items-center gap-1 px-2.5 py-1 text-t-xs font-medium text-[var(--t2)] border border-[var(--border)] rounded-[var(--rp)] hover:bg-[var(--sep)] transition-colors">
        <Download className="w-3 h-3" /> Export CSV
      </button>
    </>
  );

  return (
    <div className="space-y-4">

      {/* KPI row */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatTile loading={metricsLoading}
          icon={<Activity className="w-4 h-4" />}
          iconBg="rgba(41,84,255,.1)" iconColor="var(--blue)"
          value={(metrics?.totalGenerations ?? 0).toLocaleString()}
          label="สร้างเอกสารทั้งหมด" />
        <StatTile loading={metricsLoading}
          icon={<CheckCircle2 className="w-4 h-4" />}
          iconBg="rgba(5,150,105,.1)" iconColor="var(--emerald)"
          value={`${metrics?.successRatePercentage ?? 0}%`}
          label="อัตราความสำเร็จ" />
        <StatTile loading={metricsLoading}
          icon={<Clock className="w-4 h-4" />}
          iconBg="rgba(217,119,6,.1)" iconColor="var(--amber)"
          value={metrics ? fmtTime(metrics.avgExecutionTimeMs) : "—"}
          label="เวลาเฉลี่ยต่อเอกสาร" />
        <StatTile loading={metricsLoading}
          icon={<XCircle className="w-4 h-4" />}
          iconBg="rgba(225,29,72,.1)" iconColor="var(--rose)"
          value={String(metrics?.totalFailed ?? 0)}
          label="ล้มเหลวทั้งหมด" />
      </div>

      {/* Log table */}
      <div className="rounded-[var(--r)] border border-[var(--border)] bg-[var(--surf)] overflow-hidden">
        <Toolbar
          title="ประวัติการสร้าง"
          count={total}
          search={search} onSearch={setSearch}
          searchPlaceholder="ค้นหาแม่แบบ / โปรเจกต์..."
          filters={STATUS_TABS}
          activeFilter={status}
          onFilter={(k) => { setStatus(k as StatusFilter); setPage(1); }}
          actions={toolbarActions}
        />
        <Table
          columns={COLUMNS}
          rows={filtered}
          loading={loading}
          skeletonRows={8}
          emptyTitle="ไม่พบข้อมูล"
          footer={
            <div className="flex items-center justify-between w-full">
              <span className="text-t-xs text-[var(--t3)]">
                {total > 0 ? `หน้า ${page} / ${totalPgs}` : "ไม่มีข้อมูล"}
              </span>
              <Pagination page={page} total={totalPgs} onChange={setPage} />
            </div>
          }
        />
      </div>
    </div>
  );
}

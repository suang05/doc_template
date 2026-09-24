"use client";
import React, { useState, useCallback } from "react";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { Pill } from "@/components/ui/Pill";
import { cn } from "@/utils/cn";
import { api } from "@/lib/api-client";
import type { Project, ApiKey } from "@/types/api";

// ── sub-components ────────────────────────────────────────────────────────────

function CodeBadge({ code }: { code: string }) {
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded-[5px] text-t-xs font-bold"
      style={{ color: "var(--navy)", background: "rgba(30,58,95,.09)" }}>
      {code}
    </span>
  );
}

function KeyStatusBadge({ active }: { active: boolean }) {
  return (
    <Pill intent={active ? "success" : "failed"}>
      {active ? "เปิดใช้" : "ระงับ"}
    </Pill>
  );
}

// ── modal ─────────────────────────────────────────────────────────────────────

function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: React.ReactNode }) {
  return (
    <div className="fixed inset-0 z-[var(--z-modal)] flex items-center justify-center">
      <div className="absolute inset-0 bg-black/30" onClick={onClose} />
      <div className="relative bg-[var(--surf)] rounded-[var(--r)] border border-[var(--border)] w-[400px] shadow-none overflow-hidden">
        <div className="flex items-center justify-between px-5 py-3.5 border-b border-[var(--sep)]">
          <p className="text-t-lg font-bold text-[var(--t1)]">{title}</p>
          <button onClick={onClose} className="w-6 h-6 flex items-center justify-center rounded text-[var(--t3)] hover:text-[var(--t1)] hover:bg-[var(--sep)] transition-colors">
            <CloseIcon />
          </button>
        </div>
        <div className="px-5 py-4">{children}</div>
      </div>
    </div>
  );
}

function Field({ label, required, children }: { label: string; required?: boolean; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <label className="text-t-xs font-semibold text-[var(--t2)]">
        {label}{required && <span className="text-[var(--rose)] ml-0.5">*</span>}
      </label>
      {children}
    </div>
  );
}

function Input({ value, onChange, placeholder, mono }: {
  value: string; onChange: (v: string) => void; placeholder?: string; mono?: boolean;
}) {
  return (
    <input
      value={value}
      onChange={e => onChange(e.target.value)}
      placeholder={placeholder}
      className={cn(
        "px-3 py-1.5 rounded-[var(--rb)] border border-[var(--border)] text-t-base text-[var(--t1)] bg-[var(--surf)] outline-none focus:border-[var(--blue)] transition-colors w-full",
        mono && "font-mono"
      )}
    />
  );
}

// ── main ──────────────────────────────────────────────────────────────────────

interface Props {
  projects: Project[];
  onRefresh: () => void;
  onSelectApiKey: (key: string) => void;
}

export function ProjectsView({ projects, onRefresh, onSelectApiKey }: Props) {
  // new project modal
  const [showNewProject, setShowNewProject] = useState(false);
  const [projCode, setProjCode] = useState("");
  const [projName, setProjName] = useState("");
  const [projDesc, setProjDesc] = useState("");
  const [creatingProj, setCreatingProj] = useState(false);

  // new key modal
  const [showNewKey, setShowNewKey] = useState(false);
  const [keyTargetId, setKeyTargetId] = useState<string>("");
  const [keyName, setKeyName] = useState("");
  const [keyExpires, setKeyExpires] = useState("");
  const [creatingKey, setCreatingKey] = useState(false);
  const [newKeySecret, setNewKeySecret] = useState<string | null>(null);

  const [copiedId, setCopiedId] = useState<string | null>(null);

  const handleCreateProject = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!projCode || !projName) return;
    setCreatingProj(true);
    try {
      await api.createProject({ code: projCode.toUpperCase().trim(), name: projName.trim(), description: projDesc.trim() || undefined });
      setShowNewProject(false);
      setProjCode(""); setProjName(""); setProjDesc("");
      onRefresh();
    } catch (err: unknown) {
      alert("สร้างโปรเจกต์ไม่สำเร็จ: " + (err instanceof Error ? err.message : ""));
    } finally { setCreatingProj(false); }
  }, [projCode, projName, projDesc, onRefresh]);

  const openNewKey = useCallback((projectId: string) => {
    setKeyTargetId(projectId);
    setKeyName(""); setKeyExpires(""); setNewKeySecret(null);
    setShowNewKey(true);
  }, []);

  const handleCreateKey = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    if (!keyName) return;
    setCreatingKey(true);
    try {
      const res = await api.createApiKey(keyTargetId, { name: keyName.trim(), expiresAt: keyExpires || undefined });
      setNewKeySecret(res.keySecret ?? null);
      onRefresh();
    } catch (err: unknown) {
      alert("สร้างกุญแจไม่สำเร็จ: " + (err instanceof Error ? err.message : ""));
      setShowNewKey(false);
    } finally { setCreatingKey(false); }
  }, [keyTargetId, keyName, keyExpires, onRefresh]);

  const handleRevoke = useCallback(async (keyId: string) => {
    if (!confirm("ต้องการระงับกุญแจนี้?")) return;
    try { await api.revokeApiKey(keyId); onRefresh(); }
    catch (err: unknown) { alert("ระงับกุญแจไม่สำเร็จ: " + (err instanceof Error ? err.message : "")); }
  }, [onRefresh]);

  const copy = useCallback((text: string, id: string) => {
    try { navigator.clipboard.writeText(text); } catch {}
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 1500);
  }, []);

  return (
    <div className="flex flex-col gap-3">
      {/* ── toolbar ───────────────────────────────────────────────────────── */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-t-2xl font-bold text-[var(--t1)] tracking-tight leading-none">โปรเจกต์ & API Keys</h1>
          <p className="text-t-xs text-[var(--t3)] mt-0.5">จัดการโปรเจกต์และกุญแจเข้าถึงระบบ</p>
        </div>
        <Button onClick={() => setShowNewProject(true)} icon={<Plus className="w-3.5 h-3.5" />}>
          สร้างโปรเจกต์
        </Button>
      </div>

      {/* ── project list ──────────────────────────────────────────────────── */}
      {projects.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 gap-3 text-center">
          <div className="w-10 h-10 rounded-[var(--ri)] bg-[var(--sep)] flex items-center justify-center text-[var(--t3)]">
            <FolderIcon />
          </div>
          <p className="text-t-base font-semibold text-[var(--t1)]">ยังไม่มีโปรเจกต์</p>
          <p className="text-t-sm text-[var(--t3)]">สร้างโปรเจกต์แรกเพื่อเริ่มใช้งาน</p>
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          {projects.map(proj => (
            <ProjectCard
              key={proj.id}
              project={proj}
              copiedId={copiedId}
              onNewKey={() => openNewKey(proj.id)}
              onCopy={copy}
              onRevoke={handleRevoke}
              onSelectKey={onSelectApiKey}
            />
          ))}
        </div>
      )}

      {/* ── new project modal ─────────────────────────────────────────────── */}
      {showNewProject && (
        <Modal title="สร้างโปรเจกต์ใหม่" onClose={() => setShowNewProject(false)}>
          <form onSubmit={handleCreateProject} className="flex flex-col gap-3">
            <Field label="รหัสโปรเจกต์" required>
              <Input value={projCode} onChange={setProjCode} placeholder="SMK_SALES" mono />
            </Field>
            <Field label="ชื่อโปรเจกต์" required>
              <Input value={projName} onChange={setProjName} placeholder="Sales Application" />
            </Field>
            <Field label="คำอธิบาย">
              <Input value={projDesc} onChange={setProjDesc} placeholder="ระบบขายและสัญญาโครงการ" />
            </Field>
            <div className="flex gap-2 justify-end pt-1">
              <Button type="button" variant="ghost" onClick={() => setShowNewProject(false)}>ยกเลิก</Button>
              <Button type="submit" loading={creatingProj} disabled={!projCode || !projName}>สร้างโปรเจกต์</Button>
            </div>
          </form>
        </Modal>
      )}

      {/* ── new key modal ─────────────────────────────────────────────────── */}
      {showNewKey && (
        <Modal title="ออกกุญแจใหม่" onClose={() => setShowNewKey(false)}>
          {newKeySecret ? (
            <div className="flex flex-col gap-3">
              <div className="p-3 rounded-[var(--rp)] bg-[var(--emerald-t)] border border-[var(--emerald)] text-t-sm text-[var(--emerald)] font-medium">
                สร้างกุญแจสำเร็จ — คัดลอกก่อนปิด ไม่สามารถดูได้อีก
              </div>
              <div className="flex items-center gap-2 px-3 py-2 rounded-[var(--rb)] border border-[var(--border)] bg-[var(--bg)]">
                <code className="flex-1 text-t-sm font-mono text-[var(--t1)] break-all">{newKeySecret}</code>
                <button onClick={() => copy(newKeySecret, "new")}
                  className="flex-none text-[var(--t3)] hover:text-[var(--blue)] transition-colors">
                  {copiedId === "new" ? <CheckTinyIcon /> : <CopyIcon />}
                </button>
              </div>
              <div className="flex justify-end">
                <Button onClick={() => setShowNewKey(false)}>ปิด</Button>
              </div>
            </div>
          ) : (
            <form onSubmit={handleCreateKey} className="flex flex-col gap-3">
              <Field label="ชื่อกุญแจ" required>
                <Input value={keyName} onChange={setKeyName} placeholder="Production" />
              </Field>
              <Field label="วันหมดอายุ (ไม่บังคับ)">
                <input type="date" value={keyExpires} onChange={e => setKeyExpires(e.target.value)}
                  className="px-3 py-1.5 rounded-[var(--rb)] border border-[var(--border)] text-t-base text-[var(--t1)] bg-[var(--surf)] outline-none focus:border-[var(--blue)] transition-colors w-full" />
              </Field>
              <div className="flex gap-2 justify-end pt-1">
                <Button type="button" variant="ghost" onClick={() => setShowNewKey(false)}>ยกเลิก</Button>
                <Button type="submit" loading={creatingKey} disabled={!keyName}>ออกกุญแจ</Button>
              </div>
            </form>
          )}
        </Modal>
      )}
    </div>
  );
}

// ── ProjectCard ───────────────────────────────────────────────────────────────

function ProjectCard({ project: p, copiedId, onNewKey, onCopy, onRevoke, onSelectKey }: {
  project: Project; copiedId: string | null;
  onNewKey: () => void; onCopy: (text: string, id: string) => void;
  onRevoke: (id: string) => void; onSelectKey: (key: string) => void;
}) {
  return (
    <div className="rounded-[var(--r)] border border-[var(--border)] bg-[var(--surf)] overflow-hidden">
      {/* project header row */}
      <div className="flex items-center gap-3 px-4 py-3">
        <div className="w-8 h-8 rounded-[var(--rp)] bg-[var(--blue-t)] flex items-center justify-center flex-none text-[var(--blue)]">
          <GridIcon />
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <p className="text-t-base font-bold text-[var(--t1)]">{p.name}</p>
            <CodeBadge code={p.code} />
          </div>
          {p.description && <p className="text-t-xs text-[var(--t3)] mt-0.5 truncate">{p.description}</p>}
        </div>
        <Button variant="outline" size="sm" onClick={onNewKey} icon={<KeyIcon />}>ออกกุญแจใหม่</Button>
      </div>

      {/* API key rows */}
      {p.apiKeys && p.apiKeys.length > 0 && (
        <div className="border-t border-[var(--sep)] divide-y divide-[var(--sep)]">
          {p.apiKeys.map(k => (
            <ApiKeyRow
              key={k.id}
              apiKey={k}
              copied={copiedId === k.id}
              onCopy={() => {
                onCopy(k.keyMasked ?? k.id, k.id);
                if (k.keyMasked) onSelectKey(k.keyMasked);
              }}
              onRevoke={() => onRevoke(k.id)}
            />
          ))}
        </div>
      )}
    </div>
  );
}

// ── ApiKeyRow ─────────────────────────────────────────────────────────────────

function ApiKeyRow({ apiKey: k, copied, onCopy, onRevoke }: {
  apiKey: ApiKey; copied: boolean; onCopy: () => void; onRevoke: () => void;
}) {
  return (
    <div className="flex items-center gap-3 px-4 py-2.5 pl-[52px] hover:bg-[var(--sep)] transition-colors group">
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <p className="text-t-sm font-semibold text-[var(--t1)]">{k.name}</p>
          <KeyStatusBadge active={k.isActive} />
        </div>
        <p className="text-t-xs font-mono text-[var(--t3)] mt-0.5">{k.keyMasked ?? "••••••••"}</p>
      </div>
      <div className="flex items-center gap-1.5 flex-none">
        <Button variant="outline" size="sm" onClick={onCopy} icon={copied ? <CheckTinyIcon /> : <CopyIcon />}>คัดลอก</Button>
        {k.isActive && (
          <Button variant="danger" size="sm" onClick={onRevoke} icon={<BanIcon />}>ระงับ</Button>
        )}
      </div>
    </div>
  );
}

// ── icons ─────────────────────────────────────────────────────────────────────

const sw = { fill: "none", stroke: "currentColor", strokeWidth: "2", strokeLinecap: "round" as const, strokeLinejoin: "round" as const };

const CloseIcon     = () => <svg width="13" height="13" viewBox="0 0 24 24" {...sw}><path d="M18 6 6 18M6 6l12 12"/></svg>;
const CopyIcon      = () => <svg width="11" height="11" viewBox="0 0 24 24" {...sw}><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>;
const CheckTinyIcon = () => <svg width="11" height="11" viewBox="0 0 24 24" {...sw} strokeWidth="3"><polyline points="20 6 9 17 4 12"/></svg>;
const KeyIcon       = () => <svg width="12" height="12" viewBox="0 0 24 24" {...sw}><circle cx="7.5" cy="15.5" r="5.5"/><path d="m21 2-9.6 9.6"/><path d="m15.5 7.5 3 3L22 7l-3-3"/></svg>;
const BanIcon       = () => <svg width="11" height="11" viewBox="0 0 24 24" {...sw}><circle cx="12" cy="12" r="10"/><path d="m4.9 4.9 14.2 14.2"/></svg>;
const GridIcon      = () => <svg width="14" height="14" viewBox="0 0 24 24" {...sw}><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>;
const FolderIcon    = () => <svg width="20" height="20" viewBox="0 0 24 24" {...sw}><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>;

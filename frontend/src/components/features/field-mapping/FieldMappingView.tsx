"use client";
// Pattern: features/field-mapping — 4-layer: useFieldMapping hook, ui/ primitives
import { useEffect, useState, useCallback } from "react";
import { Plus, Trash2, GripVertical, Eye, Save, RefreshCw, CheckCircle2, XCircle, AlertCircle, ChevronDown } from "lucide-react";
import { cn } from "@/utils/cn";
import { useFieldMapping } from "@/hooks/useFieldMapping";
import { api } from "@/lib/api-client";
import { Button } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import type { TemplateDetail, FieldMappingItem, ThaiTransform, PlaceholderType } from "@/types/api";

interface FieldMappingViewProps {
  templates: TemplateDetail[];
  onToast?: (msg: string, intent?: "success" | "error" | "warning" | "info") => void;
}

const TRANSFORMS: { value: ThaiTransform | ""; label: string }[] = [
  { value: "",                   label: "— ไม่แปลง —" },
  { value: "formatThaiBaht",     label: "บาทไทย (สองล้าน...)" },
  { value: "formatThaiDate",     label: "วันที่ไทย (9 มิถุนายน 2569)" },
  { value: "formatThaiDateTime", label: "วันเวลาไทย" },
  { value: "formatCurrency",     label: "ตัวเลขสกุลเงิน (2,500,000.00)" },
  { value: "formatPhone",        label: "เบอร์โทรศัพท์ (081-xxx-xxxx)" },
  { value: "formatThaiId",       label: "เลขบัตรประชาชน (x-xxxx-xxxxx-xx-x)" },
];

const PLACEHOLDER_TYPES: { value: PlaceholderType; label: string; color: string }[] = [
  { value: "text",    label: "ข้อความ",  color: "var(--t3)" },
  { value: "table",   label: "ตาราง",   color: "var(--blue)" },
  { value: "qrcode",  label: "QR Code", color: "var(--emerald)" },
  { value: "barcode", label: "Barcode", color: "var(--amber)" },
  { value: "image",   label: "รูปภาพ",  color: "var(--purple, #8b5cf6)" },
];

// ── schema var badge ────────────────────────────────────────────────────────────

function PlaceholderTag({ text }: { text: string }) {
  return (
    <Badge intent="blue" className="font-mono whitespace-nowrap">
      {`{{${text}}}`}
    </Badge>
  );
}

// ── preview result row ──────────────────────────────────────────────────────────

function PreviewRow({ item }: { item: { placeholder: string; sourcePath: string; rawValue?: string | null; transformedValue?: string | null; found: boolean } }) {
  return (
    <div className={cn(
      "flex items-start gap-2 px-3 py-2 border-b border-[var(--sep)] text-t-xs last:border-0",
      !item.found && "opacity-60"
    )}>
      <span className="mt-0.5">
        {item.found
          ? <CheckCircle2 className="w-3.5 h-3.5 text-[var(--emerald)]" />
          : <XCircle className="w-3.5 h-3.5 text-[var(--rose)]" />}
      </span>
      <div className="min-w-0 flex-1">
        <p className="font-mono text-[var(--navy)] truncate">{`{{${item.placeholder}}}`}</p>
        <p className="text-[var(--t3)] truncate">{item.sourcePath}</p>
      </div>
      <div className="text-right shrink-0 max-w-[140px]">
        {item.transformedValue != null
          ? <p className="text-[var(--t1)] font-medium truncate">{item.transformedValue}</p>
          : item.rawValue != null
            ? <p className="text-[var(--t2)] truncate">{String(item.rawValue)}</p>
            : <p className="text-[var(--t3)] italic">ไม่พบ</p>}
      </div>
    </div>
  );
}

// ── mapping row ─────────────────────────────────────────────────────────────────

function MappingRow({
  item, idx, onUpdate, onDelete, dragHandleProps,
}: {
  item: FieldMappingItem;
  idx: number;
  onUpdate: (idx: number, field: keyof FieldMappingItem, val: unknown) => void;
  onDelete: (idx: number) => void;
  dragHandleProps?: React.HTMLAttributes<HTMLSpanElement>;
}) {
  const inputCls = "w-full bg-[var(--sep)] border border-[var(--border)] rounded-[var(--ri)] px-2 py-1 text-t-xs text-[var(--t1)] focus:outline-none focus:border-[var(--blue)] focus:ring-1 focus:ring-[var(--blue-t)] transition";
  const placeholderType = (item.placeholderType ?? "text") as PlaceholderType;
  const isText = placeholderType === "text";
  const typeColor = PLACEHOLDER_TYPES.find((t) => t.value === placeholderType)?.color ?? "var(--t3)";

  return (
    <tr className="border-b border-[var(--sep)] last:border-0 hover:bg-[var(--bg)] group transition-colors">
      {/* drag handle */}
      <td className="py-2 pl-3 pr-1 w-6">
        <span {...dragHandleProps} className="text-[var(--border)] group-hover:text-[var(--t3)] cursor-grab active:cursor-grabbing transition-colors">
          <GripVertical className="w-3.5 h-3.5" />
        </span>
      </td>
      {/* placeholder */}
      <td className="py-2 px-2 whitespace-nowrap">
        <PlaceholderTag text={item.placeholder} />
      </td>
      {/* label */}
      <td className="py-2 px-2">
        <input value={item.label} onChange={(e) => onUpdate(idx, "label", e.target.value)}
          placeholder="ชื่อฟิลด์ภาษาไทย" className={inputCls} />
      </td>
      {/* placeholder type */}
      <td className="py-2 px-2 whitespace-nowrap">
        <div className="relative">
          <select
            value={placeholderType}
            onChange={(e) => onUpdate(idx, "placeholderType", e.target.value as PlaceholderType)}
            style={{ borderColor: typeColor, color: typeColor }}
            className="w-full appearance-none bg-[var(--sep)] border rounded-[var(--ri)] px-2 py-1 text-t-xs font-medium focus:outline-none focus:ring-1 transition pr-5"
          >
            {PLACEHOLDER_TYPES.map((t) => (
              <option key={t.value} value={t.value}>{t.label}</option>
            ))}
          </select>
          <ChevronDown className="w-3 h-3 absolute right-1.5 top-1/2 -translate-y-1/2 pointer-events-none" style={{ color: typeColor }} />
        </div>
      </td>
      {/* source path */}
      <td className="py-2 px-2">
        <input value={item.sourcePath} onChange={(e) => onUpdate(idx, "sourcePath", e.target.value)}
          placeholder={placeholderType === "table" ? "$.items (array)" : "$.data.fieldName"}
          className={cn(inputCls, "font-mono")} />
      </td>
      {/* transform — text only */}
      <td className="py-2 px-2">
        {isText ? (
          <div className="relative">
            <select value={item.transform ?? ""} onChange={(e) => onUpdate(idx, "transform", e.target.value || null)}
              className={cn(inputCls, "appearance-none pr-6")}>
              {TRANSFORMS.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
            </select>
            <ChevronDown className="w-3 h-3 absolute right-2 top-1/2 -translate-y-1/2 pointer-events-none text-[var(--t3)]" />
          </div>
        ) : (
          <span className="text-t-xs text-[var(--t3)] italic px-1">—</span>
        )}
      </td>
      {/* default value */}
      <td className="py-2 px-2">
        <input value={item.defaultValue ?? ""} onChange={(e) => onUpdate(idx, "defaultValue", e.target.value || null)}
          placeholder="ค่าเริ่มต้น" className={inputCls}
          disabled={placeholderType === "table"}
          title={placeholderType === "table" ? "ตาราง ไม่รองรับค่าเริ่มต้น" : undefined} />
      </td>
      {/* required */}
      <td className="py-2 px-2 text-center">
        <button onClick={() => onUpdate(idx, "isRequired", !item.isRequired)}
          className={cn("w-5 h-5 rounded border-2 flex items-center justify-center transition-colors mx-auto",
            item.isRequired
              ? "border-[var(--navy)] bg-[var(--navy)] text-white"
              : "border-[var(--border)] bg-[var(--sep)]"
          )}>
          {item.isRequired && <span className="text-[9px] font-black leading-none">✓</span>}
        </button>
      </td>
      {/* delete */}
      <td className="py-2 pl-1 pr-3 text-center">
        <button onClick={() => onDelete(idx)}
          className="opacity-0 group-hover:opacity-100 text-[var(--t3)] hover:text-[var(--rose)] transition-all p-0.5 rounded">
          <Trash2 className="w-3.5 h-3.5" />
        </button>
      </td>
    </tr>
  );
}

// ── main view ───────────────────────────────────────────────────────────────────

export function FieldMappingView({ templates, onToast }: FieldMappingViewProps) {
  const [selectedFile, setSelectedFile] = useState(templates[0]?.fileName ?? "");
  const [localItems, setLocalItems] = useState<FieldMappingItem[]>([]);
  const [schemaPlaceholders, setSchemaPlaceholders] = useState<string[]>([]);
  const [schemaLoading, setSchemaLoading] = useState(false);
  const [showPreview, setShowPreview] = useState(false);
  const [sampleJson, setSampleJson] = useState("{}");
  const [dirty, setDirty] = useState(false);

  const fm = useFieldMapping(selectedFile);

  // load schema placeholders
  const loadSchema = useCallback(async (file: string) => {
    if (!file) return;
    setSchemaLoading(true);
    try {
      const s = await api.getTemplateSchema(file);
      setSchemaPlaceholders((s.variables ?? []).map((v) => v.key));
    } catch { setSchemaPlaceholders([]); }
    finally { setSchemaLoading(false); }
  }, []);

  // when template changes
  useEffect(() => {
    if (!selectedFile) return;
    fm.load();
    loadSchema(selectedFile);
    setShowPreview(false);
    setDirty(false);
  }, [selectedFile]); // eslint-disable-line react-hooks/exhaustive-deps

  // sync hook mappings → local editable copy
  useEffect(() => {
    setLocalItems(fm.mappings.map((m) => ({ ...m })));
    setDirty(false);
  }, [fm.mappings]);

  // auto-add unmapped placeholders
  useEffect(() => {
    if (!schemaPlaceholders.length) return;
    setLocalItems((prev) => {
      const mapped = new Set(prev.map((m) => m.placeholder));
      const toAdd = schemaPlaceholders.filter((p) => !mapped.has(p));
      if (!toAdd.length) return prev;
      const next = [
        ...prev,
        ...toAdd.map((p, i) => ({
          placeholder: p,
          sourcePath: "",
          label: p,
          placeholderType: "text" as const,
          transform: null,
          defaultValue: null,
          isRequired: false,
          sortOrder: prev.length + i,
        } satisfies FieldMappingItem)),
      ];
      setDirty(true);
      return next;
    });
  }, [schemaPlaceholders]);

  const updateItem = (idx: number, field: keyof FieldMappingItem, val: unknown) => {
    setLocalItems((prev) => {
      const next = [...prev];
      next[idx] = { ...next[idx], [field]: val };
      return next;
    });
    setDirty(true);
  };

  const deleteItem = (idx: number) => {
    setLocalItems((prev) => prev.filter((_, i) => i !== idx));
    setDirty(true);
  };

  const addItem = () => {
    setLocalItems((prev) => [
      ...prev,
      { placeholder: "", sourcePath: "", label: "", placeholderType: "text", transform: null, defaultValue: null, isRequired: false, sortOrder: prev.length },
    ]);
    setDirty(true);
  };

  const handleSave = async () => {
    const ok = await fm.save(localItems.map((m, i) => ({ ...m, sortOrder: i })));
    if (ok) onToast?.("บันทึก mapping สำเร็จ", "success");
    else onToast?.(fm.error ?? "บันทึกไม่สำเร็จ", "error");
    setDirty(false);
  };

  const handlePreview = async () => {
    await fm.preview(localItems, sampleJson);
    setShowPreview(true);
  };

  const unmapped = schemaPlaceholders.filter(
    (p) => !localItems.find((m) => m.placeholder === p && m.sourcePath)
  );

  return (
    <div className="flex flex-col gap-4 h-[calc(100vh-var(--topbar-h)-2.5rem)]">

      {/* ── header row ──────────────────────────────────────────── */}
      <div className="flex items-center gap-3 flex-wrap">
        {/* template selector */}
        <div className="relative flex-1 min-w-[200px] max-w-xs">
          <div className="pointer-events-none absolute left-3 inset-y-0 flex items-center text-[var(--t3)]">
            <svg width="13" height="13" viewBox="0 0 16 16" fill="none"><path d="M3 2h7l3 3v9a1 1 0 01-1 1H3a1 1 0 01-1-1V3a1 1 0 011-1z" stroke="currentColor" strokeWidth="1.5"/><path d="M10 2v3h3" stroke="currentColor" strokeWidth="1.5"/></svg>
          </div>
          <select value={selectedFile} onChange={(e) => setSelectedFile(e.target.value)}
            className="w-full appearance-none bg-[var(--surf)] border border-[var(--border)] rounded-[var(--ri)] pl-7 pr-8 py-1.5 text-t-sm text-[var(--t1)] focus:outline-none focus:border-[var(--blue)] transition">
            {templates.map((t) => <option key={t.fileName} value={t.fileName}>{t.fileName}</option>)}
          </select>
          <span className="pointer-events-none absolute right-3 inset-y-0 flex items-center text-[var(--t3)]">▾</span>
        </div>

        {/* schema badge */}
        {schemaLoading
          ? <div className="h-6 w-28 rounded bg-[var(--sep)] animate-pulse" />
          : schemaPlaceholders.length > 0 && (
            <span className="text-t-xs text-[var(--t3)]">
              <span className="font-semibold text-[var(--emerald)]">✓ {schemaPlaceholders.length}</span> ตัวแปร
              {unmapped.length > 0 && (
                <span className="ml-2 text-[var(--amber)] font-semibold">{unmapped.length} ยังไม่ได้ผูก</span>
              )}
            </span>
          )
        }

        <div className="flex-1" />

        {/* actions */}
        <Button size="sm" variant="outline" onClick={() => { fm.load(); loadSchema(selectedFile); }}>
          <RefreshCw className={cn("w-3.5 h-3.5", fm.loading && "animate-spin")} /> รีเฟรช
        </Button>
        <Button size="sm" variant="outline" onClick={handlePreview} disabled={fm.previewing || !selectedFile}>
          {fm.previewing ? <RefreshCw className="w-3.5 h-3.5 animate-spin" /> : <Eye className="w-3.5 h-3.5" />} จำลอง
        </Button>
        <Button size="sm" variant="navy" onClick={handleSave} disabled={fm.saving || !dirty}>
          {fm.saving ? <RefreshCw className="w-3.5 h-3.5 animate-spin" /> : <Save className="w-3.5 h-3.5" />}
          {dirty ? "บันทึก *" : "บันทึก"}
        </Button>
      </div>

      {/* error */}
      {fm.error && (
        <div className="flex items-center gap-2 px-3 py-2 bg-[var(--rose-t)] border border-[var(--rose)] rounded-[var(--rb)] text-t-xs text-[var(--rose)]">
          <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {fm.error}
        </div>
      )}

      {/* ── main content ─────────────────────────────────────────── */}
      <div className="flex gap-4 flex-1 min-h-0">

        {/* mapping table */}
        <div className="flex-1 rounded-[var(--r)] border border-[var(--border)] bg-[var(--surf)] flex flex-col overflow-hidden">
          <div className="overflow-auto flex-1">
            <table className="w-full text-left min-w-[820px]">
              <thead className="sticky top-0 z-10 bg-[var(--sep)] border-b border-[var(--border)]">
                <tr>
                  <th className="py-2 pl-3 pr-1 w-6" />
                  <th className="py-2 px-2 text-t-xs font-semibold text-[var(--t3)] uppercase tracking-[.4px] whitespace-nowrap">ตัวแปร</th>
                  <th className="py-2 px-2 text-t-xs font-semibold text-[var(--t3)] uppercase tracking-[.4px]">ชื่อฟิลด์ (Label)</th>
                  <th className="py-2 px-2 text-t-xs font-semibold text-[var(--t3)] uppercase tracking-[.4px] whitespace-nowrap">ประเภท</th>
                  <th className="py-2 px-2 text-t-xs font-semibold text-[var(--t3)] uppercase tracking-[.4px]">Source Path (JSON)</th>
                  <th className="py-2 px-2 text-t-xs font-semibold text-[var(--t3)] uppercase tracking-[.4px]">Transform</th>
                  <th className="py-2 px-2 text-t-xs font-semibold text-[var(--t3)] uppercase tracking-[.4px] whitespace-nowrap">ค่าเริ่มต้น</th>
                  <th className="py-2 px-2 text-t-xs font-semibold text-[var(--t3)] uppercase tracking-[.4px] text-center whitespace-nowrap">จำเป็น</th>
                  <th className="py-2 pl-1 pr-3 w-8" />
                </tr>
              </thead>
              <tbody>
                {fm.loading ? (
                  Array.from({ length: 5 }).map((_, i) => (
                    <tr key={i} className="border-b border-[var(--sep)]">
                      {[1,2,3,4,5,6,7,8,9].map((j) => (
                        <td key={j} className="py-2.5 px-2">
                          <div className="h-3 rounded bg-[var(--sep)] animate-pulse" />
                        </td>
                      ))}
                    </tr>
                  ))
                ) : localItems.length === 0 ? (
                  <tr>
                    <td colSpan={9} className="py-12 text-center text-t-xs text-[var(--t3)]">
                      ยังไม่มี mapping — เลือกแม่แบบที่มีตัวแปร หรือกด "+ เพิ่มฟิลด์"
                    </td>
                  </tr>
                ) : (
                  localItems.map((item, idx) => (
                    <MappingRow
                      key={`${item.placeholder}-${idx}`}
                      item={item} idx={idx}
                      onUpdate={updateItem} onDelete={deleteItem}
                    />
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* footer: add row */}
          <div className="border-t border-[var(--sep)] px-4 py-2 flex items-center gap-3">
            <button onClick={addItem}
              className="flex items-center gap-1.5 text-t-xs font-medium text-[var(--t3)] hover:text-[var(--navy)] transition-colors">
              <Plus className="w-3.5 h-3.5" /> เพิ่มฟิลด์
            </button>
            <span className="text-t-xs text-[var(--t3)]">{localItems.length} ฟิลด์</span>
          </div>
        </div>

        {/* preview panel */}
        {showPreview && (
          <div className="w-72 rounded-[var(--r)] border border-[var(--border)] bg-[var(--surf)] flex flex-col overflow-hidden shrink-0">
            <div className="flex items-center justify-between px-3 py-2 border-b border-[var(--border)]">
              <span className="text-t-xs font-semibold text-[var(--t1)]">จำลองผลลัพธ์</span>
              <button onClick={() => setShowPreview(false)} className="text-[var(--t3)] hover:text-[var(--t1)] text-[11px]">✕</button>
            </div>
            {/* sample JSON input */}
            <div className="px-3 pt-2 pb-1 border-b border-[var(--sep)]">
              <label className="text-t-xs text-[var(--t3)] font-medium">Sample JSON</label>
              <textarea value={sampleJson} onChange={(e) => setSampleJson(e.target.value)} rows={4} spellCheck={false}
                className="w-full mt-1 bg-[var(--sep)] border border-[var(--border)] rounded-[var(--ri)] px-2 py-1 font-mono text-t-xs text-[var(--t1)] focus:outline-none focus:border-[var(--blue)] resize-none" />
              <button onClick={handlePreview} disabled={fm.previewing}
                className="mt-1 text-t-xs text-[var(--blue)] hover:text-[var(--navy)] font-semibold transition-colors flex items-center gap-1">
                {fm.previewing ? <RefreshCw className="w-3 h-3 animate-spin" /> : <Eye className="w-3 h-3" />} รันจำลอง
              </button>
            </div>
            {/* results */}
            <div className="flex-1 overflow-y-auto">
              {fm.previewResult.length === 0
                ? <p className="text-t-xs text-[var(--t3)] p-3 text-center">กรอก JSON แล้วกด "รันจำลอง"</p>
                : fm.previewResult.map((r) => <PreviewRow key={r.placeholder} item={r} />)
              }
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

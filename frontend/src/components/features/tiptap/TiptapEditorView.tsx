"use client";
// Pattern: features/tiptap — Tiptap HTML template editor view
import { useState } from "react";
import { Save, RefreshCw, AlertCircle } from "lucide-react";
import { TiptapEditor } from "./TiptapEditor";
import { useTiptapEditor } from "@/hooks/useTiptapEditor";
import { Button } from "@/components/ui/Button";

const DEFAULT_HTML = `<h1>ชื่อเอกสาร</h1>
<p>เรียน {{recipientName}}</p>
<p>เนื้อหาเอกสาร...</p>
<p>วันที่ {{documentDate}}</p>`;

interface TiptapEditorViewProps {
  apiKey?: string;
  onToast?: (msg: string, intent?: "success" | "error" | "warning" | "info") => void;
}

export function TiptapEditorView({ apiKey, onToast }: TiptapEditorViewProps) {
  const [fileName, setFileName] = useState("my-template");
  const [content, setContent]   = useState(DEFAULT_HTML);
  const [isGlobal, setIsGlobal] = useState(false);

  const { saving, error, save } = useTiptapEditor(apiKey);

  const handleSave = async () => {
    const ok = await save(fileName, content, isGlobal);
    if (ok) onToast?.("บันทึก HTML Template สำเร็จ", "success");
    else onToast?.(error ?? "บันทึกไม่สำเร็จ", "error");
  };

  return (
    <div className="flex flex-col gap-4 h-[calc(100vh-var(--topbar-h)-2.5rem)]">

      {/* Header */}
      <div className="flex flex-wrap items-center gap-3">
        {/* File name */}
        <div className="flex items-center gap-1.5 min-w-[160px] max-w-xs flex-1">
          <input
            value={fileName}
            onChange={(e) => setFileName(e.target.value.replace(/\s/g, "-"))}
            placeholder="ชื่อไฟล์ (ไม่ต้องใส่ .html)"
            className="flex-1 bg-[var(--surf)] border border-[var(--border)] rounded-[var(--ri)] px-3 py-1.5 text-t-sm text-[var(--t1)] focus:outline-none focus:border-[var(--blue)] transition"
          />
          <span className="text-t-xs text-[var(--t3)] font-mono">.html</span>
        </div>

        {/* Global toggle + Save — always in one row, wraps below filename on narrow screens */}
        <div className="flex items-center gap-3 ml-auto">
          <label className="flex items-center gap-2 text-t-xs text-[var(--t2)] cursor-pointer select-none whitespace-nowrap">
            <input
              type="checkbox"
              checked={isGlobal}
              onChange={(e) => setIsGlobal(e.target.checked)}
              className="w-3.5 h-3.5 accent-[var(--blue)]"
            />
            Global Template
          </label>

          <Button size="sm" onClick={handleSave} disabled={saving || !fileName.trim()}>
            {saving
              ? <RefreshCw className="w-3.5 h-3.5 animate-spin" />
              : <Save className="w-3.5 h-3.5" />}
            บันทึก Template
          </Button>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="flex items-center gap-2 px-3 py-2 bg-[var(--rose-t)] border border-[var(--rose)] rounded-[var(--rb)] text-t-xs text-[var(--rose)]">
          <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {error}
        </div>
      )}

      {/* Editor */}
      <TiptapEditor
        value={content}
        onChange={setContent}
        placeholder="เริ่มพิมพ์เนื้อหา HTML Template..."
        className="flex-1 min-h-0"
      />

      {/* Hint */}
      <p className="text-t-xs text-[var(--t3)]">
        ใช้ <code className="font-mono bg-[var(--sep)] px-1 rounded">{"{{ชื่อตัวแปร}}"}</code> เพื่อแทรก placeholder — ตัวแปรจะถูก highlight เป็นสีน้ำเงิน
      </p>
    </div>
  );
}

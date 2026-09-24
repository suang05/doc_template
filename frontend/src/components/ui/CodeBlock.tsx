"use client";
import { useState } from "react";
import { cn } from "@/utils/cn";

interface CodeBlockProps {
  code: string;
  lang?: string;
  copyable?: boolean;
  className?: string;
}

export function CodeBlock({ code, lang, copyable = true, className }: CodeBlockProps) {
  const [copied, setCopied] = useState(false);

  const copy = () => {
    navigator.clipboard.writeText(code).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  };

  return (
    <div className={cn("relative rounded bg-[#0f172a] overflow-hidden", className)}>
      {lang && (
        <div className="px-4 py-1.5 text-[10px] font-bold text-[#475569] uppercase tracking-widest border-b border-[#1e293b]">
          {lang}
        </div>
      )}
      {copyable && (
        <button
          onClick={copy}
          className="absolute top-2 right-2 px-2 py-1 rounded text-[10px] font-semibold text-[#64748b] hover:text-[#94a3b8] hover:bg-[#1e293b] transition-colors"
        >
          {copied ? "Copied!" : "Copy"}
        </button>
      )}
      <pre className="p-4 text-[12px] font-mono text-[#cbd5e1] overflow-x-auto leading-relaxed">
        <code>{code}</code>
      </pre>
    </div>
  );
}

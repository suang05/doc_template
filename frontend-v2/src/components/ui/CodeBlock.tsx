'use client';

import React, { useState } from 'react';
import { Check, Copy } from 'lucide-react';

export interface CodeBlockProps {
  code: string;
  language?: string;
  title?: string;
  maxHeight?: string;
}

export const CodeBlock: React.FC<CodeBlockProps> = ({
  code,
  language = 'json',
  title,
  maxHeight = 'max-h-72',
}) => {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="border border-border rounded-sm bg-slate-900 text-slate-100 overflow-hidden font-mono text-xs shadow-xs">
      <div className="flex items-center justify-between px-3 py-1.5 bg-slate-950/80 border-b border-slate-800">
        <span className="text-[11px] text-slate-400 font-sans">{title || language}</span>
        <button
          onClick={handleCopy}
          className="flex items-center gap-1 text-[11px] text-slate-400 hover:text-slate-100 px-2 py-0.5 rounded-sm hover:bg-slate-800 transition-colors"
        >
          {copied ? (
            <>
              <Check className="w-3 h-3 text-emerald-400" />
              <span className="text-emerald-400">คัดลอกแล้ว</span>
            </>
          ) : (
            <>
              <Copy className="w-3 h-3" />
              <span>คัดลอก</span>
            </>
          )}
        </button>
      </div>

      <pre className={`p-3 overflow-x-auto ${maxHeight} text-slate-200 leading-relaxed scrollbar-thin`}>
        <code>{code}</code>
      </pre>
    </div>
  );
};

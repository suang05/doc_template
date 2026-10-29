'use client';

import React from 'react';
import dynamic from 'next/dynamic';

const Editor = dynamic(() => import('@monaco-editor/react'), { ssr: false });

export type StudioTabKey = 'main' | 'headerFooter' | 'payload';

export interface StudioEditorTabsProps {
  leftTab: StudioTabKey;
  onTabChange: (tab: StudioTabKey) => void;
  mainHtml: string;
  headerHtml: string;
  footerHtml: string;
  sampleDataJson: string;
  onUpdateMainHtml: (val: string) => void;
  onUpdateHeaderHtml: (val: string) => void;
  onUpdateFooterHtml: (val: string) => void;
  onUpdateSampleDataJson: (val: string) => void;
  onInsertSnippet: (snippetText: string) => void;
  onEditorMount: (editor: any, monaco: any) => void;
}

export const StudioEditorTabs: React.FC<StudioEditorTabsProps> = ({
  leftTab,
  onTabChange,
  mainHtml,
  headerHtml,
  footerHtml,
  sampleDataJson,
  onUpdateMainHtml,
  onUpdateHeaderHtml,
  onUpdateFooterHtml,
  onUpdateSampleDataJson,
  onInsertSnippet,
  onEditorMount,
}) => {
  return (
    <div className="w-full md:w-1/2 flex flex-col border-r border-border h-full bg-surface min-w-0">
      {/* Tab Header */}
      <div className="h-9 bg-surfaceSubtle border-b border-border px-3 flex items-center justify-between shrink-0">
        <div className="flex items-center gap-1 h-full pt-2">
          <button
            onClick={() => onTabChange('main')}
            className={`px-4 h-full flex items-center text-[11px] font-semibold rounded-t-sm transition-colors cursor-pointer ${
              leftTab === 'main'
                ? 'bg-surface text-primary border-t border-l border-r border-border border-b-2 border-b-primary -mb-px'
                : 'text-textMuted hover:text-textPrimary border-b-2 border-b-transparent -mb-px'
            }`}
          >
            Main HTML
          </button>
          <button
            onClick={() => onTabChange('headerFooter')}
            className={`px-4 h-full flex items-center text-[11px] font-semibold rounded-t-sm transition-colors cursor-pointer ${
              leftTab === 'headerFooter'
                ? 'bg-surface text-primary border-t border-l border-r border-border border-b-2 border-b-primary -mb-px'
                : 'text-textMuted hover:text-textPrimary border-b-2 border-b-transparent -mb-px'
            }`}
          >
            Header & Footer
          </button>
          <button
            onClick={() => onTabChange('payload')}
            className={`px-4 h-full flex items-center text-[11px] font-semibold rounded-t-sm transition-colors cursor-pointer ${
              leftTab === 'payload'
                ? 'bg-surface text-primary border-t border-l border-r border-border border-b-2 border-b-primary -mb-px'
                : 'text-textMuted hover:text-textPrimary border-b-2 border-b-transparent -mb-px'
            }`}
          >
            JSON Payload
          </button>
        </div>
        {leftTab !== 'payload' && (
          <span className="text-[10px] text-textMuted">UTF-8 • Sarabun Font</span>
        )}
        {leftTab === 'payload' && (
          <span className="text-[10px] bg-slate-100 text-textMuted px-1.5 py-0.5 rounded-[2px]">Auto-extracted</span>
        )}
      </div>

      {/* Tab Content */}
      <div className="flex-1 relative flex flex-col overflow-hidden bg-zinc-900">
        {/* Quick Insert Snippet Bar for HTML templates */}
        {leftTab !== 'payload' && (
          <div className="bg-zinc-950 border-b border-zinc-800 px-3 py-1.5 flex items-center gap-1.5 overflow-x-auto shrink-0 scrollbar-thin">
            <span className="text-zinc-500 font-mono text-[10px] uppercase shrink-0 mr-1">แทรกด่วน:</span>
            <button
              type="button"
              onClick={() => onInsertSnippet('{{#each items}}\n<tr>\n  <td>{{addOne @index}}</td>\n  <td>{{name}}</td>\n  <td style="text-align: right;">{{amount:number}}</td>\n</tr>\n{{/each}}')}
              className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
              title="แทรกบล็อกวนลูปตารางรายการ"
            >
              <span className="text-indigo-400 font-bold">+</span> ตารางรายการ (each)
            </button>
            <button
              type="button"
              onClick={() => onInsertSnippet('{{#if condition}}\n  <!-- เมื่อเป็นจริง -->\n{{else}}\n  <!-- เมื่อเป็นเท็จ -->\n{{/if}}')}
              className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
              title="แทรกบล็อกเงื่อนไข True/False"
            >
              <span className="text-sky-400 font-bold">+</span> เงื่อนไข (if)
            </button>
            <button
              type="button"
              onClick={() => onInsertSnippet('{{#ifEquals status "APPROVED"}}\n  <span>อนุมัติแล้ว</span>\n{{else}}\n  <span>รออนุมัติ</span>\n{{/ifEquals}}')}
              className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
              title="ตรวจสอบค่าเท่ากัน"
            >
              <span className="text-sky-400 font-bold">+</span> เช็คค่า (ifEquals)
            </button>
            <button
              type="button"
              onClick={() => onInsertSnippet('{{addOne @index}}')}
              className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
              title="ลำดับแถวเริ่มต้นที่ 1"
            >
              <span className="text-emerald-400 font-bold">+</span> ลำดับ (1,2,3..)
            </button>
            <button
              type="button"
              onClick={() => onInsertSnippet('<div style="page-break-after: always;"></div>')}
              className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
              title="แบ่งหน้าขึ้นหน้าใหม่ A4"
            >
              <span className="text-amber-400 font-bold">+</span> แบ่งหน้า A4
            </button>
            <button
              type="button"
              onClick={() => onInsertSnippet('{{qr:trackingUrl}}')}
              className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
              title="แทรกรูป QR Code ขนาด 150x150"
            >
              <span className="text-purple-400 font-bold">+</span> QR Code
            </button>
            <button
              type="button"
              onClick={() => onInsertSnippet('{{barcode:code}}')}
              className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
              title="แทรกรูป Barcode Code128"
            >
              <span className="text-purple-400 font-bold">+</span> Barcode
            </button>
            <button
              type="button"
              onClick={() => onInsertSnippet(':thai_baht_text}}')}
              className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
              title="แปลงตัวเลขเป็นตัวหนังสือบาทไทย"
            >
              <span className="text-emerald-400 font-bold">:</span> ตัวหนังสือบาท
            </button>
            <button
              type="button"
              onClick={() => onInsertSnippet(':number}}')}
              className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
              title="จัดฟอร์แมตตัวเลขมีคอมม่าทศนิยม"
            >
              <span className="text-emerald-400 font-bold">:</span> ตัวเลขคอมม่า
            </button>
            <button
              type="button"
              onClick={() => onInsertSnippet(':thai_date}}')}
              className="px-2 py-0.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white rounded-[2px] text-[11px] font-mono shrink-0 transition-colors border border-zinc-700/60 flex items-center gap-1 cursor-pointer"
              title="จัดรูปแบบวันที่ไทย พ.ศ."
            >
              <span className="text-amber-400 font-bold">:</span> วันที่ไทย
            </button>
          </div>
        )}

        {leftTab === 'main' && (
          <Editor
            height="100%"
            defaultLanguage="html"
            theme="vs-dark"
            value={mainHtml}
            onChange={(val) => { onUpdateMainHtml(val || ''); }}
            onMount={onEditorMount}
            path="main"
            options={{
              minimap: { enabled: false },
              fontSize: 12,
              fontFamily: "'Fira Code', monospace",
              lineNumbers: 'on',
              wordWrap: 'on',
              automaticLayout: true,
            }}
          />
        )}

        {leftTab === 'headerFooter' && (
          <div className="flex-1 flex flex-col">
            <div className="flex-1 flex flex-col border-b border-zinc-800">
              <div className="text-[10px] font-mono text-zinc-500 bg-zinc-900 px-3 py-1 border-b border-zinc-800 flex items-center gap-1.5">
                <span>Header HTML</span>
                <span className="text-zinc-700">·</span>
                <span className="text-sky-600/60">{'<template id="header">'}&hellip;{'</template>'}</span>
              </div>
              <div className="flex-1 relative">
                <Editor
                  height="100%"
                  defaultLanguage="html"
                  theme="vs-dark"
                  value={headerHtml}
                  onChange={(val) => { onUpdateHeaderHtml(val || ''); }}
                  onMount={onEditorMount}
                  path="header"
                  options={{
                    minimap: { enabled: false },
                    fontSize: 12,
                    fontFamily: "'Fira Code', monospace",
                    lineNumbers: 'on',
                    wordWrap: 'on',
                    automaticLayout: true,
                  }}
                />
              </div>
            </div>
            <div className="flex-1 flex flex-col">
              <div className="text-[10px] font-mono text-zinc-500 bg-zinc-900 px-3 py-1 border-b border-zinc-800 flex items-center gap-1.5">
                <span>Footer HTML</span>
                <span className="text-zinc-700">·</span>
                <span className="text-sky-600/60">{'<template id="footer">'}&hellip;{'</template>'}</span>
              </div>
              <div className="flex-1 relative">
                <Editor
                  height="100%"
                  defaultLanguage="html"
                  theme="vs-dark"
                  value={footerHtml}
                  onChange={(val) => { onUpdateFooterHtml(val || ''); }}
                  onMount={onEditorMount}
                  path="footer"
                  options={{
                    minimap: { enabled: false },
                    fontSize: 12,
                    fontFamily: "'Fira Code', monospace",
                    lineNumbers: 'on',
                    wordWrap: 'on',
                    automaticLayout: true,
                  }}
                />
              </div>
            </div>
          </div>
        )}

        {leftTab === 'payload' && (
          <Editor
            height="100%"
            defaultLanguage="json"
            theme="vs-dark"
            value={sampleDataJson}
            onChange={(v) => onUpdateSampleDataJson(v || '{}')}
            path="payload"
            options={{
              minimap: { enabled: false },
              fontSize: 12,
              fontFamily: "'Fira Code', monospace",
              lineNumbers: 'on',
              wordWrap: 'on',
              automaticLayout: true,
            }}
          />
        )}
      </div>
    </div>
  );
};


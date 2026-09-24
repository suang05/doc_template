'use client';

/**
 * JsonEditor — thin abstraction over the text editing surface.
 * Interface is stable; swap `impl` prop to "monaco" when Monaco is installed
 * without changing any parent component.
 *
 * Future swap:
 *   import dynamic from 'next/dynamic';
 *   const Monaco = dynamic(() => import('@monaco-editor/react'), { ssr: false });
 *   if (impl === 'monaco') return <Monaco language="json" value={value} onChange={v => onChange(v ?? '')} />;
 */

export interface JsonEditorProps {
  value: string;
  onChange: (value: string) => void;
  readOnly?: boolean;
  placeholder?: string;
  impl?: 'textarea'; // extend to 'monaco' when ready
}

export function JsonEditor({ value, onChange, readOnly = false, placeholder, impl: _impl = 'textarea' }: JsonEditorProps) {
  return (
    <textarea
      value={value}
      onChange={e => onChange(e.target.value)}
      readOnly={readOnly}
      placeholder={placeholder}
      spellCheck={false}
      className="w-full h-full p-4 font-mono text-xs bg-[var(--bg)] text-[var(--t1)] resize-none focus:outline-none leading-relaxed"
      style={{ fontFamily: "'Consolas', 'Menlo', 'Courier New', monospace" }}
    />
  );
}

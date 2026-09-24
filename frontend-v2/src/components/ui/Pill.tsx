import React from 'react';

// Solid intents → solid BG, white text (status, role)
// Soft intents  → tint BG, vivid text (type labels, kind indicators)
export type PillIntent =
  | 'success' | 'failed' | 'processing' | 'idle'
  | 'dev' | 'admin'
  | 'text' | 'transform' | 'qr' | 'barcode' | 'image';

const INTENT_STYLES: Record<PillIntent, string> = {
  // solid
  success:    'bg-emerald-500 text-white',
  failed:     'bg-red-500 text-white',
  processing: 'bg-amber-400 text-white',
  idle:       'bg-slate-400 text-white',
  dev:        'bg-slate-600 text-white',
  admin:      'bg-violet-600 text-white',
  // soft
  text:       'bg-slate-100 text-slate-600',
  transform:  'bg-violet-100 text-violet-700',
  qr:         'bg-sky-100 text-sky-700',
  barcode:    'bg-amber-100 text-amber-700',
  image:      'bg-emerald-100 text-emerald-700',
};

export interface PillProps {
  intent: PillIntent;
  size?: 'sm' | 'md';
  children: React.ReactNode;
  className?: string;
}

export const Pill: React.FC<PillProps> = ({ intent, size = 'md', children, className = '' }) => {
  const sizeClass = size === 'sm' ? 'px-1.5 py-0.5 text-[10px]' : 'px-2 py-0.5 text-xs';
  return (
    <span className={`inline-flex items-center font-semibold rounded-sm ${sizeClass} ${INTENT_STYLES[intent]} ${className}`}>
      {children}
    </span>
  );
};

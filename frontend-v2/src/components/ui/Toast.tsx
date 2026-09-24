'use client';

import React, { useEffect } from 'react';
import { CheckCircle, AlertCircle, Info, X } from 'lucide-react';

export interface ToastProps {
  type?: 'success' | 'error' | 'info';
  message: string;
  onClose: () => void;
  duration?: number;
}

export const Toast: React.FC<ToastProps> = ({
  type = 'info',
  message,
  onClose,
  duration = 4000,
}) => {
  useEffect(() => {
    if (duration > 0) {
      const timer = setTimeout(onClose, duration);
      return () => clearTimeout(timer);
    }
  }, [duration, onClose]);

  const config = {
    success: {
      icon: CheckCircle,
      bg: 'bg-emerald-50 border-emerald-200 text-emerald-800',
      iconColor: 'text-emerald-600',
    },
    error: {
      icon: AlertCircle,
      bg: 'bg-red-50 border-red-200 text-red-800',
      iconColor: 'text-red-600',
    },
    info: {
      icon: Info,
      bg: 'bg-sky-50 border-sky-200 text-sky-800',
      iconColor: 'text-sky-600',
    },
  }[type];

  const Icon = config.icon;

  return (
    <div
      className={`fixed bottom-4 right-4 z-50 flex items-center gap-2.5 px-3 py-2 border rounded-sm shadow-md animate-in slide-in-from-bottom-2 duration-150 text-xs font-medium ${config.bg}`}
      role="alert"
    >
      <Icon className={`w-4 h-4 shrink-0 ${config.iconColor}`} />
      <span>{message}</span>
      <button
        onClick={onClose}
        className="ml-2 p-0.5 rounded-sm hover:opacity-75 transition-opacity text-current"
      >
        <X className="w-3.5 h-3.5" />
      </button>
    </div>
  );
};

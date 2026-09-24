"use client";
import { useEffect, useState } from "react";
import type { Toast, ToastIntent } from "@/hooks/useToast";
import { cn } from "@/utils/cn";

const intentStyle: Record<ToastIntent, { bar: string; icon: string }> = {
  success: { bar: "bg-[var(--emerald)]",   icon: "✓" },
  error:   { bar: "bg-[var(--rose)]",      icon: "✕" },
  warning: { bar: "bg-[var(--amber)]",     icon: "!" },
  info:    { bar: "bg-[var(--blue)]",      icon: "i" },
};

function ToastItem({ toast, onDismiss }: { toast: Toast; onDismiss: (id: string) => void }) {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const t = setTimeout(() => setVisible(true), 10);
    return () => clearTimeout(t);
  }, []);

  const style = intentStyle[toast.intent];

  return (
    <div
      className={cn(
        "flex items-center gap-3 bg-[var(--surf)] border border-[var(--border)]",
        "rounded shadow-sm px-4 py-3 min-w-[280px] max-w-[360px]",
        "transition-all duration-200",
        visible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-2"
      )}
    >
      <span className={cn("w-5 h-5 rounded-full flex items-center justify-center text-[10px] font-bold text-white flex-none", style.bar)}>
        {style.icon}
      </span>
      <p className="flex-1 text-[13px] text-[var(--t1)]">{toast.message}</p>
      <button
        onClick={() => onDismiss(toast.id)}
        className="text-[var(--t3)] hover:text-[var(--t1)] text-[16px] leading-none flex-none"
      >
        ×
      </button>
    </div>
  );
}

export function ToastContainer({ toasts, onDismiss }: { toasts: Toast[]; onDismiss: (id: string) => void }) {
  return (
    <div className="fixed bottom-5 right-5 z-[200] flex flex-col gap-2 items-end pointer-events-none">
      {toasts.map((t) => (
        <div key={t.id} className="pointer-events-auto">
          <ToastItem toast={t} onDismiss={onDismiss} />
        </div>
      ))}
    </div>
  );
}

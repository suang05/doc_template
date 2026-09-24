"use client";
import { ReactNode, useEffect, useRef } from "react";
import type { ModalSize } from "@/types/api";
import { cn } from "@/utils/cn";

const sizeClass: Record<ModalSize, string> = {
  sm:   "max-w-sm",
  md:   "max-w-lg",
  lg:   "max-w-3xl",
  full: "max-w-[95vw] h-[92vh]",
};

interface ModalProps {
  open: boolean;
  onClose: () => void;
  title?: string;
  size?: ModalSize;
  children: ReactNode;
  footer?: ReactNode;
}

export function Modal({ open, onClose, title, size = "md", children, footer }: ModalProps) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      style={{ background: "rgba(15,23,42,.45)" }}
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <div
        ref={ref}
        role="dialog"
        aria-modal
        className={cn(
          "bg-[var(--surf)] rounded border border-[var(--border)] flex flex-col w-full",
          sizeClass[size]
        )}
        style={{ maxHeight: size === "full" ? "92vh" : "90vh" }}
      >
        {title && (
          <div className="flex items-center justify-between px-5 py-3.5 border-b border-[var(--border)] flex-none">
            <span className="text-[14px] font-bold text-[var(--t1)]">{title}</span>
            <button
              onClick={onClose}
              className="w-7 h-7 flex items-center justify-center rounded-icon text-[var(--t3)] hover:bg-[var(--sep)] hover:text-[var(--t1)] transition-colors"
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                <path d="M18 6 6 18M6 6l12 12" />
              </svg>
            </button>
          </div>
        )}
        <div className="flex-1 overflow-y-auto min-h-0">{children}</div>
        {footer && (
          <div className="flex items-center justify-end gap-2 px-5 py-3 border-t border-[var(--border)] flex-none">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
}

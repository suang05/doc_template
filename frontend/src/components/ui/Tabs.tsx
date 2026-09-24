"use client";
import { ReactNode } from "react";
import { cn } from "@/utils/cn";

export interface TabItem {
  key: string;
  label: ReactNode;
  disabled?: boolean;
}

interface TabsProps {
  items: TabItem[];
  activeKey: string;
  onChange: (key: string) => void;
  className?: string;
}

export function Tabs({ items, activeKey, onChange, className }: TabsProps) {
  return (
    <div className={cn("flex border-b border-[var(--border)]", className)}>
      {items.map((item) => (
        <button
          key={item.key}
          onClick={() => !item.disabled && onChange(item.key)}
          className={cn(
            "px-4 py-2.5 text-[13px] font-medium border-b-2 -mb-px transition-colors",
            activeKey === item.key
              ? "border-[var(--blue)] text-[var(--blue)]"
              : "border-transparent text-[var(--t3)] hover:text-[var(--t2)]",
            item.disabled && "opacity-40 cursor-not-allowed"
          )}
        >
          {item.label}
        </button>
      ))}
    </div>
  );
}

import { ReactNode } from "react";
import { cn } from "@/utils/cn";

interface CardProps {
  title?: string;
  subtitle?: string;
  headerActions?: ReactNode;
  children: ReactNode;
  className?: string;
  padding?: "sm" | "md" | "lg";
  noPadding?: boolean;
}

const paddingClass = { sm: "p-3", md: "p-4", lg: "p-5" };

export function Card({
  title,
  subtitle,
  headerActions,
  children,
  className,
  padding = "md",
  noPadding = false,
}: CardProps) {
  const hasHeader = title || subtitle || headerActions;
  return (
    <div
      className={cn(
        "bg-[var(--surf)] border border-[var(--border)] rounded",
        className
      )}
    >
      {hasHeader && (
        <div className="flex items-start justify-between px-5 pt-3.5 pb-3 border-b border-[var(--sep)]">
          <div>
            {title && <div className="text-[13.5px] font-bold text-[var(--t1)]">{title}</div>}
            {subtitle && <div className="text-xs text-[var(--t3)] mt-0.5">{subtitle}</div>}
          </div>
          {headerActions && <div className="flex items-center gap-2">{headerActions}</div>}
        </div>
      )}
      <div className={noPadding ? "" : paddingClass[padding]}>{children}</div>
    </div>
  );
}

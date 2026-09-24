import { ReactNode } from "react";
import { cn } from "@/utils/cn";

interface StatTileProps {
  label: string;
  value: string;
  /** Optional icon element */
  icon?: ReactNode;
  /** CSS color value for icon background, e.g. "rgba(41,84,255,.1)" */
  iconBg?: string;
  /** CSS color value for icon, e.g. "var(--blue)" */
  iconColor?: string;
  /** Trend text e.g. "+12%" — omit to hide */
  trend?: string;
  /** true = green, false = red */
  trendUp?: boolean;
  loading?: boolean;
  onClick?: () => void;
  className?: string;
}

export function StatTile({
  label, value, icon, iconBg, iconColor,
  trend, trendUp, loading, onClick, className,
}: StatTileProps) {
  return (
    <div
      onClick={onClick}
      className={cn(
        "flex items-center gap-3 px-4 py-3 rounded-[var(--r)] border border-[var(--border)] bg-[var(--surf)]",
        onClick && "cursor-pointer hover:border-[var(--blue)] transition-colors",
        className
      )}
    >
      {icon && (
        <div
          className="w-8 h-8 rounded-[var(--rp)] flex items-center justify-center flex-none"
          style={{ background: iconBg, color: iconColor }}
        >
          {icon}
        </div>
      )}
      <div className="min-w-0 flex-1">
        {loading ? (
          <>
            <div className="w-14 h-5 rounded bg-[var(--sep)] animate-pulse mb-1" />
            <div className="w-20 h-3 rounded bg-[var(--sep)] animate-pulse" />
          </>
        ) : (
          <>
            <div className="flex items-baseline gap-2">
              <p className="text-[22px] font-extrabold text-[var(--t1)] leading-none tracking-tight tabular-nums">
                {value}
              </p>
              {trend && (
                <span className={cn(
                  "text-t-xs font-semibold leading-none",
                  trendUp ? "text-[var(--emerald)]" : "text-[var(--rose)]"
                )}>
                  {trend}
                </span>
              )}
            </div>
            <p className="text-t-xs text-[var(--t3)] mt-1 leading-none">{label}</p>
          </>
        )}
      </div>
    </div>
  );
}

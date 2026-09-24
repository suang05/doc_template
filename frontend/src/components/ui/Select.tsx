import { SelectHTMLAttributes } from "react";
import type { SelectOption } from "@/types/api";
import { cn } from "@/utils/cn";

interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  options: SelectOption[];
  placeholder?: string;
  error?: string;
}

export function Select({ label, options, placeholder, error, className, ...props }: SelectProps) {
  return (
    <div className="flex flex-col gap-1">
      {label && <label className="text-xs font-semibold text-[var(--t2)]">{label}</label>}
      <select
        {...props}
        className={cn(
          "px-3 py-1.5 rounded-btn border bg-[var(--surf)] text-[13px] text-[var(--t1)] outline-none appearance-none cursor-pointer",
          error
            ? "border-[var(--rose)]"
            : "border-[var(--border)] focus:border-[var(--blue)] focus:outline focus:outline-2 focus:outline-[var(--blue-t)]",
          className
        )}
      >
        {placeholder && <option value="">{placeholder}</option>}
        {options.map((o) => (
          <option key={o.value} value={o.value}>{o.label}</option>
        ))}
      </select>
      {error && <span className="text-xs text-[var(--rose)]">{error}</span>}
    </div>
  );
}

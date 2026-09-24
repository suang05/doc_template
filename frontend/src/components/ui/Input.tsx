import { InputHTMLAttributes, ReactNode, TextareaHTMLAttributes } from "react";
import { cn } from "@/utils/cn";

interface InputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "prefix"> {
  label?: string;
  error?: string;
  prefix?: ReactNode;
  suffix?: ReactNode;
}

export function Input({ label, error, prefix, suffix, className, ...props }: InputProps) {
  return (
    <div className="flex flex-col gap-1">
      {label && <label className="text-xs font-semibold text-[var(--t2)]">{label}</label>}
      <div
        className={cn(
          "flex items-center gap-2 px-3 py-1.5 rounded-btn border bg-[var(--surf)] text-[13px]",
          error
            ? "border-[var(--rose)] outline outline-2 outline-[var(--rose-t)]"
            : "border-[var(--border)] focus-within:border-[var(--blue)] focus-within:outline focus-within:outline-2 focus-within:outline-[var(--blue-t)]",
          className
        )}
      >
        {prefix && <span className="text-[var(--t3)] flex-none">{prefix}</span>}
        <input
          {...props}
          className="flex-1 bg-transparent outline-none text-[var(--t1)] placeholder:text-[var(--t3)] min-w-0"
        />
        {suffix && <span className="text-[var(--t3)] flex-none">{suffix}</span>}
      </div>
      {error && <span className="text-xs text-[var(--rose)]">{error}</span>}
    </div>
  );
}

interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: string;
  error?: string;
}

export function Textarea({ label, error, className, ...props }: TextareaProps) {
  return (
    <div className="flex flex-col gap-1">
      {label && <label className="text-xs font-semibold text-[var(--t2)]">{label}</label>}
      <textarea
        {...props}
        className={cn(
          "px-3 py-2 rounded-btn border bg-[var(--surf)] text-[13px] text-[var(--t1)] placeholder:text-[var(--t3)] outline-none resize-none",
          error
            ? "border-[var(--rose)] outline outline-2 outline-[var(--rose-t)]"
            : "border-[var(--border)] focus:border-[var(--blue)] focus:outline focus:outline-2 focus:outline-[var(--blue-t)]",
          className
        )}
      />
      {error && <span className="text-xs text-[var(--rose)]">{error}</span>}
    </div>
  );
}

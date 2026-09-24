"use client";
import { ButtonHTMLAttributes, ReactNode } from "react";
import type { ButtonVariant, ButtonSize } from "@/types/api";
import { cn } from "@/utils/cn";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  loading?: boolean;
  icon?: ReactNode;
  children?: ReactNode;
}

const variantClass: Record<ButtonVariant, string> = {
  primary: "bg-[var(--blue)] text-white hover:brightness-110",
  navy:    "bg-[var(--navy)] text-white hover:brightness-125",
  outline: "bg-[var(--surf)] border border-[var(--border)] text-[var(--t2)] hover:border-[var(--blue)] hover:text-[var(--blue)]",
  ghost:   "bg-transparent text-[var(--t2)] hover:bg-[var(--sep)]",
  danger:  "bg-[var(--rose-t)] text-[var(--rose)] hover:brightness-95",
};

const sizeClass: Record<ButtonSize, string> = {
  sm: "px-2.5 py-1.5 text-xs gap-1.5",
  md: "px-3.5 py-1.5 text-[13px] gap-2",
  lg: "px-4 py-2.5 text-sm gap-2",
};

export function Button({
  variant = "primary",
  size = "md",
  loading = false,
  icon,
  children,
  className,
  disabled,
  ...props
}: ButtonProps) {
  return (
    <button
      {...props}
      disabled={disabled || loading}
      className={cn(
        "inline-flex items-center justify-center font-semibold rounded-btn transition-all duration-100 cursor-pointer select-none",
        variantClass[variant],
        sizeClass[size],
        (disabled || loading) && "opacity-50 cursor-not-allowed",
        className
      )}
    >
      {loading ? <Spinner size={size} /> : icon}
      {children}
    </button>
  );
}

function Spinner({ size }: { size: ButtonSize }) {
  const s = size === "sm" ? 12 : size === "lg" ? 16 : 14;
  return (
    <svg width={s} height={s} viewBox="0 0 24 24" fill="none" className="animate-spin">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="3" strokeOpacity=".25" />
      <path d="M22 12a10 10 0 0 0-10-10" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}

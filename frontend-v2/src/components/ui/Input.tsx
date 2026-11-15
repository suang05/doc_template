'use client';

import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { LucideIcon } from 'lucide-react';
import { cn } from '@/utils/cn';

export const inputVariants = cva(
  'w-full h-8 rounded-sm bg-surface border text-xs text-textPrimary placeholder:text-textMuted transition-colors focus:outline-none focus:ring-1 pr-2.5 disabled:opacity-50 disabled:cursor-not-allowed',
  {
    variants: {
      hasError: {
        true: 'border-red-500 focus:border-red-500 focus:ring-red-500',
        false: 'border-border focus:border-primary focus:ring-primary',
      },
      hasIcon: {
        true: 'pl-8',
        false: 'px-2.5',
      },
    },
    defaultVariants: {
      hasError: false,
      hasIcon: false,
    },
  }
);

export interface InputProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'size'>,
    VariantProps<typeof inputVariants> {
  label?: string;
  error?: string;
  icon?: LucideIcon;
  helperText?: string;
}

export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ label, error, icon: Icon, helperText, className, id, 'aria-describedby': ariaDescribedBy, ...props }, ref) => {
    const generatedId = React.useId();
    const inputId = id || (label ? `${label.toLowerCase().replace(/\s+/g, '-')}-${generatedId}` : generatedId);
    const errorId = `${inputId}-error`;
    const helperId = `${inputId}-helper`;
    const describedBy =
      [
        ariaDescribedBy,
        error ? errorId : helperText ? helperId : undefined,
      ]
        .filter(Boolean)
        .join(' ') || undefined;

    return (
      <div className="w-full space-y-1">
        {label && (
          <label htmlFor={inputId} className="block text-xs font-medium text-textSecondary">
            {label}
          </label>
        )}

        <div className="relative flex items-center">
          {Icon && (
            <div className="absolute left-2.5 pointer-events-none text-textMuted">
              <Icon className="w-3.5 h-3.5" />
            </div>
          )}

          <input
            ref={ref}
            id={inputId}
            aria-invalid={error ? 'true' : 'false'}
            aria-describedby={describedBy}
            className={cn(
              inputVariants({
                hasError: Boolean(error),
                hasIcon: Boolean(Icon),
              }),
              className
            )}
            {...props}
          />
        </div>

        {error ? (
          <p id={errorId} className="text-[11px] text-red-600 leading-none">
            {error}
          </p>
        ) : helperText ? (
          <p id={helperId} className="text-[11px] text-textMuted leading-none">
            {helperText}
          </p>
        ) : null}
      </div>
    );
  }
);

Input.displayName = 'Input';

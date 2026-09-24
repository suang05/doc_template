import React from 'react';
import { LucideIcon } from 'lucide-react';

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  icon?: LucideIcon;
  helperText?: string;
}

export const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ label, error, icon: Icon, helperText, className = '', id, ...props }, ref) => {
    const inputId = id || (label ? label.toLowerCase().replace(/\s+/g, '-') : undefined);

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
            className={`w-full h-8 rounded-sm bg-surface border text-xs text-textPrimary placeholder:text-textMuted transition-colors focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary ${
              Icon ? 'pl-8' : 'px-2.5'
            } pr-2.5 ${error ? 'border-red-500 focus:border-red-500 focus:ring-red-500' : 'border-border'} ${className}`}
            {...props}
          />
        </div>

        {error ? (
          <p className="text-[11px] text-red-600 leading-none">{error}</p>
        ) : helperText ? (
          <p className="text-[11px] text-textMuted leading-none">{helperText}</p>
        ) : null}
      </div>
    );
  }
);

Input.displayName = 'Input';

import React from 'react';

export interface SelectOption {
  value: string;
  label: string;
}

export interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  error?: string;
  options: SelectOption[];
  helperText?: string;
}

export const Select = React.forwardRef<HTMLSelectElement, SelectProps>(
  ({ label, error, options, helperText, className = '', id, ...props }, ref) => {
    const selectId = id || (label ? label.toLowerCase().replace(/\s+/g, '-') : undefined);

    return (
      <div className="w-full space-y-1">
        {label && (
          <label htmlFor={selectId} className="block text-xs font-medium text-textSecondary">
            {label}
          </label>
        )}

        <div className="relative">
          <select
            ref={ref}
            id={selectId}
            className={`w-full h-8 px-2.5 rounded-sm bg-surface border text-xs text-textPrimary transition-colors focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary ${
              error ? 'border-red-500 focus:border-red-500 focus:ring-red-500' : 'border-border'
            } ${className}`}
            {...props}
          >
            {options.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
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

Select.displayName = 'Select';

import * as React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { LucideIcon, Loader2 } from 'lucide-react';
import { cn } from '@/utils/cn';

export const buttonVariants = cva(
  'inline-flex items-center justify-center font-sans font-medium transition-all duration-150 select-none cursor-pointer disabled:pointer-events-none disabled:opacity-50 disabled:cursor-not-allowed rounded-sm',
  {
    variants: {
      variant: {
        primary: 'bg-primary text-white hover:bg-primaryDark border border-transparent shadow-sm',
        secondary: 'bg-surfaceSubtle text-textSecondary hover:bg-slate-200/70 border border-border',
        outline: 'bg-surface text-textPrimary hover:bg-surfaceSubtle border border-border',
        ghost: 'bg-transparent text-textSecondary hover:bg-surfaceSubtle hover:text-textPrimary border border-transparent',
        danger: 'bg-red-600 text-white hover:bg-red-700 border border-transparent shadow-sm',
        destructive: 'bg-red-600 text-white hover:bg-red-700 border border-transparent shadow-sm',
        success: 'bg-emerald-600 text-white hover:bg-emerald-700 border border-transparent shadow-sm',
      },
      size: {
        sm: 'h-7 px-2.5 text-xs gap-1.5',
        md: 'h-8 px-3 text-xs font-medium gap-2',
        lg: 'h-9 px-4 text-sm font-medium gap-2.5',
        icon: 'h-8 w-8 p-0',
      },
    },
    defaultVariants: {
      variant: 'primary',
      size: 'md',
    },
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  loading?: boolean;
  icon?: LucideIcon;
  iconPosition?: 'left' | 'right';
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  (
    {
      children,
      className,
      variant,
      size,
      loading = false,
      disabled = false,
      icon: Icon,
      iconPosition = 'left',
      ...props
    },
    ref
  ) => {
    return (
      <button
        ref={ref}
        disabled={disabled || loading}
        className={cn(buttonVariants({ variant, size }), className)}
        {...props}
      >
        {loading ? (
          <Loader2 className="w-3.5 h-3.5 animate-spin" />
        ) : (
          Icon && iconPosition === 'left' && <Icon className="w-3.5 h-3.5 shrink-0" />
        )}

        {children && <span>{children}</span>}

        {!loading && Icon && iconPosition === 'right' && (
          <Icon className="w-3.5 h-3.5 shrink-0" />
        )}
      </button>
    );
  }
);

Button.displayName = 'Button';

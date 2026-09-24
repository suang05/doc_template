import React, { useState, useRef, useEffect } from 'react';
import { LucideIcon, MoreVertical } from 'lucide-react';

export interface DropdownItem {
  id: string;
  label: string;
  icon?: LucideIcon;
  danger?: boolean;
  onClick: () => void;
}

export interface DropdownProps {
  items: DropdownItem[];
  triggerIcon?: LucideIcon;
  className?: string;
  align?: 'left' | 'right';
  side?: 'top' | 'bottom';
}

export const Dropdown: React.FC<DropdownProps> = ({
  items,
  triggerIcon: TriggerIcon = MoreVertical,
  className = '',
  align = 'left',
  side = 'bottom',
}) => {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  const sideClass = side === 'top' ? 'bottom-full mb-1' : 'top-full mt-1';
  const alignClass = align === 'right' ? 'right-0' : 'left-0';
  const originClass = `origin-${side === 'top' ? 'bottom' : 'top'}-${align === 'right' ? 'right' : 'left'}`;
  const menuClasses = `absolute w-44 ${sideClass} ${alignClass} ${originClass} rounded-sm bg-surface border border-border shadow-md z-50 focus:outline-none`;

  return (
    <div className={`relative inline-block text-left ${className}`} ref={dropdownRef}>
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          setIsOpen(!isOpen);
        }}
        className="p-1 rounded-sm text-textSecondary hover:text-textPrimary hover:bg-surfaceSubtle transition-colors cursor-pointer outline-none"
      >
        <TriggerIcon className="w-4 h-4" />
      </button>

      {isOpen && (
        <div className={menuClasses}>
          <div className="py-1">
            {items.map((item) => {
              const Icon = item.icon;
              return (
                <button
                  key={item.id}
                  onClick={(e) => {
                    e.stopPropagation();
                    setIsOpen(false);
                    item.onClick();
                  }}
                  className={`w-full text-left px-3 py-1.5 text-xs font-medium flex items-center gap-2 hover:bg-surfaceSubtle cursor-pointer ${
                    item.danger ? 'text-red-600 hover:text-red-700' : 'text-textPrimary'
                  }`}
                >
                  {Icon && <Icon className="w-3.5 h-3.5 shrink-0" />}
                  {item.label}
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
};

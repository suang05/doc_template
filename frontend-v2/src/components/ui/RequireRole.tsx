'use client';

import React from 'react';
import { ShieldOff } from 'lucide-react';
import { useAuthContext } from '@/contexts/AuthContext';
import { hasRole, isSuperAdmin as checkSuperAdmin } from '@/lib/rbac';
import type { UserRole } from '@/constants/domain';

interface RequireRoleProps {
  /** Minimum project role required — enforced against UserRole union */
  role?: UserRole;
  /** Require platform-wide SystemRole.SuperAdmin */
  superAdminOnly?: boolean;
  children: React.ReactNode;
  /** Custom fallback — defaults to AccessDenied card */
  fallback?: React.ReactNode;
}

function AccessDenied({ message }: { message?: string }) {
  return (
    <div className="flex h-full items-center justify-center pt-20">
      <div className="text-center space-y-3">
        <div className="inline-flex items-center justify-center w-10 h-10 rounded-full bg-red-50 border border-red-200">
          <ShieldOff className="w-5 h-5 text-red-500" />
        </div>
        <div className="space-y-1">
          <h3 className="text-sm font-semibold text-textPrimary">ไม่มีสิทธิ์เข้าถึง</h3>
          <p className="text-xs text-textMuted">
            {message || 'บัญชีของคุณไม่มีสิทธิ์ดูหน้านี้'}
          </p>
        </div>
      </div>
    </div>
  );
}

export function RequireRole({
  role = 'Viewer',
  superAdminOnly = false,
  children,
  fallback,
}: RequireRoleProps) {
  const { role: userRole, systemRole } = useAuthContext();

  if (superAdminOnly && !checkSuperAdmin(systemRole)) {
    return (
      <>
        {fallback ?? (
          <AccessDenied message="หน้านี้จำกัดเฉพาะผู้ดูแลแพลตฟอร์มระดับ SuperAdmin เท่านั้น" />
        )}
      </>
    );
  }

  if (!superAdminOnly && !hasRole(userRole, role)) {
    return <>{fallback ?? <AccessDenied />}</>;
  }

  return <>{children}</>;
}

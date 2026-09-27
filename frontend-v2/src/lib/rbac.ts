import type { NavigationItemId } from '@/components/layout/Sidebar';
import type { UserRole } from '@/types/api';

// Higher number = higher privilege — Record<UserRole> ensures compile-time sync with schema
export const ROLE_LEVEL: Record<UserRole, number> = {
  Viewer: 1,
  Editor: 2,
  Admin: 3,
};

/** Minimum role required to access each nav item */
export const NAV_MIN_ROLE: Record<NavigationItemId, UserRole> = {
  generator:        'Viewer',
  audit:            'Viewer',
  apidocs:          'Viewer',
  templates:        'Editor',
  studio:           'Editor',
  upload:           'Editor',
  mapping:          'Editor',
  'version-history':'Editor',
  logs:             'Admin',
  analytics:        'Admin',
  datasources:      'Admin',
  projects:         'Admin',
  users:            'Admin',
  settings:         'Admin',
};

/** True if userRole meets or exceeds the required role */
export function hasRole(userRole: string | null, required: UserRole): boolean {
  if (!userRole) return false;
  return (ROLE_LEVEL[userRole as UserRole] ?? 0) >= ROLE_LEVEL[required];
}

/** True if userRole can access the given nav item */
export function canAccess(userRole: string | null, id: NavigationItemId): boolean {
  return hasRole(userRole, NAV_MIN_ROLE[id] ?? 'Admin');
}

import type { NavigationItemId } from '@/components/layout/Sidebar';
import type { UserRole } from '@/constants/domain';

// Higher number = higher privilege — Record<UserRole> ensures compile-time sync with schema
const ROLE_LEVEL: Record<UserRole, number> = {
  Viewer: 1,
  Developer: 2,
  Admin: 3,
};

export const SUPER_ADMIN_ONLY_ITEMS: ReadonlySet<NavigationItemId> = new Set([
  'projects',
]);

/** Minimum project role required to access each nav item */
const NAV_MIN_ROLE: Record<NavigationItemId, UserRole> = {
  generator: 'Viewer',
  jobs: 'Viewer',
  audit: 'Viewer',
  apidocs: 'Viewer',
  templates: 'Developer',
  studio: 'Developer',
  upload: 'Developer',
  mapping: 'Developer',
  'version-history': 'Developer',
  logs: 'Admin',
  analytics: 'Admin',
  datasources: 'Admin',
  'api-keys': 'Admin',
  projects: 'Admin',
  users: 'Admin',
  settings: 'Admin',
};

export function isSuperAdmin(systemRole: string | null | undefined): boolean {
  return systemRole === 'SuperAdmin';
}

/** True if userRole meets or exceeds the required role */
export function hasRole(userRole: string | null, required: UserRole): boolean {
  if (!userRole) return false;
  return (ROLE_LEVEL[userRole as UserRole] ?? 0) >= ROLE_LEVEL[required];
}

/** True if user can access the given nav item */
export function canAccess(
  userRole: string | null,
  id: NavigationItemId,
  systemRole?: string | null,
): boolean {
  if (SUPER_ADMIN_ONLY_ITEMS.has(id)) {
    return isSuperAdmin(systemRole);
  }
  return hasRole(userRole, NAV_MIN_ROLE[id] ?? 'Admin');
}

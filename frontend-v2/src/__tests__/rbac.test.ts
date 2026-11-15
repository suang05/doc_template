import { describe, it, expect } from 'vitest';
import {
  hasRole,
  canAccess,
  isSuperAdmin,
  SUPER_ADMIN_ONLY_ITEMS,
} from '@/lib/rbac';
import type { NavigationItemId } from '@/components/layout/Sidebar';
import type { UserRole } from '@/constants/domain';

describe('hasRole', () => {
  it.each([
    [null, 'Viewer', false],
    ['', 'Viewer', false],
    ['SuperUser', 'Viewer', false],
    ['Viewer', 'Viewer', true],
    ['Viewer', 'Developer', false],
    ['Viewer', 'Admin', false],
    ['Developer', 'Viewer', true],
    ['Developer', 'Developer', true],
    ['Developer', 'Admin', false],
    ['Admin', 'Viewer', true],
    ['Admin', 'Developer', true],
    ['Admin', 'Admin', true],
  ] as const)('evaluates userRole "%s" against required "%s" -> %s', (userRole, required, expected) => {
    expect(hasRole(userRole, required as UserRole)).toBe(expected);
  });
});

describe('canAccess', () => {
  it.each([
    [null, 'generator', false],
    [null, 'templates', false],
    [null, 'logs', false],
    ['Viewer', 'generator', true],
    ['Viewer', 'audit', true],
    ['Viewer', 'apidocs', true],
    ['Viewer', 'templates', false],
    ['Viewer', 'studio', false],
    ['Viewer', 'logs', false],
    ['Developer', 'templates', true],
    ['Developer', 'studio', true],
    ['Developer', 'mapping', true],
    ['Developer', 'upload', true],
    ['Developer', 'logs', false],
    ['Developer', 'users', false],
    ['Admin', 'generator', true],
    ['Admin', 'templates', true],
    ['Admin', 'logs', true],
    ['Admin', 'settings', true],
  ] as const)('evaluates userRole "%s" accessing route "%s" -> %s', (userRole, route, expected) => {
    expect(canAccess(userRole, route as NavigationItemId)).toBe(expected);
  });
});

describe('SuperAdmin RBAC Controls', () => {
  it('identifies SuperAdmin systemRole strictly', () => {
    expect(isSuperAdmin('SuperAdmin')).toBe(true);
    expect(isSuperAdmin('superadmin')).toBe(false);
    expect(isSuperAdmin('PortalUser')).toBe(false);
    expect(isSuperAdmin(null)).toBe(false);
    expect(isSuperAdmin(undefined)).toBe(false);
  });

  it('restricts SUPER_ADMIN_ONLY_ITEMS (projects) from non-SuperAdmin users', () => {
    expect(SUPER_ADMIN_ONLY_ITEMS.has('projects')).toBe(true);
    expect(canAccess('Admin', 'projects')).toBe(false);
    expect(canAccess('Admin', 'projects', 'PortalUser')).toBe(false);
    expect(canAccess('Admin', 'projects', 'SuperAdmin')).toBe(true);
    expect(canAccess('Viewer', 'projects', 'SuperAdmin')).toBe(true);
    expect(canAccess(null, 'projects', 'SuperAdmin')).toBe(true);
  });
});

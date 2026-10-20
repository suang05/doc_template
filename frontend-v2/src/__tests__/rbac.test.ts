import { describe, it, expect } from 'vitest';
import { hasRole, canAccess } from '@/lib/rbac';
import type { NavigationItemId } from '@/components/layout/Sidebar';
import type { UserRole } from '@/types/api';

describe('hasRole', () => {
  it.each([
    [null, 'Viewer', false],
    ['', 'Viewer', false],
    ['SuperUser', 'Viewer', false],
    ['Viewer', 'Viewer', true],
    ['Viewer', 'Editor', false],
    ['Viewer', 'Admin', false],
    ['Editor', 'Viewer', true],
    ['Editor', 'Editor', true],
    ['Editor', 'Admin', false],
    ['Admin', 'Viewer', true],
    ['Admin', 'Editor', true],
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
    ['Editor', 'templates', true],
    ['Editor', 'studio', true],
    ['Editor', 'mapping', true],
    ['Editor', 'upload', true],
    ['Editor', 'logs', false],
    ['Editor', 'users', false],
    ['Admin', 'generator', true],
    ['Admin', 'templates', true],
    ['Admin', 'logs', true],
    ['Admin', 'settings', true],
  ] as const)('evaluates userRole "%s" accessing route "%s" -> %s', (userRole, route, expected) => {
    expect(canAccess(userRole, route as NavigationItemId)).toBe(expected);
  });
});

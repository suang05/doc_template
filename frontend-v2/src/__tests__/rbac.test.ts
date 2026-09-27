import { describe, it, expect } from 'vitest';
import { hasRole, canAccess } from '@/lib/rbac';

describe('hasRole', () => {
  it('returns false when userRole is null', () => {
    expect(hasRole(null, 'Viewer')).toBe(false);
  });

  it('returns false when userRole is empty string', () => {
    expect(hasRole('', 'Viewer')).toBe(false);
  });

  it('Viewer satisfies Viewer', () => {
    expect(hasRole('Viewer', 'Viewer')).toBe(true);
  });

  it('Viewer does not satisfy Editor', () => {
    expect(hasRole('Viewer', 'Editor')).toBe(false);
  });

  it('Viewer does not satisfy Admin', () => {
    expect(hasRole('Viewer', 'Admin')).toBe(false);
  });

  it('Editor satisfies Viewer', () => {
    expect(hasRole('Editor', 'Viewer')).toBe(true);
  });

  it('Editor satisfies Editor', () => {
    expect(hasRole('Editor', 'Editor')).toBe(true);
  });

  it('Editor does not satisfy Admin', () => {
    expect(hasRole('Editor', 'Admin')).toBe(false);
  });

  it('Admin satisfies all roles', () => {
    expect(hasRole('Admin', 'Viewer')).toBe(true);
    expect(hasRole('Admin', 'Editor')).toBe(true);
    expect(hasRole('Admin', 'Admin')).toBe(true);
  });

  it('unknown role returns false', () => {
    expect(hasRole('SuperUser', 'Viewer')).toBe(false);
  });
});

describe('canAccess', () => {
  it('null user cannot access any nav item', () => {
    expect(canAccess(null, 'generator')).toBe(false);
    expect(canAccess(null, 'templates')).toBe(false);
    expect(canAccess(null, 'logs')).toBe(false);
  });

  it('Viewer can access Viewer-gated items', () => {
    expect(canAccess('Viewer', 'generator')).toBe(true);
    expect(canAccess('Viewer', 'audit')).toBe(true);
    expect(canAccess('Viewer', 'apidocs')).toBe(true);
  });

  it('Viewer cannot access Editor-gated items', () => {
    expect(canAccess('Viewer', 'templates')).toBe(false);
    expect(canAccess('Viewer', 'studio')).toBe(false);
    expect(canAccess('Viewer', 'mapping')).toBe(false);
  });

  it('Viewer cannot access Admin-gated items', () => {
    expect(canAccess('Viewer', 'logs')).toBe(false);
    expect(canAccess('Viewer', 'users')).toBe(false);
    expect(canAccess('Viewer', 'settings')).toBe(false);
  });

  it('Editor can access Editor-gated items', () => {
    expect(canAccess('Editor', 'templates')).toBe(true);
    expect(canAccess('Editor', 'studio')).toBe(true);
    expect(canAccess('Editor', 'mapping')).toBe(true);
    expect(canAccess('Editor', 'upload')).toBe(true);
  });

  it('Editor cannot access Admin-gated items', () => {
    expect(canAccess('Editor', 'logs')).toBe(false);
    expect(canAccess('Editor', 'users')).toBe(false);
    expect(canAccess('Editor', 'datasources')).toBe(false);
  });

  it('Admin can access all nav items', () => {
    expect(canAccess('Admin', 'generator')).toBe(true);
    expect(canAccess('Admin', 'templates')).toBe(true);
    expect(canAccess('Admin', 'logs')).toBe(true);
    expect(canAccess('Admin', 'users')).toBe(true);
    expect(canAccess('Admin', 'settings')).toBe(true);
  });
});

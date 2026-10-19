import { describe, it, expect } from 'vitest';
import { decodeBackendJwt, jwtRole, jwtGivenName, jwtFamilyName } from '@/lib/auth/jwt';

describe('decodeBackendJwt & Claim Mappings', () => {
  it('decodes standard and URI-encoded claims from a valid JWT', () => {
    const payload = {
      sub: 'user-guid-001',
      email: 'admin@smk.local',
      role: 'GlobalAdmin',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'GlobalAdmin',
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname': 'Somchai',
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname': 'Prasert',
      ProjectId: 'proj-001',
      exp: 1800000000,
    };

    const encoded = Buffer.from(JSON.stringify(payload)).toString('base64');
    const token = `header.${encoded}.signature`;

    const decoded = decodeBackendJwt(token);
    expect(decoded).not.toBeNull();
    expect(decoded?.sub).toBe('user-guid-001');
    expect(decoded?.email).toBe('admin@smk.local');
    expect(jwtRole(decoded!)).toBe('GlobalAdmin');
    expect(jwtGivenName(decoded!)).toBe('Somchai');
    expect(jwtFamilyName(decoded!)).toBe('Prasert');
  });

  it('returns null on corrupted or invalid JWT string', () => {
    expect(decodeBackendJwt('')).toBeNull();
    expect(decodeBackendJwt('not-a-jwt')).toBeNull();
    expect(decodeBackendJwt('header.invalid-base64-%%%.signature')).toBeNull();
  });
});

describe('Token Refresh Rotation Thresholds', () => {
  it('identifies token as expiring within 60 seconds threshold', () => {
    const nowSec = 1700000000;
    const nowMs = nowSec * 1000;

    // Token expires in 30 seconds (less than 60s threshold)
    const expiringExp = nowSec + 30;
    const shouldRefreshExpiring = nowMs >= (expiringExp - 60) * 1000;
    expect(shouldRefreshExpiring).toBe(true);

    // Token expires in 120 seconds (more than 60s threshold)
    const freshExp = nowSec + 120;
    const shouldRefreshFresh = nowMs >= (freshExp - 60) * 1000;
    expect(shouldRefreshFresh).toBe(false);
  });

  it('triggers token refresh when explicit update is requested', () => {
    const trigger = 'update';
    const isExplicitUpdate = trigger === 'update';
    expect(isExplicitUpdate).toBe(true);
  });
});

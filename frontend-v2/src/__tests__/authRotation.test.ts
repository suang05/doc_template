import { describe, it, expect } from 'vitest';
import {
  decodeBackendJwt,
  jwtRole,
  jwtGivenName,
  jwtFamilyName,
  shouldRefreshBackendToken,
} from '@/lib/auth/jwt';

describe('decodeBackendJwt & Claim Mappings', () => {
  it('decodes standard short claims from a valid JWT', () => {
    const payload = {
      sub: 'user-guid-001',
      email: 'admin@smk.local',
      role: 'GlobalAdmin',
      given_name: 'Somchai',
      family_name: 'Prasert',
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

  it('falls back to Microsoft URI-encoded claims when standard short claims are absent', () => {
    const payload = {
      sub: 'user-guid-002',
      email: 'user@smk.local',
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Editor',
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname': 'Kanya',
      'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname': 'Suksawat',
      exp: 1800000000,
    };

    const encoded = Buffer.from(JSON.stringify(payload)).toString('base64');
    const token = `header.${encoded}.signature`;

    const decoded = decodeBackendJwt(token);
    expect(decoded).not.toBeNull();
    expect(jwtRole(decoded!)).toBe('Editor');
    expect(jwtGivenName(decoded!)).toBe('Kanya');
    expect(jwtFamilyName(decoded!)).toBe('Suksawat');
  });

  it('returns null on corrupted or invalid JWT string', () => {
    expect(decodeBackendJwt('')).toBeNull();
    expect(decodeBackendJwt('not-a-jwt')).toBeNull();
    expect(decodeBackendJwt('header.invalid-base64-%%%.signature')).toBeNull();
  });
});

describe('shouldRefreshBackendToken', () => {
  const baseTimeSec = 1700000000;
  const baseTimeMs = baseTimeSec * 1000;

  it.each([
    {
      scenario: 'token expiring within threshold (30s remaining)',
      exp: baseTimeSec + 30,
      trigger: undefined,
      expected: true,
    },
    {
      scenario: 'token at exact threshold boundary (60s remaining)',
      exp: baseTimeSec + 60,
      trigger: undefined,
      expected: true,
    },
    {
      scenario: 'fresh token well beyond threshold (120s remaining)',
      exp: baseTimeSec + 120,
      trigger: undefined,
      expected: false,
    },
    {
      scenario: 'already expired token (-10s remaining)',
      exp: baseTimeSec - 10,
      trigger: undefined,
      expected: true,
    },
    {
      scenario: 'fresh token with explicit update trigger',
      exp: baseTimeSec + 3600,
      trigger: 'update',
      expected: true,
    },
    {
      scenario: 'fresh token with non-update trigger',
      exp: baseTimeSec + 3600,
      trigger: 'signIn',
      expected: false,
    },
  ])(
    'evaluates $scenario -> $expected',
    ({ exp, trigger, expected }) => {
      expect(shouldRefreshBackendToken(exp, trigger, baseTimeMs)).toBe(expected);
    },
  );

  it('uses Date.now() default parameter when nowMs is omitted', () => {
    const futureExp = Math.floor(Date.now() / 1000) + 3600;
    expect(shouldRefreshBackendToken(futureExp)).toBe(false);
  });
});

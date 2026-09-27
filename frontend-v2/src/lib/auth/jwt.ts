// Decodes the backend's JWT payload — SSoT for reading claims out of the raw token.
// ClaimTypes.Role/GivenName/Surname serialize to these URLs when MapInboundClaims=true.
const CLAIMS_ROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
const CLAIMS_GIVEN_NAME = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname';
const CLAIMS_FAMILY_NAME = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname';

export interface BackendJwtPayload {
  sub: string;
  email: string;
  given_name?: string;
  family_name?: string;
  role?: string;
  SystemRole?: string;
  ProjectId?: string;
  exp: number;

  [CLAIMS_ROLE]?: string;
  [CLAIMS_GIVEN_NAME]?: string;
  [CLAIMS_FAMILY_NAME]?: string;
}

export function decodeBackendJwt(token: string): BackendJwtPayload | null {
  try {
    const part = token.split('.')[1];
    const json = Buffer.from(
      part.replace(/-/g, '+').replace(/_/g, '/'),
      'base64',
    ).toString('utf-8');
    return JSON.parse(json);
  } catch {
    return null;
  }
}

export function jwtRole(payload: BackendJwtPayload): string | null {
  return payload.role ?? payload[CLAIMS_ROLE] ?? null;
}

export function jwtGivenName(payload: BackendJwtPayload): string {
  return payload.given_name ?? payload[CLAIMS_GIVEN_NAME] ?? '';
}

export function jwtFamilyName(payload: BackendJwtPayload): string {
  return payload.family_name ?? payload[CLAIMS_FAMILY_NAME] ?? '';
}

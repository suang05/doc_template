import { NextRequest, NextResponse } from 'next/server';
import {
  applyRotatedTokens,
  attachSessionCookie,
  bffProblemJson,
  clearSessionCookies,
  readSessionJwt,
  rotateBackendRefreshToken,
} from '@/lib/auth/refreshCoordinator';

export async function POST(req: NextRequest) {
  const sessionJwt = await readSessionJwt(req);

  if (!sessionJwt?.token?.refreshToken) {
    const res = bffProblemJson(401, 'Unauthorized', 'No valid session refresh token available');
    clearSessionCookies(res);
    return res;
  }

  try {
    const rotated = await rotateBackendRefreshToken(sessionJwt.token.refreshToken);
    const updatedToken = applyRotatedTokens(sessionJwt.token, rotated);

    const res = NextResponse.json({
      ok: true,
      expiresIn: rotated.expiresIn,
      exp: rotated.exp,
      projectId: updatedToken.projectId ?? null,
      systemRole: updatedToken.systemRole ?? null,
    });

    await attachSessionCookie(res, sessionJwt.cookieName, updatedToken);
    return res;
  } catch {
    const res = bffProblemJson(401, 'Session Expired', 'Refresh token is expired or revoked');
    clearSessionCookies(res);
    return res;
  }
}

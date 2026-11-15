import { NextRequest, NextResponse } from 'next/server';
import type { JWT } from 'next-auth/jwt';
import {
  attachSessionCookie,
  clearSessionCookies,
  readSessionJwt,
  rotateBackendRefreshToken,
} from '@/lib/auth/refreshCoordinator';

export async function POST(req: NextRequest) {
  const sessionJwt = await readSessionJwt(req);

  if (!sessionJwt?.token?.refreshToken) {
    const res = NextResponse.json(
      {
        type: 'https://httpstatuses.io/401',
        title: 'Unauthorized',
        status: 401,
        detail: 'No valid session refresh token available',
      },
      { status: 401 },
    );
    clearSessionCookies(res);
    return res;
  }

  try {
    const rotated = await rotateBackendRefreshToken(sessionJwt.token.refreshToken);

    const updatedToken: JWT = {
      ...sessionJwt.token,
      backendToken: rotated.accessToken,
      refreshToken: rotated.refreshToken,
      backendTokenExp: rotated.exp,
      systemRole: rotated.systemRole ?? sessionJwt.token.systemRole ?? null,
      projectId: rotated.projectId ?? sessionJwt.token.projectId ?? null,
      role: rotated.role ?? sessionJwt.token.role ?? null,
      error: null,
    };

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
    const res = NextResponse.json(
      {
        type: 'https://httpstatuses.io/401',
        title: 'Session Expired',
        status: 401,
        detail: 'Refresh token is expired or revoked',
      },
      { status: 401 },
    );
    clearSessionCookies(res);
    return res;
  }
}

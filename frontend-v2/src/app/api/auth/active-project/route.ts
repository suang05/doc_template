import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import {
  ACTIVE_PROJECT_COOKIE,
  attachSessionCookie,
  BACKEND_INTERNAL_URL,
  readSessionJwt,
} from '@/lib/auth/refreshCoordinator';

const activeProjectPayloadSchema = z.object({
  projectId: z.string().uuid('projectId ต้องเป็น UUID ที่ถูกต้อง'),
});

interface PortalProjectsEnvelope {
  data?: Array<{
    id: string;
    name: string;
    slug: string;
    role: string;
  }> | null;
}

export async function POST(req: NextRequest) {
  const sessionJwt = await readSessionJwt(req);
  if (!sessionJwt?.token?.backendToken) {
    return NextResponse.json(
      {
        type: 'https://httpstatuses.io/401',
        title: 'Unauthorized',
        status: 401,
        detail: 'Authentication required',
      },
      { status: 401 },
    );
  }

  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return NextResponse.json(
      {
        type: 'https://httpstatuses.io/400',
        title: 'Invalid JSON',
        status: 400,
        detail: 'Invalid request payload',
      },
      { status: 400 },
    );
  }

  const parsed = activeProjectPayloadSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json(
      {
        type: 'https://httpstatuses.io/400',
        title: 'Validation Failed',
        status: 400,
        detail: parsed.error.issues[0]?.message ?? 'Invalid projectId',
      },
      { status: 400 },
    );
  }

  const targetProjectId = parsed.data.projectId;
  const isSuperAdmin = sessionJwt.token.systemRole === 'SuperAdmin';
  let resolvedRole = sessionJwt.token.role ?? null;

  try {
    const portalRes = await fetch(`${BACKEND_INTERNAL_URL}/api/v1/portal/projects`, {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${sessionJwt.token.backendToken}`,
      },
      cache: 'no-store',
    });

    if (portalRes.ok) {
      const json = (await portalRes.json()) as PortalProjectsEnvelope;
      const match = json.data?.find(
        (p) => p.id.toLowerCase() === targetProjectId.toLowerCase(),
      );
      if (!match && !isSuperAdmin) {
        return NextResponse.json(
          {
            type: 'https://httpstatuses.io/403',
            title: 'Forbidden',
            status: 403,
            detail: 'คุณไม่มีสิทธิ์เข้าถึงโปรเจกต์ที่เลือก',
          },
          { status: 403 },
        );
      }
      if (match?.role) {
        resolvedRole = match.role;
      }
    } else if (!isSuperAdmin) {
      return NextResponse.json(
        {
          type: 'https://httpstatuses.io/403',
          title: 'Forbidden',
          status: 403,
          detail: 'ไม่สามารถยืนยันสิทธิ์เข้าถึงโปรเจกต์ได้',
        },
        { status: 403 },
      );
    }
  } catch {
    if (!isSuperAdmin) {
      return NextResponse.json(
        {
          type: 'https://httpstatuses.io/502',
          title: 'Bad Gateway',
          status: 502,
          detail: 'ไม่สามารถเชื่อมต่อระบบเพื่อตรวจสิทธิ์โปรเจกต์',
        },
        { status: 502 },
      );
    }
  }

  const isSecureRequest =
    req.nextUrl.protocol === 'https:' ||
    req.headers.get('x-forwarded-proto') === 'https';

  const res = NextResponse.json({
    ok: true,
    projectId: targetProjectId,
    role: resolvedRole,
  });

  res.cookies.set(ACTIVE_PROJECT_COOKIE, targetProjectId, {
    httpOnly: true,
    sameSite: 'lax',
    path: '/',
    secure: isSecureRequest,
    maxAge: 30 * 24 * 60 * 60,
  });

  await attachSessionCookie(res, sessionJwt.cookieName, {
    ...sessionJwt.token,
    projectId: targetProjectId,
    role: resolvedRole,
  });

  return res;
}

export async function DELETE() {
  const res = NextResponse.json({ ok: true });
  res.cookies.delete(ACTIVE_PROJECT_COOKIE);
  return res;
}

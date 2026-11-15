import { NextRequest, NextResponse } from 'next/server';
import { z } from 'zod';
import {
  ACTIVE_PROJECT_COOKIE,
  attachSessionCookie,
  BACKEND_INTERNAL_URL,
  bffProblemJson,
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
    return bffProblemJson(401, 'Unauthorized', 'Authentication required');
  }

  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return bffProblemJson(400, 'Invalid JSON', 'Invalid request payload');
  }

  const parsed = activeProjectPayloadSchema.safeParse(body);
  if (!parsed.success) {
    return bffProblemJson(400, 'Validation Failed', parsed.error.issues[0]?.message ?? 'Invalid projectId');
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
        return bffProblemJson(403, 'Forbidden', 'คุณไม่มีสิทธิ์เข้าถึงโปรเจกต์ที่เลือก');
      }
      if (match?.role) {
        resolvedRole = match.role;
      }
    } else if (!isSuperAdmin) {
      return bffProblemJson(403, 'Forbidden', 'ไม่สามารถยืนยันสิทธิ์เข้าถึงโปรเจกต์ได้');
    }
  } catch {
    if (!isSuperAdmin) {
      return bffProblemJson(502, 'Bad Gateway', 'ไม่สามารถเชื่อมต่อระบบเพื่อตรวจสิทธิ์โปรเจกต์');
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

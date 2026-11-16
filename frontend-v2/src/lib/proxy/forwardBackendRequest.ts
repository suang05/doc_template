import { NextRequest, NextResponse } from 'next/server';
import type { JWT } from 'next-auth/jwt';
import { auth } from '@/auth';
import {
  ACTIVE_PROJECT_COOKIE,
  applyRotatedTokens,
  attachSessionCookie,
  BACKEND_INTERNAL_URL,
  bffProblemJson,
  readSessionJwt,
  rotateBackendRefreshToken,
} from '@/lib/auth/refreshCoordinator';

const MAX_REPLAYABLE_MULTIPART_BYTES = 2 * 1024 * 1024;

const HOP_BY_HOP_REQUEST_HEADERS = new Set([
  'host',
  'connection',
  'keep-alive',
  'transfer-encoding',
  'te',
  'trailer',
  'upgrade',
  'proxy-authorization',
  'proxy-connection',
  'content-length',
]);

const HOP_BY_HOP_RESPONSE_HEADERS = new Set([
  'connection',
  'keep-alive',
  'transfer-encoding',
  'te',
  'trailer',
  'upgrade',
  'content-encoding',
]);

interface PreparedBody {
  buffer?: ArrayBuffer;
  stream?: ReadableStream<Uint8Array>;
  isReplayable: boolean;
}

async function prepareRequestBody(req: NextRequest): Promise<PreparedBody> {
  if (req.method === 'GET' || req.method === 'HEAD') {
    return { isReplayable: true };
  }

  const contentType = (req.headers.get('content-type') || '').toLowerCase();
  const isMultipart = contentType.includes('multipart/form-data');

  if (isMultipart) {
    const lengthHeader = req.headers.get('content-length');
    const parsedLength = lengthHeader ? Number.parseInt(lengthHeader, 10) : NaN;
    if (
      Number.isFinite(parsedLength) &&
      parsedLength > 0 &&
      parsedLength <= MAX_REPLAYABLE_MULTIPART_BYTES
    ) {
      const ab = await req.arrayBuffer();
      return {
        buffer: ab.byteLength > 0 ? ab : undefined,
        isReplayable: true,
      };
    }

    return {
      stream: req.body ?? undefined,
      isReplayable: false,
    };
  }

  const ab = await req.arrayBuffer();
  return {
    buffer: ab.byteLength > 0 ? ab : undefined,
    isReplayable: true,
  };
}

function buildUpstreamHeaders(
  req: NextRequest,
  accessToken: string | null | undefined,
  defaultProjectId: string | null | undefined,
  normalizedPath: string,
): Headers {
  const headers = new Headers();
  req.headers.forEach((value, key) => {
    if (!HOP_BY_HOP_REQUEST_HEADERS.has(key.toLowerCase())) {
      headers.set(key, value);
    }
  });

  headers.delete('x-api-key');

  const isM2mTest = req.headers.get('x-m2m-test') === 'true';
  const incomingAuth = req.headers.get('authorization');
  const hasApiKeyAuth = Boolean(incomingAuth && /^ApiKey\s+\S+/i.test(incomingAuth));

  if (isM2mTest && hasApiKeyAuth && incomingAuth) {
    headers.set('authorization', incomingAuth);
  } else if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`);
  } else {
    headers.delete('authorization');
  }

  const isManagementRoute =
    normalizedPath.startsWith('v1/management/') || normalizedPath === 'v1/management';
  const activeProjectCookie = req.cookies.get(ACTIVE_PROJECT_COOKIE)?.value;

  if (!headers.has('Project-Id') && !isManagementRoute) {
    const resolvedProjectId = activeProjectCookie || defaultProjectId;
    if (resolvedProjectId) {
      headers.set('Project-Id', resolvedProjectId);
    }
  }

  return headers;
}

function copyUpstreamResponseHeaders(upstreamHeaders: Headers): Headers {
  const out = new Headers();
  upstreamHeaders.forEach((value, key) => {
    const lower = key.toLowerCase();
    if (!HOP_BY_HOP_RESPONSE_HEADERS.has(lower)) {
      out.set(key, value);
    }
  });
  return out;
}

function dispatchBackendRequest(
  targetUrl: string,
  method: string,
  headers: Headers,
  preparedBody: PreparedBody,
): Promise<Response> {
  if (method === 'GET' || method === 'HEAD') {
    return fetch(targetUrl, {
      method,
      headers,
      redirect: 'manual',
    });
  }

  if (preparedBody.stream) {
    return fetch(targetUrl, {
      method,
      headers,
      body: preparedBody.stream,
      // @ts-expect-error Node fetch supports duplex for streamed uploads
      duplex: 'half',
      redirect: 'manual',
    });
  }

  const bodyBytes = preparedBody.buffer
    ? preparedBody.buffer.slice(0)
    : undefined;

  return fetch(targetUrl, {
    method,
    headers,
    body: bodyBytes,
    redirect: 'manual',
  });
}

export async function handleProxyRequest(
  req: NextRequest,
  pathSegments: string[],
): Promise<NextResponse> {
  const normalizedPath = pathSegments.join('/');
  const targetUrl = `${BACKEND_INTERNAL_URL}/api/${normalizedPath}${req.nextUrl.search}`;

  const [session, sessionJwt, preparedBody] = await Promise.all([
    auth(),
    readSessionJwt(req),
    prepareRequestBody(req),
  ]);

  const initialAccessToken = sessionJwt?.token?.backendToken ?? session?.backendToken ?? null;
  const fallbackProjectId = sessionJwt?.token?.projectId ?? session?.user?.projectId ?? null;

  const initialHeaders = buildUpstreamHeaders(
    req,
    initialAccessToken,
    fallbackProjectId,
    normalizedPath,
  );

  let backendResponse: Response;
  try {
    backendResponse = await dispatchBackendRequest(
      targetUrl,
      req.method,
      initialHeaders,
      preparedBody,
    );
  } catch {
    return bffProblemJson(502, 'Backend Service Unavailable', 'Cannot connect to backend service');
  }

  let rotatedTokenCookiePayload: { cookieName: string; token: JWT } | null = null;

  if (
    backendResponse.status === 401 &&
    preparedBody.isReplayable &&
    sessionJwt?.token?.refreshToken
  ) {
    try {
      const rotated = await rotateBackendRefreshToken(sessionJwt.token.refreshToken);
      const updatedToken = applyRotatedTokens(sessionJwt.token, rotated);
      rotatedTokenCookiePayload = {
        cookieName: sessionJwt.cookieName,
        token: updatedToken,
      };

      const retryHeaders = buildUpstreamHeaders(
        req,
        rotated.accessToken,
        updatedToken.projectId,
        normalizedPath,
      );

      await backendResponse.body?.cancel();
      backendResponse = await dispatchBackendRequest(
        targetUrl,
        req.method,
        retryHeaders,
        preparedBody,
      );
    } catch {
      // Refresh failed; return the original 401 response downstream.
    }
  }

  const responseHeaders = copyUpstreamResponseHeaders(backendResponse.headers);
  const nextRes = new NextResponse(backendResponse.body, {
    status: backendResponse.status,
    statusText: backendResponse.statusText,
    headers: responseHeaders,
  });

  if (rotatedTokenCookiePayload) {
    await attachSessionCookie(
      nextRes,
      rotatedTokenCookiePayload.cookieName,
      rotatedTokenCookiePayload.token,
    );
  }

  return nextRes;
}

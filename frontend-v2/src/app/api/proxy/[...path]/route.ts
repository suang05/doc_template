import { NextRequest } from 'next/server';
import { auth } from '@/auth';

// Server-only — never exposed to the browser. Falls back to the public URL for local dev
// where the Next.js server and the backend both resolve `localhost` the same way.
const BACKEND_INTERNAL_URL =
  process.env.API_INTERNAL_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

const HOP_BY_HOP_HEADERS = ['host', 'connection', 'content-length', 'transfer-encoding'];

async function proxy(req: NextRequest, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  const session = await auth();

  const targetUrl = `${BACKEND_INTERNAL_URL}/api/${path.join('/')}${req.nextUrl.search}`;

  const headers = new Headers(req.headers);
  for (const name of HOP_BY_HOP_HEADERS) headers.delete(name);

  if (session?.backendToken) {
    headers.set('Authorization', `Bearer ${session.backendToken}`);
  } else {
    headers.delete('authorization');
  }

  const hasBody = req.method !== 'GET' && req.method !== 'HEAD';

  const backendRes = await fetch(targetUrl, {
    method: req.method,
    headers,
    body: hasBody ? req.body : undefined,
    // Required by undici when streaming a request body via fetch.
    duplex: hasBody ? 'half' : undefined,
  } as RequestInit);

  const resHeaders = new Headers(backendRes.headers);
  resHeaders.delete('content-encoding');
  resHeaders.delete('content-length');

  return new Response(backendRes.body, {
    status: backendRes.status,
    headers: resHeaders,
  });
}

export {
  proxy as GET,
  proxy as POST,
  proxy as PUT,
  proxy as PATCH,
  proxy as DELETE,
};

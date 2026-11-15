import { NextRequest } from 'next/server';
import { handleProxyRequest } from '@/lib/proxy/forwardBackendRequest';

async function proxy(
  req: NextRequest,
  { params }: { params: Promise<{ path: string[] }> },
) {
  const { path } = await params;
  return handleProxyRequest(req, path);
}

export {
  proxy as GET,
  proxy as POST,
  proxy as PUT,
  proxy as PATCH,
  proxy as DELETE,
  proxy as HEAD,
};

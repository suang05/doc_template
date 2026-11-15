import { NextResponse } from 'next/server';
import { BACKEND_INTERNAL_URL } from '@/lib/auth/refreshCoordinator';

export async function GET() {
  try {
    const res = await fetch(`${BACKEND_INTERNAL_URL}/health`, {
      method: 'GET',
      cache: 'no-store',
    });

    const contentType = res.headers.get('content-type') || 'application/json';
    const body = await res.text();

    return new NextResponse(body, {
      status: res.status,
      headers: {
        'Content-Type': contentType,
        'Cache-Control': 'no-store',
      },
    });
  } catch {
    return NextResponse.json(
      {
        status: 'unhealthy',
        checks: [],
        error: 'Cannot connect to backend service',
      },
      { status: 503 },
    );
  }
}

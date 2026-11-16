'use client';

import { RouteErrorFallback } from '@/components/ui/RouteBoundaryStates';

export default function AppSegmentError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return <RouteErrorFallback error={error} reset={reset} />;
}

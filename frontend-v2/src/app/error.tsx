'use client';

import { RouteErrorFallback } from '@/components/ui/RouteBoundaryStates';

export default function RootError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="min-h-screen bg-canvas flex items-center justify-center p-6">
      <RouteErrorFallback error={error} reset={reset} />
    </div>
  );
}

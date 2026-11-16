'use client';

import { RouteErrorFallback } from '@/components/ui/RouteBoundaryStates';

export default function StudioError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <RouteErrorFallback
      title="ไม่สามารถโหลด Template Studio ได้"
      error={error}
      reset={reset}
      homeHref="/templates"
      homeLabel="กลับหน้ารายการแม่แบบ"
    />
  );
}

'use client';

import './globals.css';
import { RouteErrorFallback } from '@/components/ui/RouteBoundaryStates';

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html lang="th">
      <body className="min-h-screen bg-canvas text-textPrimary antialiased flex items-center justify-center p-6">
        <RouteErrorFallback
          title="ระบบเกิดข้อผิดพลาดร้ายแรง"
          error={error}
          reset={reset}
          homeHref="/templates"
          homeLabel="กลับหน้าหลัก"
        />
      </body>
    </html>
  );
}

'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import { RequireRole } from '@/components/ui/RequireRole';
import { UploadTemplateView } from '@/features/templates';

export function UploadPageClient() {
  const router = useRouter();

  return (
    <RequireRole role="Developer">
      <UploadTemplateView
        onSuccess={(templateId) => router.push(`/mapping?templateId=${templateId}`)}
        onCancel={() => router.push('/templates')}
      />
    </RequireRole>
  );
}

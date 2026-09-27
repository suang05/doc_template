'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import { RequireRole } from '@/components/ui/RequireRole';
import { TemplatesView } from '@/components/features/templates/TemplatesView';

export default function TemplatesPage() {
  const router = useRouter();

  return (
    <RequireRole role="Editor">
      <TemplatesView
        onOpenStudio={(templateId) => router.push(`/studio/${templateId}`)}
        onOpenMapping={(templateId) => router.push(`/mapping?templateId=${templateId}`)}
        onOpenGenerator={(slug) => router.push(`/generator?slug=${encodeURIComponent(slug)}`)}
        onNavigateUpload={() => router.push('/upload')}
      />
    </RequireRole>
  );
}

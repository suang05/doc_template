'use client';

import React from 'react';
import { useParams, useRouter } from 'next/navigation';
import { RequireRole } from '@/components/ui/RequireRole';
import { TemplateStudioView } from '@/components/features/studio/TemplateStudioView';

export default function StudioPage() {
  const router = useRouter();
  const params = useParams<{ templateId: string }>();

  return (
    <RequireRole role="Editor">
      <TemplateStudioView
        templateId={params.templateId ?? ''}
        onBack={() => router.push('/templates')}
      />
    </RequireRole>
  );
}

'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import { RequireRole } from '@/components/ui/RequireRole';
import { TemplateStudioView } from '@/components/features/studio/TemplateStudioView';

export interface StudioPageClientProps {
  templateId: string;
}

export function StudioPageClient({ templateId }: StudioPageClientProps) {
  const router = useRouter();

  return (
    <RequireRole role="Editor">
      <TemplateStudioView
        templateId={templateId}
        onBack={() => router.push('/templates')}
      />
    </RequireRole>
  );
}


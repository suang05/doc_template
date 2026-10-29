'use client';

import React from 'react';
import { RequireRole } from '@/components/ui/RequireRole';
import { FieldMappingView } from '@/features/mapping';

export interface MappingPageClientProps {
  initialTemplateId?: string;
}

export function MappingPageClient({ initialTemplateId }: MappingPageClientProps) {
  return (
    <RequireRole role="Editor">
      <FieldMappingView initialTemplateId={initialTemplateId} />
    </RequireRole>
  );
}


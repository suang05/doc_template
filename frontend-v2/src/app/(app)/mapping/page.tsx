'use client';

import React from 'react';
import { useSearchParams } from 'next/navigation';
import { RequireRole } from '@/components/ui/RequireRole';
import { FieldMappingView } from '@/components/features/mappings/FieldMappingView';

export default function MappingPage() {
  const searchParams = useSearchParams();
  const templateId = searchParams.get('templateId') ?? undefined;

  return (
    <RequireRole role="Editor">
      <FieldMappingView initialTemplateId={templateId} />
    </RequireRole>
  );
}

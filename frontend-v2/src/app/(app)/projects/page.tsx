import React from 'react';
import { RequireRole } from '@/components/ui/RequireRole';
import { ApiKeysView } from '@/features/apikeys';

export default function ProjectsPage() {
  return (
    <RequireRole role="Admin">
      <ApiKeysView />
    </RequireRole>
  );
}

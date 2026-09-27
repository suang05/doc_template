import React from 'react';
import { RequireRole } from '@/components/ui/RequireRole';
import { ApiKeysView } from '@/components/features/apikeys/ApiKeysView';

export default function ProjectsPage() {
  return (
    <RequireRole role="Admin">
      <ApiKeysView />
    </RequireRole>
  );
}

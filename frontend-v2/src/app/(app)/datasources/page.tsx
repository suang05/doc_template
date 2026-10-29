import React from 'react';
import { RequireRole } from '@/components/ui/RequireRole';
import { DatasourcesView } from '@/features/datasources';

export default function DatasourcesPage() {
  return (
    <RequireRole role="Admin">
      <DatasourcesView />
    </RequireRole>
  );
}

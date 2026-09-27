import React from 'react';
import { RequireRole } from '@/components/ui/RequireRole';
import { DatasourcesView } from '@/components/features/datasources/DatasourcesView';

export default function DatasourcesPage() {
  return (
    <RequireRole role="Admin">
      <DatasourcesView />
    </RequireRole>
  );
}

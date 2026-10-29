import React from 'react';
import { RequireRole } from '@/components/ui/RequireRole';
import { LogsView } from '@/features/logs';

export default function LogsPage() {
  return (
    <RequireRole role="Admin">
      <LogsView />
    </RequireRole>
  );
}

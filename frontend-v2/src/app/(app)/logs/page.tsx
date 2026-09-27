import React from 'react';
import { RequireRole } from '@/components/ui/RequireRole';
import { LogsView } from '@/components/features/logs/LogsView';

export default function LogsPage() {
  return (
    <RequireRole role="Admin">
      <LogsView />
    </RequireRole>
  );
}

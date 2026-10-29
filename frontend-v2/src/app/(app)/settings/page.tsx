import React from 'react';
import { RequireRole } from '@/components/ui/RequireRole';
import { SettingsView } from '@/features/settings';

export default function SettingsPage() {
  return (
    <RequireRole role="Admin">
      <SettingsView />
    </RequireRole>
  );
}

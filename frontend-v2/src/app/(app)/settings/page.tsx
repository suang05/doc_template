import React from 'react';
import { RequireRole } from '@/components/ui/RequireRole';
import { SettingsView } from '@/components/features/settings/SettingsView';

export default function SettingsPage() {
  return (
    <RequireRole role="Admin">
      <SettingsView />
    </RequireRole>
  );
}

import React from 'react';
import { RequireRole } from '@/components/ui/RequireRole';
import { UsersView } from '@/components/features/users/UsersView';

export default function UsersPage() {
  return (
    <RequireRole role="Admin">
      <UsersView />
    </RequireRole>
  );
}

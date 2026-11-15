import React from 'react';
import { RequireRole } from '@/components/ui/RequireRole';
import { ProjectsControlPlaneView } from '@/features/projects';

export default function ProjectsPage() {
  return (
    <RequireRole superAdminOnly>
      <ProjectsControlPlaneView />
    </RequireRole>
  );
}

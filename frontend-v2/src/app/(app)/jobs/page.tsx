import { RequireRole } from '@/components/ui/RequireRole';
import { JobsView } from '@/features/jobs';

export default function JobsPage() {
  return (
    <RequireRole role="Viewer">
      <JobsView />
    </RequireRole>
  );
}

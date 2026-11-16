import { RouteLoadingSkeleton } from '@/components/ui/RouteBoundaryStates';

export default function RootLoading() {
  return (
    <div className="min-h-screen bg-canvas p-6">
      <div className="max-w-7xl mx-auto w-full">
        <RouteLoadingSkeleton variant="page" />
      </div>
    </div>
  );
}

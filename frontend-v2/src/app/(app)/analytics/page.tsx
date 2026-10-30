import type { Metadata } from 'next';
import { AnalyticsView } from '@/features/analytics';

export const metadata: Metadata = {
  title: 'Analytics & Telemetry — SMK Document Platform',
  description: 'Document generation throughput, error rates, and system performance metrics',
};

export default function AnalyticsPage() {
  return <AnalyticsView />;
}

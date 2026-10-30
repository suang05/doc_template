import type { Metadata } from 'next';
import { VersionHistoryView } from '@/features/version-history';

export const metadata: Metadata = {
  title: 'Version History — SMK Document Platform',
  description: 'Visual diff comparison and revision rollback for enterprise document templates',
};

export default function VersionHistoryPage() {
  return <VersionHistoryView />;
}

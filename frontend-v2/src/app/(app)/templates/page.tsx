import type { Metadata } from 'next';
import { TemplatesPageClient } from './_components/TemplatesPageClient';

export const metadata: Metadata = {
  title: 'Templates — SMK Document Platform',
  description: 'Manage and design enterprise document templates',
};

export default function TemplatesPage() {
  return <TemplatesPageClient />;
}

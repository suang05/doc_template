import type { Metadata } from 'next';
import { StudioPageClient } from './_components/StudioPageClient';

interface StudioPageProps {
  params: Promise<{ templateId: string }>;
}

export const metadata: Metadata = {
  title: 'Template Studio — SMK Document Platform',
  description: 'Visual code editor and real-time live preview for document templates',
};

export default async function StudioPage({ params }: StudioPageProps) {
  const { templateId } = await params;

  return <StudioPageClient templateId={templateId ?? ''} />;
}

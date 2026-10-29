import type { Metadata } from 'next';
import { Suspense } from 'react';
import { MappingPageClient } from './_components/MappingPageClient';

interface MappingPageProps {
  searchParams: Promise<{ templateId?: string }>;
}

export const metadata: Metadata = {
  title: 'Field Mapping — SMK Document Platform',
  description: 'Map dataset fields to template variables with transformers',
};

export default async function MappingPage({ searchParams }: MappingPageProps) {
  const { templateId } = await searchParams;

  return (
    <Suspense fallback={<div className="p-8 text-center text-xs text-textMuted">Loading mapping canvas...</div>}>
      <MappingPageClient initialTemplateId={templateId} />
    </Suspense>
  );
}

import type { Metadata } from 'next';
import { Suspense } from 'react';
import { GeneratorPageClient } from './_components/GeneratorPageClient';

interface GeneratorPageProps {
  searchParams: Promise<{ slug?: string }>;
}

export const metadata: Metadata = {
  title: 'Document Generator — SMK Document Platform',
  description: 'Generate PDF, DOCX, and XLSX documents from data payloads',
};

export default async function GeneratorPage({ searchParams }: GeneratorPageProps) {
  const { slug } = await searchParams;

  return (
    <Suspense fallback={<div className="p-8 text-center text-xs text-textMuted">Loading generator...</div>}>
      <GeneratorPageClient initialSlug={slug ?? ''} />
    </Suspense>
  );
}

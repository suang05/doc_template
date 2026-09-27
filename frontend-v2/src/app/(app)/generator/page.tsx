'use client';

import React from 'react';
import { useSearchParams } from 'next/navigation';
import { GeneratorView } from '@/components/features/generator/GeneratorView';

export default function GeneratorPage() {
  const searchParams = useSearchParams();
  const slug = searchParams.get('slug') ?? '';

  return <GeneratorView initialSlug={slug} />;
}

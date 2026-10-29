'use client';

import React from 'react';
import { GeneratorView } from '@/components/features/generator/GeneratorView';

export interface GeneratorPageClientProps {
  initialSlug: string;
}

export function GeneratorPageClient({ initialSlug }: GeneratorPageClientProps) {
  return <GeneratorView initialSlug={initialSlug} />;
}


'use client';

import React from 'react';
import { GeneratorView } from '@/features/generator';

export interface GeneratorPageClientProps {
  initialSlug: string;
}

export function GeneratorPageClient({ initialSlug }: GeneratorPageClientProps) {
  return <GeneratorView initialSlug={initialSlug} />;
}


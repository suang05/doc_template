'use client';

import { useState, useMemo, useCallback } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { TemplateDto, DocumentCategory } from '@/types/api';
import { templatesApi } from '@/lib/api/templates.api';
import { FETCH_TIMEOUT_MS } from '@/constants/timeouts';

export const TEMPLATES_QUERY_KEY = ['templates'] as const;

export function useTemplates() {
  const queryClient = useQueryClient();
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<DocumentCategory | 'all'>('all');

  const {
    data: templates = [],
    isLoading: loading,
    error: rawError,
    refetch,
  } = useQuery<TemplateDto[], Error>({
    queryKey: TEMPLATES_QUERY_KEY,
    queryFn: async ({ signal }) => {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

      const abortHandler = () => controller.abort();
      signal?.addEventListener('abort', abortHandler);

      try {
        const data = await templatesApi.listTemplates({ signal: controller.signal });
        return data || [];
      } catch (err: unknown) {
        if (err instanceof Error && err.name === 'AbortError') {
          throw new Error('การเชื่อมต่อกับ Backend ใช้เวลานานเกินไป กรุณาลองใหม่อีกครั้ง');
        }
        throw err instanceof Error ? err : new Error('ไม่สามารถโหลดรายการแม่แบบได้');
      } finally {
        clearTimeout(timeoutId);
        signal?.removeEventListener('abort', abortHandler);
      }
    },
  });

  const error = rawError ? rawError.message : null;

  const filteredTemplates = useMemo(() => {
    return templates.filter((t) => {
      const matchesSearch =
        t.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        t.slug.toLowerCase().includes(searchQuery.toLowerCase());

      const matchesCategory =
        selectedCategory === 'all' ||
        (t.category && t.category.toLowerCase() === selectedCategory.toLowerCase());

      return matchesSearch && matchesCategory;
    });
  }, [templates, searchQuery, selectedCategory]);

  const refresh = useCallback(async () => {
    await refetch();
  }, [refetch]);

  return {
    templates,
    filteredTemplates,
    loading,
    error,
    searchQuery,
    setSearchQuery,
    selectedCategory,
    setSelectedCategory,
    refresh,
    invalidate: () => queryClient.invalidateQueries({ queryKey: TEMPLATES_QUERY_KEY }),
  };
}

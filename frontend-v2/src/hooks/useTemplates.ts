'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import { TemplateDto, DocumentCategory } from '@/types/api';
import { templatesApi } from '@/lib/api/templates.api';
import { FETCH_TIMEOUT_MS } from '@/constants/timeouts';

export function useTemplates() {
  const [templates, setTemplates] = useState<TemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<DocumentCategory | 'all'>('all');

  const fetchTemplates = useCallback(async () => {
    setLoading(true);
    setError(null);

    // Add timeout to prevent hanging forever if backend doesn't respond
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

    try {
      const data = await templatesApi.listTemplates({ signal: controller.signal });
      setTemplates(data || []);
    } catch (err: unknown) {
      setError(err instanceof Error && err.name === 'AbortError'
        ? 'การเชื่อมต่อกับ Backend ใช้เวลานานเกินไป กรุณาลองใหม่อีกครั้ง'
        : (err instanceof Error ? err.message : 'ไม่สามารถโหลดรายการแม่แบบได้'));
    } finally {
      clearTimeout(timeoutId);
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTemplates();
  }, [fetchTemplates]);

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

  return {
    templates,
    filteredTemplates,
    loading,
    error,
    searchQuery,
    setSearchQuery,
    selectedCategory,
    setSelectedCategory,
    refresh: fetchTemplates,
  };
}

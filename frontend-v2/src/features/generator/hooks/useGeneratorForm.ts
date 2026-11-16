'use client';

import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import type { FieldMappingDto, TemplateSchemaDto } from '@/lib/api/types';
import { templatesApi } from '@/features/templates';
import { withTimeoutSignal } from '@/lib/api/withTimeoutSignal';

function setNestedValue(obj: Record<string, unknown>, path: string, value: string): void {
  const parts = path.split('.');
  let cur = obj;
  for (let i = 0; i < parts.length - 1; i++) {
    if (typeof cur[parts[i]] !== 'object' || cur[parts[i]] === null) {
      cur[parts[i]] = {};
    }
    cur = cur[parts[i]] as Record<string, unknown>;
  }
  cur[parts[parts.length - 1]] = value;
}

const DEFAULT_FALLBACK_JSON =
  '{\n  "contractNo": "SMK-2026-001",\n  "customerName": "นายสมชาย ใจดี",\n  "amount": 2500000,\n  "effectiveDate": "2026-09-15"\n}';

interface GeneratorFormQueryData {
  mappings: FieldMappingDto[];
  templateSchema: TemplateSchemaDto | null;
}

export function useGeneratorForm(templateId: string | undefined) {
  const [formValues, setFormValues] = useState<Record<string, string>>({});
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [fallbackJson, setFallbackJson] = useState(DEFAULT_FALLBACK_JSON);
  const [jsonError, setJsonError] = useState<string | null>(null);
  const lastHydratedRef = useRef<{ templateId: string; dataUpdatedAt: number } | null>(null);

  const formConfigQuery = useQuery<GeneratorFormQueryData>({
    queryKey: ['generator-form', templateId] as const,
    enabled: Boolean(templateId),
    queryFn: ({ signal }) =>
      withTimeoutSignal(async (timedSignal) => {
        const [mappingsRes, schemaRes] = await Promise.allSettled([
          templatesApi.getTemplateMappings(templateId!, { signal: timedSignal }),
          templatesApi.getTemplateSchema(templateId!, { signal: timedSignal }),
        ]);

        const mappingsList =
          mappingsRes.status === 'fulfilled' && Array.isArray(mappingsRes.value)
            ? mappingsRes.value
            : [];
        const schema =
          schemaRes.status === 'fulfilled' && schemaRes.value?.samplePayload
            ? schemaRes.value
            : null;

        return { mappings: mappingsList, templateSchema: schema };
      }, signal),
  });

  useEffect(() => {
    if (!templateId) {
      lastHydratedRef.current = null;
      setFormValues({});
      setFieldErrors({});
      return;
    }

    const templateChanged = lastHydratedRef.current?.templateId !== templateId;
    if (templateChanged) {
      setFieldErrors({});
    }

    if (
      formConfigQuery.data &&
      (templateChanged || lastHydratedRef.current?.dataUpdatedAt !== formConfigQuery.dataUpdatedAt)
    ) {
      const initial: Record<string, string> = {};
      for (const m of formConfigQuery.data.mappings) {
        initial[m.sourcePath] = m.defaultValue ?? '';
      }
      setFormValues(initial);

      const samplePayload = formConfigQuery.data.templateSchema?.samplePayload;
      if (samplePayload) {
        try {
          const parsed: unknown = JSON.parse(samplePayload);
          setFallbackJson(JSON.stringify(parsed, null, 2));
        } catch {
          setFallbackJson(samplePayload);
        }
      }

      lastHydratedRef.current = {
        templateId,
        dataUpdatedAt: formConfigQuery.dataUpdatedAt,
      };
    }
  }, [templateId, formConfigQuery.data, formConfigQuery.dataUpdatedAt]);

  const mappings = useMemo(
    () => (templateId ? formConfigQuery.data?.mappings ?? [] : []),
    [templateId, formConfigQuery.data?.mappings],
  );
  const templateSchema = templateId ? formConfigQuery.data?.templateSchema ?? null : null;
  const mappingsLoading = Boolean(templateId) && formConfigQuery.isLoading;

  const hasMappings = Array.isArray(mappings) && mappings.length > 0;

  const setField = useCallback((sourcePath: string, value: string) => {
    setFormValues((prev) => ({ ...prev, [sourcePath]: value }));
    setFieldErrors((prev) => {
      if (!prev[sourcePath]) return prev;
      const next = { ...prev };
      delete next[sourcePath];
      return next;
    });
  }, []);

  const applyServerFieldErrors = useCallback(
    (rawFieldErrors: Record<string, string>): string | null => {
      const mapped: Record<string, string> = {};
      let firstKey: string | null = null;
      for (const [key, msg] of Object.entries(rawFieldErrors)) {
        const normalizedKey = key.startsWith('payload.') ? key.slice('payload.'.length) : key;
        const matched =
          mappings.find((m) => m.sourcePath === normalizedKey) ??
          mappings.find((m) => m.placeholder === normalizedKey);
        const targetKey = matched?.sourcePath ?? normalizedKey;
        mapped[targetKey] = msg;
        if (!firstKey) firstKey = targetKey;
      }
      setFieldErrors(mapped);
      return firstKey;
    },
    [mappings],
  );

  const assembledData = useMemo((): Record<string, unknown> => {
    if (!hasMappings) {
      try {
        return JSON.parse(fallbackJson) as Record<string, unknown>;
      } catch {
        return {};
      }
    }
    const result: Record<string, unknown> = {};
    for (const [path, value] of Object.entries(formValues)) {
      setNestedValue(result, path, value);
    }
    return result;
  }, [hasMappings, formValues, fallbackJson]);

  const validateForm = useCallback((): { valid: boolean; firstErrorField: string | null } => {
    if (!hasMappings) {
      try {
        JSON.parse(fallbackJson);
        setJsonError(null);
        return { valid: true, firstErrorField: null };
      } catch {
        setJsonError('รูปแบบ JSON ไม่ถูกต้อง กรุณาตรวจสอบวงเล็บหรือเครื่องหมายจุลภาค');
        return { valid: false, firstErrorField: 'fallbackJson' };
      }
    }
    const errors: Record<string, string> = {};
    let firstErrorField: string | null = null;
    const safeMappings = Array.isArray(mappings) ? mappings : [];
    for (const m of safeMappings) {
      if (m.required && !formValues[m.sourcePath]?.trim()) {
        errors[m.sourcePath] = 'กรุณากรอกข้อมูลนี้';
        if (!firstErrorField) firstErrorField = m.sourcePath;
      }
    }
    setFieldErrors(errors);
    return { valid: Object.keys(errors).length === 0, firstErrorField };
  }, [hasMappings, mappings, formValues, fallbackJson]);

  const handleFallbackChange = useCallback((json: string) => {
    setFallbackJson(json);
    try {
      JSON.parse(json);
      setJsonError(null);
    } catch {
      setJsonError('รูปแบบ JSON ไม่ถูกต้อง');
    }
  }, []);

  const fillSampleData = useCallback(() => {
    let parsedSample: Record<string, unknown> | null = null;
    if (templateSchema?.samplePayload) {
      try {
        const parsed: unknown = JSON.parse(templateSchema.samplePayload);
        if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
          parsedSample = parsed as Record<string, unknown>;
          setFallbackJson(JSON.stringify(parsedSample, null, 2));
          setJsonError(null);
          if (!hasMappings) return;
        }
      } catch {
        parsedSample = null;
      }
    }

    if (!hasMappings) {
      setFallbackJson(
        '{\n  "contractNo": "SMK-2026-999",\n  "customerName": "บริษัท ทดสอบจำกัด (มหาชน)",\n  "amount": 999000,\n  "effectiveDate": "2026-12-31"\n}',
      );
      setJsonError(null);
      return;
    }

    const sampleData: Record<string, string> = { ...formValues };
    const safeMappings = Array.isArray(mappings) ? mappings : [];
    for (const m of safeMappings) {
      if (sampleData[m.sourcePath]) continue;

      if (parsedSample !== null) {
        const val = parsedSample[m.placeholder] ?? parsedSample[m.sourcePath];
        if (val !== undefined && typeof val !== 'object') {
          sampleData[m.sourcePath] = String(val);
          continue;
        }
      }

      if (m.defaultValue) {
        sampleData[m.sourcePath] = m.defaultValue;
        continue;
      }

      const pathLower = m.sourcePath.toLowerCase();
      if (
        m.transform === 'thai_baht_text' ||
        pathLower.includes('amount') ||
        pathLower.includes('price') ||
        pathLower.includes('total') ||
        pathLower.includes('ยอด')
      ) {
        sampleData[m.sourcePath] = '1500000.00';
      } else if (
        m.transform === 'thai_date' ||
        m.transform === 'thai_date_short' ||
        pathLower.includes('date') ||
        pathLower.includes('วันที่')
      ) {
        sampleData[m.sourcePath] = new Date().toISOString().split('T')[0];
      } else if (pathLower.includes('name') || pathLower.includes('ชื่อ')) {
        sampleData[m.sourcePath] = 'บริษัท สมชายและบุตร จำกัด';
      } else if (pathLower.includes('address') || pathLower.includes('ที่อยู่')) {
        sampleData[m.sourcePath] = '123/45 ถนนพัฒนาการ แขวงสวนหลวง เขตสวนหลวง กรุงเทพฯ 10250';
      } else if (pathLower.includes('email') || pathLower.includes('อีเมล')) {
        sampleData[m.sourcePath] = 'test@example.com';
      } else {
        sampleData[m.sourcePath] = `ตัวอย่าง ${m.label}`;
      }
    }
    setFormValues(sampleData);
    setFieldErrors({});
  }, [templateSchema, mappings, hasMappings, formValues]);

  return {
    mappings,
    mappingsLoading,
    hasMappings,
    formValues,
    setField,
    fieldErrors,
    applyServerFieldErrors,
    fallbackJson,
    handleFallbackChange,
    jsonError,
    assembledData,
    validateForm,
    fillSampleData,
    templateSchema,
  };
}

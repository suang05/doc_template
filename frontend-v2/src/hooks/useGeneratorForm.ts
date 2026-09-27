'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import { FieldMappingDto, TemplateSchemaDto } from '@/types/api';
import { templatesApi } from '@/lib/api/templates.api';
import { FETCH_TIMEOUT_MS } from '@/constants/timeouts';

// Build nested object from dot-notation paths: "customer.name" → { customer: { name: v } }
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

export function useGeneratorForm(templateId: string | undefined) {
  const [mappings, setMappings] = useState<FieldMappingDto[]>([]);
  const [mappingsLoading, setMappingsLoading] = useState(false);
  const [formValues, setFormValues] = useState<Record<string, string>>({});
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [fallbackJson, setFallbackJson] = useState(DEFAULT_FALLBACK_JSON);
  const [jsonError, setJsonError] = useState<string | null>(null);
  const [templateSchema, setTemplateSchema] = useState<TemplateSchemaDto | null>(null);

  useEffect(() => {
    if (!templateId) {
      setMappings([]);
      setFormValues({});
      setFieldErrors({});
      setTemplateSchema(null);
      return;
    }
    setMappingsLoading(true);
    setFieldErrors({});

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

    Promise.allSettled([
      templatesApi.getTemplateMappings(templateId),
      templatesApi.getTemplateSchema(templateId),
    ]).then(([mappingsRes, schemaRes]) => {
      if (controller.signal.aborted) return;
      if (mappingsRes.status === 'fulfilled') {
        setMappings(mappingsRes.value);
        const initial: Record<string, string> = {};
        for (const m of mappingsRes.value) {
          initial[m.sourcePath] = m.defaultValue ?? '';
        }
        setFormValues(initial);
      }
      if (schemaRes.status === 'fulfilled' && schemaRes.value.samplePayload) {
        setTemplateSchema(schemaRes.value);
        try {
          const parsed = JSON.parse(schemaRes.value.samplePayload);
          setFallbackJson(JSON.stringify(parsed, null, 2));
        } catch {
          setFallbackJson(schemaRes.value.samplePayload);
        }
      }
      setMappingsLoading(false);
    }).finally(() => clearTimeout(timeoutId));

    return () => controller.abort();
  }, [templateId]);

  const hasMappings = mappings.length > 0;

  const setField = useCallback((sourcePath: string, value: string) => {
    setFormValues((prev) => ({ ...prev, [sourcePath]: value }));
    // Clear error on change
    setFieldErrors((prev) => {
      if (!prev[sourcePath]) return prev;
      const next = { ...prev };
      delete next[sourcePath];
      return next;
    });
  }, []);

  // Stable assembled JSON — recomputed when form values or mappings change
  const assembledData = useMemo((): Record<string, unknown> => {
    if (!hasMappings) {
      try {
        return JSON.parse(fallbackJson);
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

  // Returns true if valid, false + sets field errors if not
  const validateForm = useCallback((): boolean => {
    if (!hasMappings) {
      try {
        JSON.parse(fallbackJson);
        setJsonError(null);
        return true;
      } catch {
        setJsonError('รูปแบบ JSON ไม่ถูกต้อง กรุณาตรวจสอบวงเล็บหรือเครื่องหมายจุลภาค');
        return false;
      }
    }
    const errors: Record<string, string> = {};
    for (const m of mappings) {
      if (m.required && !formValues[m.sourcePath]?.trim()) {
        errors[m.sourcePath] = 'กรุณากรอกข้อมูลนี้';
      }
    }
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
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
    // Parse samplePayload once; use it to prime fallbackJson and as lookup during field fill
    let parsedSample: Record<string, unknown> | null = null;
    if (templateSchema?.samplePayload) {
      try {
        parsedSample = JSON.parse(templateSchema.samplePayload);
        setFallbackJson(JSON.stringify(parsedSample, null, 2));
        setJsonError(null);
        if (!hasMappings) return;
      } catch {
        parsedSample = null;
      }
    }

    if (!hasMappings) {
      setFallbackJson(
        '{\n  "contractNo": "SMK-2026-999",\n  "customerName": "บริษัท ทดสอบจำกัด (มหาชน)",\n  "amount": 999000,\n  "effectiveDate": "2026-12-31"\n}'
      );
      setJsonError(null);
      return;
    }

    const sampleData: Record<string, string> = { ...formValues };
    for (const m of mappings) {
      if (sampleData[m.sourcePath]) continue;

      // samplePayload is keyed by placeholder; try that first, then sourcePath for compat
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
      if (m.transform === 'ThaiBahtText' || pathLower.includes('amount') || pathLower.includes('price') || pathLower.includes('total') || pathLower.includes('ยอด')) {
        sampleData[m.sourcePath] = '1500000.00';
      } else if (m.transform === 'ThaiDateLong' || m.transform === 'ThaiDateShort' || pathLower.includes('date') || pathLower.includes('วันที่')) {
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
    fallbackJson,
    handleFallbackChange,
    jsonError,
    assembledData,
    validateForm,
    fillSampleData,
    templateSchema,
  };
}


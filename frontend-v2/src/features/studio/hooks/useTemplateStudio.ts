import { useState, useCallback, useEffect, useRef } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { templatesApi } from '@/features/templates/api/templates.api';
import { TEMPLATES_QUERY_KEY } from '@/features/templates/hooks/useTemplates';
import { extractErrorMessage } from '@/lib/api/client';
import { withTimeoutSignal } from '@/lib/api/withTimeoutSignal';
import type {
  TemplateDto,
  TemplateValidationResultDto,
  TemplateVersionDto,
} from '@/lib/api/types';
import { useConflictRecovery } from '@/hooks/useConflictRecovery';

export const DEFAULT_STARTER_HTML = `<!DOCTYPE html>
<html lang="th">
<head>
  <meta charset="UTF-8" />
  <title>เอกสารมาตรฐาน</title>
  <style>
    @page {
      size: A4;
      margin-top: 25mm;
      margin-bottom: 25mm;
      margin-left: 20mm;
      margin-right: 20mm;
    }
    body {
      font-family: 'Sarabun', -apple-system, sans-serif;
      font-size: 14pt;
      line-height: 1.6;
      color: #0f172a;
    }
    h1 {
      font-size: 18pt;
      text-align: center;
      margin: 20px 0;
    }
    .content {
      text-align: justify;
      margin-bottom: 40px;
    }
    .page-break {
      page-break-before: always;
    }
  </style>
</head>
<body>
  <h1>หนังสือสัญญาจะซื้อจะขาย</h1>
  <div class="content">
    <p>สัญญาฉบับนี้ทำขึ้นระหว่าง <strong>{{customerName}}</strong> (ผู้จะซื้อ) กับ <strong>บริษัท สัมมากร จำกัด (มหาชน)</strong> (ผู้จะขาย)</p>
    <p>โดยมีรายละเอียดมูลค่าสัญญาเป็นจำนวนเงินทั้งสิ้น <strong>{{amount:thai_baht_text}}</strong> ({{amount:thai_currency}} บาท)</p>
  </div>
  
  <div class="page-break"></div>
  
  <h1>เอกสารแนบท้าย (หน้าที่ 2)</h1>
  <div class="content">
    <p>รายละเอียดเพิ่มเติมของสัญญา...</p>
  </div>
</body>
</html>
<template id="header">
<!DOCTYPE html>
<html>
<head>
  <style>
    body { font-family: 'Sarabun', sans-serif; margin: 0; padding: 0; width: 100%; font-size: 12px; }
    .header-container { display: flex; justify-content: space-between; border-bottom: 2px solid #0284c7; padding-bottom: 10px; margin: 0 20mm; }
    .company { font-size: 16px; font-weight: bold; color: #0284c7; }
    .doc-meta { text-align: right; color: #475569; }
  </style>
</head>
<body>
  <div class="header-container">
    <div class="company">SAMMAKORN</div>
    <div class="doc-meta">เลขที่: {{contractNo}}</div>
  </div>
</body>
</html>
</template>
<template id="footer">
<!DOCTYPE html>
<html>
<head>
  <style>
    body { font-family: 'Sarabun', sans-serif; margin: 0; padding: 0; width: 100%; font-size: 12px; }
    .footer-container { display: flex; justify-content: space-between; margin: 0 20mm; margin-top: 10px; }
    .signature-box { text-align: center; width: 45%; }
    .signature-line { border-bottom: 1px dotted #334155; margin-bottom: 8px; }
  </style>
</head>
<body>
  <div class="footer-container">
    <div class="signature-box">
      <div class="signature-line"></div>
      <div>( ผู้จะซื้อ )</div>
    </div>
    <div class="signature-box">
      <div class="signature-line"></div>
      <div>( ผู้จะขาย )</div>
    </div>
  </div>
</body>
</html>
</template>`;

export const parseCombinedHtml = (fullHtml: string) => {
  let main = fullHtml;
  let header = '';
  let footer = '';

  const headerMatch = main.match(/<template\s+id=["']header["'][^>]*>([\s\S]*?)<\/template>/i);
  if (headerMatch) {
    header = headerMatch[1].trim();
    main = main.replace(headerMatch[0], '');
  }

  const footerMatch = main.match(/<template\s+id=["']footer["'][^>]*>([\s\S]*?)<\/template>/i);
  if (footerMatch) {
    footer = footerMatch[1].trim();
    main = main.replace(footerMatch[0], '');
  }

  return { main: main.trim(), header, footer };
};

export const getCombinedHtml = (main: string, header: string, footer: string) => {
  let combined = main;
  if (header.trim()) {
    combined += `\n<template id="header">\n${header.trim()}\n</template>`;
  }
  if (footer.trim()) {
    combined += `\n<template id="footer">\n${footer.trim()}\n</template>`;
  }
  return combined;
};

const defaultParsed = parseCombinedHtml(DEFAULT_STARTER_HTML);
const DEFAULT_NORMALIZED = getCombinedHtml(defaultParsed.main, defaultParsed.header, defaultParsed.footer);

interface StudioBundle {
  template: TemplateDto;
  main: string;
  header: string;
  footer: string;
  normalizedHtml: string;
  etag: string | null;
  persistedSamplePayload: string | null;
  dataSchema: string | null;
  versions: TemplateVersionDto[];
}

async function loadStudioBundle(templateId: string, signal: AbortSignal): Promise<StudioBundle> {
  const templates = await templatesApi.listTemplates({ signal });
  const current = templates.find((t) => t.id === templateId);
  if (!current) throw new Error('ไม่พบเทมเพลตที่ต้องการแก้ไข');

  let content = '';
  let resolvedEtag: string | null = null;
  let resolvedSamplePayload: string | null = null;
  let resolvedDataSchema: string | null = null;

  try {
    const studioRes = await templatesApi.getTemplateStudioWithETag(templateId, { signal });
    const studioBundle = studioRes.data;
    resolvedEtag = studioRes.etag;
    content = studioBundle.html;
    if (studioBundle.samplePayload) {
      resolvedSamplePayload = studioBundle.samplePayload;
    }
    if (studioBundle.dataSchema) {
      resolvedDataSchema = studioBundle.dataSchema;
    }
  } catch (err: unknown) {
    if (signal.aborted || (err instanceof DOMException && err.name === 'AbortError')) {
      throw err;
    }
    try {
      content = await templatesApi.getTemplateHtml(templateId, { signal });
      const metaRes = await templatesApi
        .getTemplateWithETag(templateId, { signal })
        .catch(() => null);
      resolvedEtag = metaRes?.etag ?? null;
    } catch (innerErr: unknown) {
      if (signal.aborted || (innerErr instanceof DOMException && innerErr.name === 'AbortError')) {
        throw innerErr;
      }
      content = '';
    }
  }

  if (!content || !content.trim()) {
    content = DEFAULT_STARTER_HTML;
  }

  const { main, header, footer } = parseCombinedHtml(content);
  const normalizedHtml = getCombinedHtml(main, header, footer);
  const versions = await templatesApi.listTemplateVersions(templateId, { signal });

  return {
    template: current,
    main,
    header,
    footer,
    normalizedHtml,
    etag: resolvedEtag,
    persistedSamplePayload: resolvedSamplePayload,
    dataSchema: resolvedDataSchema,
    versions,
  };
}

export function useTemplateStudio(templateId: string | null) {
  const queryClient = useQueryClient();
  const [template, setTemplate] = useState<TemplateDto | null>(null);
  const [html, setHtml] = useState<string>(DEFAULT_NORMALIZED);
  const [initialHtml, setInitialHtml] = useState<string>(DEFAULT_NORMALIZED);

  // Split states for the editor
  const [mainHtml, setMainHtml] = useState<string>(defaultParsed.main);
  const [headerHtml, setHeaderHtml] = useState<string>(defaultParsed.header);
  const [footerHtml, setFooterHtml] = useState<string>(defaultParsed.footer);

  const [isDirty, setIsDirty] = useState(false);
  const [validating, setValidating] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [versions, setVersions] = useState<TemplateVersionDto[]>([]);
  const [validationResult, setValidationResult] = useState<TemplateValidationResultDto | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const { conflict, captureConflict, clearConflict } = useConflictRecovery();

  const [persistedSamplePayload, setPersistedSamplePayload] = useState<string | null>(null);
  const [dataSchema, setDataSchema] = useState<string | null>(null);

  const forceSyncRef = useRef(false);
  const lastSyncedRef = useRef<{ templateId: string; dataUpdatedAt: number } | null>(null);

  const studioQuery = useQuery<StudioBundle, Error>({
    queryKey: ['template-studio', templateId] as const,
    enabled: Boolean(templateId),
    refetchOnWindowFocus: false,
    queryFn: ({ signal }) =>
      withTimeoutSignal((timedSignal) => loadStudioBundle(templateId!, timedSignal), signal),
  });

  // Hydrate editable studio buffers when template selection changes or clean reload completes
  useEffect(() => {
    if (!templateId) {
      lastSyncedRef.current = null;
      forceSyncRef.current = false;
      return;
    }

    const bundle = studioQuery.data;
    if (!bundle) return;

    const templateChanged = lastSyncedRef.current?.templateId !== templateId;
    const updatedChanged = lastSyncedRef.current?.dataUpdatedAt !== studioQuery.dataUpdatedAt;
    const shouldHydrate =
      templateChanged || forceSyncRef.current || (!isDirty && updatedChanged);

    if (!shouldHydrate) return;

    forceSyncRef.current = false;
    lastSyncedRef.current = { templateId, dataUpdatedAt: studioQuery.dataUpdatedAt };

    setTemplate(bundle.template);
    setEtag(bundle.etag);
    clearConflict();
    if (bundle.persistedSamplePayload) {
      setPersistedSamplePayload(bundle.persistedSamplePayload);
    }
    if (bundle.dataSchema) {
      setDataSchema(bundle.dataSchema);
    }
    setMainHtml(bundle.main);
    setHeaderHtml(bundle.header);
    setFooterHtml(bundle.footer);
    setHtml(bundle.normalizedHtml);
    setInitialHtml(bundle.normalizedHtml);
    setIsDirty(false);
    setVersions(bundle.versions);
    setActionError(null);
  }, [templateId, studioQuery.data, studioQuery.dataUpdatedAt, isDirty, clearConflict]);

  const reloadStudio = useCallback(async () => {
    if (!templateId) return;
    forceSyncRef.current = true;
    setActionError(null);
    clearConflict();
    await studioQuery.refetch();
  }, [templateId, clearConflict, studioQuery]);

  // Sync individual editor parts back to the combined HTML
  useEffect(() => {
    // Only re-combine if they have been parsed once (i.e. not during initial load empty states)
    if (initialHtml) {
      const combined = getCombinedHtml(mainHtml, headerHtml, footerHtml);
      setHtml(combined);
      setIsDirty(combined !== initialHtml);
    }
  }, [mainHtml, headerHtml, footerHtml, initialHtml]);

  const updateMainHtml = (val: string) => setMainHtml(val);
  const updateHeaderHtml = (val: string) => setHeaderHtml(val);
  const updateFooterHtml = (val: string) => setFooterHtml(val);

  const saveMutation = useMutation({
    mutationFn: async ({
      targetId,
      nextHtml,
      changeNote,
      samplePayload,
      ifMatch,
    }: {
      targetId: string;
      nextHtml: string;
      changeNote?: string;
      samplePayload?: string;
      ifMatch: string | null;
    }) => {
      const res = await templatesApi.saveTemplateHtmlWithETag(
        targetId,
        {
          html: nextHtml,
          samplePayload,
          changeNote: changeNote || 'แก้ไขจาก Template Studio',
        },
        ifMatch,
      );
      let nextEtag = res.etag;
      if (!nextEtag) {
        const latest = await templatesApi.getTemplateWithETag(targetId).catch(() => null);
        nextEtag = latest?.etag ?? null;
      }
      const vList = await templatesApi.listTemplateVersions(targetId);
      return {
        version: res.data.version,
        etag: nextEtag,
        versions: vList,
        savedHtml: nextHtml,
        samplePayload,
      };
    },
    onSuccess: async (result, variables) => {
      if (result.etag) {
        setEtag(result.etag);
      }
      setInitialHtml(result.savedHtml);
      setIsDirty(false);
      if (result.samplePayload) {
        setPersistedSamplePayload(result.samplePayload);
      }
      setVersions(result.versions);
      queryClient.setQueryData<StudioBundle>(['template-studio', variables.targetId], (prev) =>
        prev
          ? {
              ...prev,
              normalizedHtml: result.savedHtml,
              etag: result.etag ?? prev.etag,
              persistedSamplePayload: result.samplePayload ?? prev.persistedSamplePayload,
              versions: result.versions,
            }
          : prev,
      );
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['template-versions', variables.targetId] }),
        queryClient.invalidateQueries({ queryKey: TEMPLATES_QUERY_KEY }),
      ]);
    },
  });

  const saveHtmlAction = useCallback(
    async (changeNote?: string, samplePayload?: string) => {
      if (!templateId) throw new Error('ไม่พบ Template ID — กรุณาเปิด Template จากรายการก่อนบันทึก');
      setActionError(null);
      clearConflict();
      try {
        const result = await saveMutation.mutateAsync({
          targetId: templateId,
          nextHtml: html,
          changeNote,
          samplePayload,
          ifMatch: etag,
        });
        return result.version;
      } catch (err: unknown) {
        if (!captureConflict(err)) {
          const msg = extractErrorMessage(err, 'บันทึก HTML ไม่สำเร็จ');
          setActionError(msg);
        }
        throw err;
      }
    },
    [templateId, html, etag, saveMutation, captureConflict, clearConflict],
  );

  const validate = useCallback(async () => {
    if (!templateId) return;
    setValidating(true);
    setActionError(null);
    try {
      const res = await templatesApi.validateTemplate(templateId, html);
      setValidationResult(res);
      return res;
    } catch (err: unknown) {
      setActionError(extractErrorMessage(err, 'ตรวจสอบโครงสร้างไม่สำเร็จ'));
    } finally {
      setValidating(false);
    }
  }, [templateId, html]);

  const queryErrorMessage = studioQuery.error
    ? extractErrorMessage(studioQuery.error, 'ไม่สามารถโหลดข้อมูลเทมเพลตได้')
    : null;
  const error = actionError ?? queryErrorMessage;
  const loading = Boolean(templateId) && (studioQuery.isLoading || studioQuery.isRefetching);
  const saving = saveMutation.isPending;

  return {
    template,
    html,
    mainHtml,
    headerHtml,
    footerHtml,
    updateMainHtml,
    updateHeaderHtml,
    updateFooterHtml,
    isDirty,
    loading,
    saving,
    validating,
    error,
    versions,
    validationResult,
    persistedSamplePayload,
    dataSchema,
    etag,
    conflict,
    clearConflict,
    reloadStudio,
    saveHtml: saveHtmlAction,
    validate,
    reload: reloadStudio,
  };
}

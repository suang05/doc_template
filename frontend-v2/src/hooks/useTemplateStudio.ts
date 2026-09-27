import { useState, useCallback, useEffect } from 'react';
import { templatesApi } from '@/lib/api/templates.api';
import { TemplateDto, TemplateVersionDto, TemplateValidationResult } from '@/types/api';
import { FETCH_TIMEOUT_MS, TOAST_AUTO_DISMISS_MS } from '@/constants/timeouts';

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

export function useTemplateStudio(templateId: string | null) {
  const [template, setTemplate] = useState<TemplateDto | null>(null);
  const [html, setHtml] = useState<string>(DEFAULT_NORMALIZED);
  const [initialHtml, setInitialHtml] = useState<string>(DEFAULT_NORMALIZED);

  // Split states for the editor
  const [mainHtml, setMainHtml] = useState<string>(defaultParsed.main);
  const [headerHtml, setHeaderHtml] = useState<string>(defaultParsed.header);
  const [footerHtml, setFooterHtml] = useState<string>(defaultParsed.footer);

  const [isDirty, setIsDirty] = useState(false);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [validating, setValidating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [versions, setVersions] = useState<TemplateVersionDto[]>([]);
  const [validationResult, setValidationResult] = useState<TemplateValidationResult | null>(null);

  const [persistedSamplePayload, setPersistedSamplePayload] = useState<string | null>(null);
  const [dataSchema, setDataSchema] = useState<string | null>(null);

  const loadTemplateData = useCallback(async (signal?: AbortSignal) => {
    if (!templateId) return;
    setLoading(true);
    setError(null);
    try {
      const templates = await templatesApi.listTemplates({ signal });
      const current = templates.find((t) => t.id === templateId);
      if (!current) throw new Error('ไม่พบเทมเพลตที่ต้องการแก้ไข');
      setTemplate(current);

      let content = '';
      try {
        const studioBundle = await templatesApi.getTemplateStudio(templateId);
        content = studioBundle.html;
        if (studioBundle.samplePayload) {
          setPersistedSamplePayload(studioBundle.samplePayload);
        }
        if (studioBundle.dataSchema) {
          setDataSchema(studioBundle.dataSchema);
        }
      } catch {
        try {
          content = await templatesApi.getTemplateHtml(templateId);
        } catch {
          content = '';
        }
      }

      if (!content || !content.trim()) {
        content = DEFAULT_STARTER_HTML;
      }

      const { main, header, footer } = parseCombinedHtml(content);
      const normalizedHtml = getCombinedHtml(main, header, footer);

      setMainHtml(main);
      setHeaderHtml(header);
      setFooterHtml(footer);
      setHtml(normalizedHtml);
      setInitialHtml(normalizedHtml);
      setIsDirty(false);

      const vList = await templatesApi.listTemplateVersions(templateId);
      setVersions(vList);
    } catch (err: unknown) {
      if (err instanceof DOMException && err.name === 'AbortError') return;
      setError(err instanceof Error ? err.message : 'ไม่สามารถโหลดข้อมูลเทมเพลตได้');
    } finally {
      setLoading(false);
    }
  }, [templateId]);

  useEffect(() => {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

    loadTemplateData(controller.signal).finally(() => clearTimeout(timeoutId));

    return () => controller.abort();
  }, [loadTemplateData]);

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

  const saveHtmlAction = useCallback(async (changeNote?: string, samplePayload?: string) => {
    if (!templateId) throw new Error('ไม่พบ Template ID — กรุณาเปิด Template จากรายการก่อนบันทึก');
    setSaving(true);
    setError(null);
    try {
      const res = await templatesApi.saveTemplateHtml(templateId, {
        html,
        samplePayload,
        changeNote: changeNote || 'แก้ไขจาก Template Studio',
      });
      setInitialHtml(html);
      setIsDirty(false);
      if (samplePayload) {
        setPersistedSamplePayload(samplePayload);
      }
      const vList = await templatesApi.listTemplateVersions(templateId);
      setVersions(vList);
      return res.version;
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'บันทึก HTML ไม่สำเร็จ';
      setError(msg);
      throw new Error(msg);
    } finally {
      setSaving(false);
    }
  }, [templateId, html]);

  const validate = useCallback(async () => {
    if (!templateId) return;
    setValidating(true);
    try {
      const res = await templatesApi.validateTemplate(templateId, html);
      setValidationResult(res);
      return res;
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'ตรวจสอบโครงสร้างไม่สำเร็จ');
    } finally {
      setValidating(false);
    }
  }, [templateId, html]);

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
    saveHtml: saveHtmlAction,
    validate,
    reload: () => loadTemplateData(),
  };
}

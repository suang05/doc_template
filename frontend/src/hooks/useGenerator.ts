"use client";
import { useState, useCallback } from "react";
import { api } from "@/lib/api-client";
import { PRESETS } from "@/lib/presets";
import type {
  TemplateDetail,
  TemplateSchemaResponse,
  DocumentGenerationResponse,
  OutputFormat,
  InputMode,
  FieldMappingItem,
} from "@/types/api";

interface GenerationResult {
  response: DocumentGenerationResponse;
  executionTimeMs: number;
  format: OutputFormat;
  templateName: string;
}

export function useGenerator(templates: TemplateDetail[]) {
  const [selectedFileName, setSelectedFileName] = useState<string>("");
  const [schema, setSchema] = useState<TemplateSchemaResponse | null>(null);
  const [schemaLoading, setSchemaLoading] = useState(false);
  const [showPreviewModal, setShowPreviewModal] = useState(false);
  const [mappings, setMappings] = useState<FieldMappingItem[]>([]);

  const [outputFormat, setOutputFormat] = useState<OutputFormat>("pdf");
  const [inputMode, setInputMode] = useState<InputMode>("form");
  const [formData, setFormData] = useState<Record<string, unknown>>({});
  const [jsonText, setJsonText] = useState("{}");
  const [jsonError, setJsonError] = useState<string | null>(null);

  const [generating, setGenerating] = useState(false);
  const [result, setResult] = useState<GenerationResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const fetchMappings = useCallback(async (fileName: string) => {
    try {
      const res = await api.getTemplateMappings(fileName);
      setMappings(res.mappings ?? []);
    } catch {
      setMappings([]);
    }
  }, []);

  const fetchSchema = useCallback(async (fileName: string) => {
    setSchemaLoading(true);
    setSchema(null);
    try {
      const s = await api.getTemplateSchema(fileName);
      setSchema(s);

      // Check preset match first
      const presetKey = (Object.keys(PRESETS) as Array<keyof typeof PRESETS>).find(
        (k) => PRESETS[k].template === fileName
      );
      if (presetKey) {
        const p = PRESETS[presetKey];
        setFormData(p.data);
        setJsonText(JSON.stringify(p.data, null, 2));
        setOutputFormat(p.format as OutputFormat);
        return;
      }

      // Auto-populate defaults from schema
      const defaults: Record<string, unknown> = {};
      (s.variables ?? []).forEach((v) => {
        if (v.type === "table") {
          const cols = v.columns?.length ? v.columns : ["ItemNo", "Description", "Quantity", "UnitPrice", "Amount"];
          defaults[v.key] = [cols.reduce<Record<string, string>>((a, c) => ({ ...a, [c]: "" }), {})];
        } else if (v.type === "number") {
          defaults[v.key] = 0;
        } else if (v.type === "date") {
          defaults[v.key] = new Date().toISOString().split("T")[0];
        } else {
          defaults[v.key] = "";
        }
      });
      const data = s.sampleData ?? defaults;
      setFormData(data);
      setJsonText(JSON.stringify(data, null, 2));
    } catch {
      setSchema(null);
    } finally {
      setSchemaLoading(false);
    }
  }, []);

  const selectTemplate = useCallback((fileName: string) => {
    setSelectedFileName(fileName);
    setResult(null);
    setError(null);
    if (fileName) {
      fetchSchema(fileName);
      fetchMappings(fileName);
    }
  }, [fetchSchema, fetchMappings]);

  const loadPreset = useCallback((presetKey: keyof typeof PRESETS) => {
    const p = PRESETS[presetKey];
    setFormData(p.data);
    setJsonText(JSON.stringify(p.data, null, 2));
    setOutputFormat(p.format as OutputFormat);
    setJsonError(null);
    if (templates.some((t) => t.fileName === p.template)) {
      selectTemplate(p.template);
    }
  }, [templates, selectTemplate]);

  const updateFormField = useCallback((key: string, val: unknown) => {
    setFormData((prev) => {
      const next = { ...prev, [key]: val };
      setJsonText(JSON.stringify(next, null, 2));
      return next;
    });
    setJsonError(null);
  }, []);

  const updateJson = useCallback((text: string) => {
    setJsonText(text);
    try {
      setFormData(JSON.parse(text));
      setJsonError(null);
    } catch {
      setJsonError("รูปแบบ JSON ไม่ถูกต้อง");
    }
  }, []);

  const prettifyJson = useCallback(() => {
    try {
      const parsed = JSON.parse(jsonText);
      setFormData(parsed);
      setJsonText(JSON.stringify(parsed, null, 2));
      setJsonError(null);
    } catch (e) {
      setJsonError("รูปแบบ JSON ไม่ถูกต้อง: " + (e instanceof Error ? e.message : ""));
    }
  }, [jsonText]);

  const addTableRow = useCallback((tableKey: string, cols: string[]) => {
    const emptyRow = cols.reduce<Record<string, string>>((a, c) => ({ ...a, [c]: "" }), {});
    updateFormField(tableKey, [...(Array.isArray(formData[tableKey]) ? formData[tableKey] as unknown[] : []), emptyRow]);
  }, [formData, updateFormField]);

  const updateTableCell = useCallback((tableKey: string, idx: number, col: string, val: unknown) => {
    const rows = [...(Array.isArray(formData[tableKey]) ? formData[tableKey] as Record<string, unknown>[] : [])];
    if (!rows[idx]) return;
    rows[idx] = { ...rows[idx], [col]: val };
    updateFormField(tableKey, rows);
  }, [formData, updateFormField]);

  const deleteTableRow = useCallback((tableKey: string, idx: number) => {
    const rows = [...(Array.isArray(formData[tableKey]) ? formData[tableKey] as unknown[] : [])];
    rows.splice(idx, 1);
    updateFormField(tableKey, rows);
  }, [formData, updateFormField]);

  const generate = useCallback(async () => {
    if (!selectedFileName) {
      setError("กรุณาเลือกแม่แบบเอกสาร");
      return;
    }
    let raw: Record<string, unknown>;
    try {
      raw = JSON.parse(jsonText);
    } catch {
      setError("ข้อมูล JSON ไม่ถูกต้อง กรุณาตรวจสอบ Syntax ก่อนส่งคำขอ");
      return;
    }

    // Transform typed variables to prefixed keys the backend expects:
    // qrcode → "qr:key", barcode → "barcode:key"
    const vars = schema?.variables ?? [];
    const data: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(raw)) {
      const meta = vars.find((sv) => sv.key === k);
      if (meta?.type === "qrcode") {
        data[`qr:${k}`] = v;
      } else if (meta?.type === "barcode") {
        data[`barcode:${k}`] = v;
      } else {
        data[k] = v;
      }
    }

    setGenerating(true);
    setError(null);
    setResult(null);
    const t0 = performance.now();
    try {
      const res = await api.generateDocument({ templateName: selectedFileName, outputFormat, data });
      setResult({ response: res, executionTimeMs: Math.round(performance.now() - t0), format: outputFormat, templateName: selectedFileName });
    } catch (e) {
      setError(e instanceof Error ? e.message : "สร้างเอกสารไม่สำเร็จ");
    } finally {
      setGenerating(false);
    }
  }, [selectedFileName, outputFormat, jsonText, schema]);

  const selectedTemplate = templates.find((t) => t.fileName === selectedFileName) ?? null;
  const schemaVars = schema?.variables ?? [];
  const mappingLabels = Object.fromEntries(
    mappings.map((m) => [m.placeholder, m.label])
  );

  // Categorised field groups — consumed by FormFields, reusable by any future view
  const textVars     = schemaVars.filter((v) => ["text", "number", "date"].includes(v.type) && !v.key.startsWith("row:"));
  const qrVars       = schemaVars.filter((v) => v.type === "qrcode");
  const barVars      = schemaVars.filter((v) => v.type === "barcode");
  const tableVars    = schemaVars.filter((v) => v.type === "table");
  const knownKeys    = new Set(schemaVars.map((v) => v.key));
  const fallbackScalar = Object.keys(formData).filter((k) => !knownKeys.has(k) && !Array.isArray(formData[k]) && typeof formData[k] !== "object" && !k.startsWith("row:"));
  const fallbackTable  = Object.keys(formData).filter((k) => !knownKeys.has(k) && Array.isArray(formData[k]));

  return {
    // state
    selectedFileName,
    selectedTemplate,
    schema,
    schemaVars,
    schemaLoading,
    mappings,
    mappingLabels,
    // categorised field groups
    textVars,
    qrVars,
    barVars,
    tableVars,
    fallbackScalar,
    fallbackTable,
    showPreviewModal,
    setShowPreviewModal,
    outputFormat,
    setOutputFormat,
    inputMode,
    setInputMode,
    formData,
    jsonText,
    jsonError,
    generating,
    result,
    error,
    // actions
    selectTemplate,
    loadPreset,
    updateFormField,
    updateJson,
    prettifyJson,
    addTableRow,
    updateTableCell,
    deleteTableRow,
    generate,
  };
}

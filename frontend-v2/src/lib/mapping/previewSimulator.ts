import type { DataSourceType } from '@/types/api';
import type { MappingRow, PlaceholderType } from './mappingMapper';

export type PreviewResult = {
  ph: string;
  phType: PlaceholderType;
  ds: DataSourceType;
  value: string | null;
  transform: string | null;
};

// Dot-path accessor for JSON data
function dotGet(path: string, obj: Record<string, unknown>): string | null {
  const val = path.split('.').reduce<unknown>(
    (o, k) => (o && typeof o === 'object' ? (o as Record<string, unknown>)[k] : undefined),
    obj,
  );
  return val != null ? String(val) : null;
}

/**
 * Client-side preview simulation (approximate).
 * Pass 1: resolve json / sql sources.
 * Pass 2: evaluate mathExpression using resolved values.
 * Pure function — no side effects, no React.
 */
export function simulatePreview(rows: MappingRow[], jsonText: string): PreviewResult[] {
  let data: Record<string, unknown> = {};
  try { data = JSON.parse(jsonText); } catch { /* noop */ }

  const resolved: Record<string, string | null> = {};

  for (const r of rows) {
    if (r.dataSourceType === 'json') {
      resolved[r.placeholder] = r.sourcePath ? dotGet(r.sourcePath, data) : null;
    }
  }

  for (const r of rows) {
    if (r.dataSourceType === 'sql') {
      resolved[r.placeholder] = r.datasetAlias ? '[DB result]' : null;
    }
  }

  for (const r of rows) {
    if (r.mathExpression) {
      let expr = r.mathExpression;
      for (const [k, v] of Object.entries(resolved)) {
        if (v != null) expr = expr.replace(new RegExp(`\\b${k}\\b`, 'g'), v);
      }
      try {
        // eslint-disable-next-line no-new-func
        const res = Function('"use strict";return (' + expr + ')')();
        resolved[r.placeholder] = typeof res === 'number' ? String(+res.toFixed(4)) : String(res);
      } catch {
        resolved[r.placeholder] = null;
      }
    }
  }

  return rows.map(r => ({
    ph: r.placeholder,
    phType: r.placeholderType,
    ds: (r.dataSourceType ?? 'json') as DataSourceType,
    value: resolved[r.placeholder] ?? null,
    transform: r.transform ?? null,
  }));
}

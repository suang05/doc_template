import { tokens } from '@/tokens';
import type { FieldMappingDto, DataSourceType, SaveFieldMappingItem } from '@/types/api';

// ── PlaceholderType ────────────────────────────────────────────────────────────
// Frontend-only concept — never persisted to the API.
// Provides rendering hints (color dot) and controls transform-field availability.
// Always defaults to 'text' on load; intentionally ephemeral.
export const PH_TYPES = [
  { value: 'text',    label: 'ข้อความ', color: tokens.colors.textMuted },
  { value: 'table',   label: 'ตาราง',   color: tokens.categories.official.primary },
  { value: 'qrcode',  label: 'QR Code', color: tokens.categories.financial.primary },
  { value: 'barcode', label: 'Barcode', color: tokens.formats.html.primary },
  { value: 'image',   label: 'รูปภาพ', color: tokens.categories.contract.primary },
] as const;
export type PlaceholderType = typeof PH_TYPES[number]['value'];
export const PT_MAP = Object.fromEntries(
  PH_TYPES.map(t => [t.value, t]),
) as Record<PlaceholderType, typeof PH_TYPES[number]>;

// ── MappingRow ─────────────────────────────────────────────────────────────────
// Internal working type: SaveFieldMappingItem + frontend-only fields.
export type MappingRow = SaveFieldMappingItem & {
  _id: string;
  placeholderType: PlaceholderType;
};

// ── Factory functions ──────────────────────────────────────────────────────────
export function genId(): string {
  return Math.random().toString(36).slice(2, 10);
}

export function newRow(sortOrder: number): MappingRow {
  return {
    _id: genId(), placeholder: '', placeholderType: 'text',
    dataSourceType: 'json', sourcePath: '', label: '',
    required: false, defaultValue: null, transform: null,
    datasetAlias: null, resultPath: null, mathExpression: null, sortOrder,
  };
}

// ── DTO → MappingRow ───────────────────────────────────────────────────────────
export function dtoToRow(m: FieldMappingDto): MappingRow {
  return {
    _id: genId(),
    placeholder: m.placeholder,
    placeholderType: 'text',
    sourcePath: m.sourcePath,
    label: m.label,
    required: m.required,
    defaultValue: m.defaultValue ?? null,
    transform: m.transform ?? null,
    sortOrder: m.sortOrder,
    dataSourceType: (m.dataSourceType ?? 'json') as DataSourceType,
    datasetAlias: m.datasetAlias ?? null,
    resultPath: m.resultPath ?? null,
    mathExpression: m.mathExpression ?? null,
  };
}

// ── MappingRow → SaveFieldMappingItem ──────────────────────────────────────────
// Strips frontend-only fields and recomputes sortOrder from array index.
export function rowToSaveItem(row: MappingRow, index: number): SaveFieldMappingItem {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const { _id, placeholderType, ...rest } = row;
  return { ...rest, sortOrder: index };
}

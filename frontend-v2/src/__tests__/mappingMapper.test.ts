import { describe, it, expect } from 'vitest';
import { genId, newRow, dtoToRow, rowToSaveItem, PH_TYPES } from '@/lib/mapping/mappingMapper';
import type { FieldMappingDto } from '@/types/api';

// ── genId ───────────────────────────────────────────────────────────────────

describe('genId', () => {
  it('returns a non-empty string', () => {
    expect(typeof genId()).toBe('string');
    expect(genId().length).toBeGreaterThan(0);
  });

  it('returns unique values', () => {
    const ids = new Set(Array.from({ length: 100 }, genId));
    expect(ids.size).toBe(100);
  });
});

// ── newRow ──────────────────────────────────────────────────────────────────

describe('newRow', () => {
  it('returns a row with the given sortOrder', () => {
    const row = newRow(3);
    expect(row.sortOrder).toBe(3);
  });

  it('defaults placeholderType to text', () => {
    expect(newRow(0).placeholderType).toBe('text');
  });

  it('defaults dataSourceType to json', () => {
    expect(newRow(0).dataSourceType).toBe('json');
  });

  it('has empty placeholder and label', () => {
    const row = newRow(0);
    expect(row.placeholder).toBe('');
    expect(row.label).toBe('');
  });

  it('has null nullable fields', () => {
    const row = newRow(0);
    expect(row.defaultValue).toBeNull();
    expect(row.transform).toBeNull();
    expect(row.datasetAlias).toBeNull();
    expect(row.resultPath).toBeNull();
    expect(row.mathExpression).toBeNull();
  });

  it('has a _id field', () => {
    expect(newRow(0)._id).toBeTruthy();
  });
});

// ── dtoToRow ────────────────────────────────────────────────────────────────

const baseDto: FieldMappingDto = {
  id: '00000000-0000-0000-0000-000000000001',
  templateId: '00000000-0000-0000-0000-000000000002',
  placeholder: 'customerName',
  sourcePath: 'customer.name',
  label: 'ชื่อลูกค้า',
  required: true,
  defaultValue: null,
  transform: 'uppercase',
  sortOrder: 2,
  dataSourceType: 'json',
  datasetAlias: null,
  resultPath: null,
  mathExpression: null,
};

describe('dtoToRow', () => {
  it('maps all FieldMappingDto fields correctly', () => {
    const row = dtoToRow(baseDto);
    expect(row.placeholder).toBe('customerName');
    expect(row.sourcePath).toBe('customer.name');
    expect(row.label).toBe('ชื่อลูกค้า');
    expect(row.required).toBe(true);
    expect(row.transform).toBe('uppercase');
    expect(row.sortOrder).toBe(2);
    expect(row.dataSourceType).toBe('json');
  });

  it('always defaults placeholderType to "text" regardless of DTO value', () => {
    // placeholderType is frontend-only and intentionally not persisted
    const row = dtoToRow(baseDto);
    expect(row.placeholderType).toBe('text');
  });

  it('assigns a unique _id (not from DTO id)', () => {
    const row = dtoToRow(baseDto);
    expect(row._id).toBeTruthy();
    expect(row._id).not.toBe(baseDto.id);
  });

  it('maps null optional fields as null', () => {
    const row = dtoToRow(baseDto);
    expect(row.defaultValue).toBeNull();
    expect(row.datasetAlias).toBeNull();
    expect(row.resultPath).toBeNull();
    expect(row.mathExpression).toBeNull();
  });

  it('maps sql dataSourceType correctly', () => {
    const dto: FieldMappingDto = { ...baseDto, dataSourceType: 'sql', datasetAlias: 'ds1', resultPath: '0.total' };
    const row = dtoToRow(dto);
    expect(row.dataSourceType).toBe('sql');
    expect(row.datasetAlias).toBe('ds1');
    expect(row.resultPath).toBe('0.total');
  });
});

// ── rowToSaveItem ────────────────────────────────────────────────────────────

describe('rowToSaveItem', () => {
  it('strips _id from the output', () => {
    const row = dtoToRow(baseDto);
    const item = rowToSaveItem(row, 0);
    expect(item).not.toHaveProperty('_id');
  });

  it('strips placeholderType from the output', () => {
    const row = dtoToRow(baseDto);
    const item = rowToSaveItem(row, 0);
    expect(item).not.toHaveProperty('placeholderType');
  });

  it('uses the index as sortOrder, not the original value', () => {
    const row = dtoToRow(baseDto); // original sortOrder = 2
    const item = rowToSaveItem(row, 5);
    expect(item.sortOrder).toBe(5);
  });

  it('preserves all other fields', () => {
    const row = dtoToRow(baseDto);
    const item = rowToSaveItem(row, 0);
    expect(item.placeholder).toBe('customerName');
    expect(item.sourcePath).toBe('customer.name');
    expect(item.label).toBe('ชื่อลูกค้า');
    expect(item.required).toBe(true);
    expect(item.transform).toBe('uppercase');
    expect(item.dataSourceType).toBe('json');
  });
});

// ── PH_TYPES ─────────────────────────────────────────────────────────────────

describe('PH_TYPES', () => {
  it('includes text as first entry', () => {
    expect(PH_TYPES[0].value).toBe('text');
  });

  it('all entries have value, label and color', () => {
    for (const t of PH_TYPES) {
      expect(t.value).toBeTruthy();
      expect(t.label).toBeTruthy();
      expect(t.color).toBeTruthy();
    }
  });
});

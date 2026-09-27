import { describe, it, expect } from 'vitest';
import { simulatePreview } from '@/lib/mapping/previewSimulator';
import type { MappingRow } from '@/lib/mapping/mappingMapper';

function makeRow(overrides: Partial<MappingRow> = {}): MappingRow {
  return {
    _id: 'test-id',
    placeholder: 'field',
    placeholderType: 'text',
    dataSourceType: 'json',
    sourcePath: '',
    label: 'Field',
    required: false,
    defaultValue: null,
    transform: null,
    datasetAlias: null,
    resultPath: null,
    mathExpression: null,
    sortOrder: 0,
    ...overrides,
  };
}

// ── Basic json resolution ────────────────────────────────────────────────────

describe('simulatePreview — json', () => {
  it('returns empty array when rows is empty', () => {
    expect(simulatePreview([], '{}')).toEqual([]);
  });

  it('resolves top-level json path', () => {
    const rows = [makeRow({ placeholder: 'name', sourcePath: 'name' })];
    const [result] = simulatePreview(rows, '{"name":"Alice"}');
    expect(result.value).toBe('Alice');
  });

  it('resolves nested dot-path', () => {
    const rows = [makeRow({ placeholder: 'amount', sourcePath: 'payment.subtotal' })];
    const [result] = simulatePreview(rows, '{"payment":{"subtotal":2500000}}');
    expect(result.value).toBe('2500000');
  });

  it('returns null when path does not exist', () => {
    const rows = [makeRow({ placeholder: 'missing', sourcePath: 'does.not.exist' })];
    const [result] = simulatePreview(rows, '{"a":1}');
    expect(result.value).toBeNull();
  });

  it('returns null when sourcePath is empty', () => {
    const rows = [makeRow({ placeholder: 'empty', sourcePath: '' })];
    const [result] = simulatePreview(rows, '{"empty":"x"}');
    expect(result.value).toBeNull();
  });

  it('survives invalid JSON gracefully', () => {
    const rows = [makeRow({ placeholder: 'x', sourcePath: 'x' })];
    const [result] = simulatePreview(rows, 'not-json');
    expect(result.value).toBeNull();
  });
});

// ── SQL resolution ───────────────────────────────────────────────────────────

describe('simulatePreview — sql', () => {
  it('returns [DB result] when datasetAlias is set', () => {
    const rows = [makeRow({ placeholder: 'total', dataSourceType: 'sql', datasetAlias: 'sales' })];
    const [result] = simulatePreview(rows, '{}');
    expect(result.value).toBe('[DB result]');
  });

  it('returns null when datasetAlias is null', () => {
    const rows = [makeRow({ placeholder: 'total', dataSourceType: 'sql', datasetAlias: null })];
    const [result] = simulatePreview(rows, '{}');
    expect(result.value).toBeNull();
  });
});

// ── Math expression resolution ────────────────────────────────────────────────

describe('simulatePreview — mathExpression', () => {
  it('evaluates simple math expression', () => {
    const rows = [makeRow({ placeholder: 'result', mathExpression: '1 + 2' })];
    const [result] = simulatePreview(rows, '{}');
    expect(result.value).toBe('3');
  });

  it('substitutes resolved placeholders into expression', () => {
    const rows = [
      makeRow({ _id: 'a', placeholder: 'qty', sourcePath: 'qty' }),
      makeRow({ _id: 'b', placeholder: 'total', mathExpression: 'qty * 100' }),
    ];
    const results = simulatePreview(rows, '{"qty":5}');
    expect(results[1].value).toBe('500');
  });

  it('handles floating point and rounds to 4 decimals', () => {
    const rows = [makeRow({ placeholder: 'tax', mathExpression: '100 * 0.07' })];
    const [result] = simulatePreview(rows, '{}');
    expect(result.value).toBe('7');
  });

  it('returns null when expression throws', () => {
    const rows = [makeRow({ placeholder: 'bad', mathExpression: 'this is not valid !!!' })];
    const [result] = simulatePreview(rows, '{}');
    expect(result.value).toBeNull();
  });
});

// ── Result shape ─────────────────────────────────────────────────────────────

describe('simulatePreview — result shape', () => {
  it('preserves ph, phType, ds, transform in result', () => {
    const rows = [makeRow({
      placeholder: 'name', sourcePath: 'name',
      placeholderType: 'qrcode', dataSourceType: 'json',
      transform: 'uppercase',
    })];
    const [result] = simulatePreview(rows, '{"name":"Alice"}');
    expect(result.ph).toBe('name');
    expect(result.phType).toBe('qrcode');
    expect(result.ds).toBe('json');
    expect(result.transform).toBe('uppercase');
  });

  it('returns one result per row in the same order', () => {
    const rows = [
      makeRow({ _id: 'x', placeholder: 'a', sourcePath: 'a' }),
      makeRow({ _id: 'y', placeholder: 'b', sourcePath: 'b' }),
    ];
    const results = simulatePreview(rows, '{"a":"1","b":"2"}');
    expect(results[0].ph).toBe('a');
    expect(results[1].ph).toBe('b');
  });
});

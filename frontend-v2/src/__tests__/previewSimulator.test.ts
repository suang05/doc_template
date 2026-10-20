import { describe, it, expect } from 'vitest';
import { simulatePreview } from '@/lib/mapping/previewSimulator';
import { newRow, type MappingRow } from '@/lib/mapping/mappingMapper';

function makeRow(overrides: Partial<MappingRow> = {}): MappingRow {
  return {
    ...newRow(0),
    _id: 'test-id',
    placeholder: 'field',
    label: 'Field',
    ...overrides,
  };
}

// ── Basic JSON resolution ───────────────────────────────────────────────────

describe('simulatePreview — json', () => {
  it('returns empty array when rows is empty', () => {
    expect(simulatePreview([], '{}')).toEqual([]);
  });

  it.each([
    ['top-level json path', { placeholder: 'name', sourcePath: 'name' }, '{"name":"Alice"}', 'Alice'],
    ['nested dot-path', { placeholder: 'amount', sourcePath: 'payment.subtotal' }, '{"payment":{"subtotal":2500000}}', '2500000'],
    ['missing path', { placeholder: 'missing', sourcePath: 'does.not.exist' }, '{"a":1}', null],
    ['empty sourcePath', { placeholder: 'empty', sourcePath: '' }, '{"empty":"x"}', null],
    ['invalid JSON payload', { placeholder: 'x', sourcePath: 'x' }, 'not-json', null],
  ])('resolves %s', (_, rowConfig, payload, expected) => {
    const rows = [makeRow(rowConfig)];
    const [result] = simulatePreview(rows, payload);
    expect(result.value).toBe(expected);
  });
});

// ── SQL resolution ───────────────────────────────────────────────────────────

describe('simulatePreview — sql', () => {
  it.each([
    ['returns [DB result] when datasetAlias is set', 'sales', '[DB result]'],
    ['returns null when datasetAlias is null', null, null],
  ])('%s', (_, datasetAlias, expected) => {
    const rows = [makeRow({ placeholder: 'total', dataSourceType: 'sql', datasetAlias })];
    const [result] = simulatePreview(rows, '{}');
    expect(result.value).toBe(expected);
  });
});

// ── Math expression resolution ────────────────────────────────────────────────

describe('simulatePreview — mathExpression', () => {
  it.each([
    ['evaluates simple math expression', '1 + 2', '3'],
    ['handles floating point and rounds to 4 decimals', '100 * 0.07', '7'],
    ['returns null when expression throws', 'this is not valid !!!', null],
  ])('%s', (_, mathExpression, expected) => {
    const rows = [makeRow({ placeholder: 'result', mathExpression })];
    const [result] = simulatePreview(rows, '{}');
    expect(result.value).toBe(expected);
  });

  it('substitutes resolved placeholders into expression', () => {
    const rows = [
      makeRow({ _id: 'a', placeholder: 'qty', sourcePath: 'qty' }),
      makeRow({ _id: 'b', placeholder: 'total', mathExpression: 'qty * 100' }),
    ];
    const results = simulatePreview(rows, '{"qty":5}');
    expect(results[1].value).toBe('500');
  });
});

// ── Result shape ─────────────────────────────────────────────────────────────

describe('simulatePreview — result shape', () => {
  it('preserves ph, phType, ds, transform in result', () => {
    const rows = [
      makeRow({
        placeholder: 'name',
        sourcePath: 'name',
        placeholderType: 'qrcode',
        dataSourceType: 'json',
        transform: 'uppercase',
      }),
    ];
    const [result] = simulatePreview(rows, '{"name":"Alice"}');
    expect(result).toEqual({
      ph: 'name',
      phType: 'qrcode',
      ds: 'json',
      transform: 'uppercase',
      value: 'Alice',
    });
  });

  it('returns one result per row in the same order', () => {
    const rows = [
      makeRow({ _id: 'x', placeholder: 'a', sourcePath: 'a' }),
      makeRow({ _id: 'y', placeholder: 'b', sourcePath: 'b' }),
    ];
    const results = simulatePreview(rows, '{"a":"1","b":"2"}');
    expect(results.map((r) => r.ph)).toEqual(['a', 'b']);
  });
});

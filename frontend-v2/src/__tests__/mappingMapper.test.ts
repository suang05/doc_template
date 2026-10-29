import { describe, it, expect } from 'vitest';
import { genId, newRow, dtoToRow, rowToSaveItem } from '@/features/mapping/domain/mappingMapper';
import type { FieldMappingDto } from '@/types/api';

describe('genId', () => {
  it('generates unique non-empty string IDs', () => {
    const id = genId();
    expect(typeof id).toBe('string');
    expect(id.length).toBeGreaterThan(0);

    const ids = new Set(Array.from({ length: 100 }, genId));
    expect(ids.size).toBe(100);
  });
});

describe('newRow', () => {
  it('creates a new MappingRow with default values and specified sortOrder', () => {
    const row = newRow(3);
    expect(row).toEqual({
      _id: expect.any(String),
      placeholder: '',
      placeholderType: 'text',
      dataSourceType: 'json',
      sourcePath: '',
      label: '',
      required: false,
      defaultValue: null,
      transform: null,
      datasetAlias: null,
      resultPath: null,
      mathExpression: null,
      sortOrder: 3,
    });
    expect(row._id.length).toBeGreaterThan(0);
  });
});

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
  it('maps FieldMappingDto to MappingRow with ephemeral fields', () => {
    const row = dtoToRow(baseDto);
    expect(row).toEqual({
      _id: expect.any(String),
      placeholder: 'customerName',
      placeholderType: 'text',
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
    });
    expect(row._id).not.toBe(baseDto.id);
  });

  it('maps sql dataSourceType with alias and resultPath', () => {
    const dto: FieldMappingDto = {
      ...baseDto,
      dataSourceType: 'sql',
      datasetAlias: 'ds1',
      resultPath: '0.total',
    };
    const row = dtoToRow(dto);
    expect(row).toEqual(
      expect.objectContaining({
        dataSourceType: 'sql',
        datasetAlias: 'ds1',
        resultPath: '0.total',
      }),
    );
  });
});

describe('rowToSaveItem', () => {
  it('transforms MappingRow into SaveFieldMappingItem stripping ephemeral fields and reindexing', () => {
    const row = dtoToRow(baseDto);
    const item = rowToSaveItem(row, 5);
    expect(item).toEqual({
      placeholder: 'customerName',
      sourcePath: 'customer.name',
      label: 'ชื่อลูกค้า',
      required: true,
      defaultValue: null,
      transform: 'uppercase',
      sortOrder: 5,
      dataSourceType: 'json',
      datasetAlias: null,
      resultPath: null,
      mathExpression: null,
    });
    expect(item).not.toHaveProperty('_id');
    expect(item).not.toHaveProperty('placeholderType');
  });
});

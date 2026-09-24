import { describe, it, expect } from 'vitest';
import {
  dataConnectionSchema,
  createDataConnectionSchema,
  testDataConnectionSchema,
} from '../schemas/dataconnection.schema';
import {
  DatasetSchema,
  CreateDatasetSchema,
} from '../schemas/dataset.schema';
import {
  SaveTemplateDatasetItemSchema,
  TemplateDatasetDtoSchema,
} from '../schemas/templateDataset.schema';

describe('Datasources and Datasets Zod Schemas Integrity', () => {
  describe('DataConnection Schemas', () => {
    it('should parse valid DataConnection', () => {
      const valid = {
        id: '123e4567-e89b-12d3-a456-426614174000',
        name: 'Postgres Production',
        provider: 'PostgreSQL',
      };
      const result = dataConnectionSchema.parse(valid);
      expect(result.name).toBe('Postgres Production');
      expect(result.provider).toBe('PostgreSQL');
    });

    it('should validate CreateDataConnectionSchema requires all fields', () => {
      const valid = {
        name: 'Main DB',
        provider: 'PostgreSQL',
        connectionString: 'Host=db;Port=5432;',
      };
      expect(createDataConnectionSchema.parse(valid).connectionString).toBe('Host=db;Port=5432;');

      const missing = {
        name: 'Main DB',
        provider: 'PostgreSQL',
        connectionString: '',
      };
      expect(() => createDataConnectionSchema.parse(missing)).toThrow();
    });

    it('should validate TestDataConnectionSchema', () => {
      const valid = {
        provider: 'PostgreSQL',
        connectionString: 'Host=localhost;',
      };
      expect(testDataConnectionSchema.parse(valid).provider).toBe('PostgreSQL');
    });
  });

  describe('Dataset Schemas', () => {
    it('should parse valid Dataset', () => {
      const valid = {
        id: '123e4567-e89b-12d3-a456-426614174000',
        name: 'Customer Invoices',
        dataConnectionId: '223e4567-e89b-12d3-a456-426614174000',
        dataConnectionName: 'Postgres Prod',
        sqlQuery: 'SELECT * FROM invoices WHERE id = @id',
        cacheSeconds: 300,
      };
      const parsed = DatasetSchema.parse(valid);
      expect(parsed.cacheSeconds).toBe(300);
      expect(parsed.name).toBe('Customer Invoices');
    });

    it('should reject invalid UUID in CreateDatasetSchema', () => {
      const invalid = {
        name: 'Invalid Dataset',
        dataConnectionId: 'not-a-uuid',
        sqlQuery: 'SELECT 1',
      };
      expect(() => CreateDatasetSchema.parse(invalid)).toThrow();
    });
  });

  describe('TemplateDataset Schemas', () => {
    it('should parse valid SaveTemplateDatasetItem', () => {
      const valid = {
        datasetId: '123e4567-e89b-12d3-a456-426614174000',
        alias: 'orders',
        sortOrder: 1,
      };
      const parsed = SaveTemplateDatasetItemSchema.parse(valid);
      expect(parsed.alias).toBe('orders');
      expect(parsed.sortOrder).toBe(1);
    });

    it('should parse TemplateDatasetDto', () => {
      const valid = {
        id: '123e4567-e89b-12d3-a456-426614174000',
        templateId: '223e4567-e89b-12d3-a456-426614174000',
        datasetId: '323e4567-e89b-12d3-a456-426614174000',
        datasetName: 'Orders Dataset',
        alias: 'orders',
        sortOrder: 0,
      };
      const parsed = TemplateDatasetDtoSchema.parse(valid);
      expect(parsed.datasetName).toBe('Orders Dataset');
    });
  });
});

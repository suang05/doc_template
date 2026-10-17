import { describe, it, expect } from 'vitest';
import {
  TemplateDtoSchema,
  CreateTemplateRequestSchema,
  SaveTemplateHtmlRequestSchema,
  TemplateVersionDtoSchema,
  TemplateValidationResultSchema,
  TemplateStudioDtoSchema,
  TemplateSchemaDtoSchema,
} from '@/schemas/template.schema';
import { loginRequestSchema, loginResponseSchema } from '@/schemas/auth.schema';
import {
  dataConnectionSchema,
  createDataConnectionSchema,
  testDataConnectionSchema,
} from '@/schemas/dataconnection.schema';
import {
  DatasetSchema,
  CreateDatasetSchema,
} from '@/schemas/dataset.schema';
import {
  SaveTemplateDatasetItemSchema,
  TemplateDatasetDtoSchema,
} from '@/schemas/templateDataset.schema';
import { GenerateDocumentRequestSchema } from '@/schemas/document.schema';
import { SaveFieldMappingItemSchema } from '@/schemas/mapping.schema';
import { CreateApiKeyRequestSchema } from '@/schemas/apikey.schema';
import { PagedLogsResponseSchema, PaginationMetadataSchema } from '@/schemas/log.schema';

// ── TemplateDtoSchema ───────────────────────────────────────────────────────

describe('TemplateDtoSchema', () => {
  const valid = {
    id: '123e4567-e89b-12d3-a456-426614174000',
    name: 'สัญญาจะซื้อจะขาย',
    slug: 'contract-sale',
    category: 'legal',
    isActive: true,
    currentVersionId: '123e4567-e89b-12d3-a456-426614174001',
    fileFormat: 'html' as const,
    createdAt: '2026-09-01T00:00:00Z',
    updatedAt: '2026-09-20T00:00:00Z',
  };

  it('parses a valid TemplateDto', () => {
    expect(() => TemplateDtoSchema.parse(valid)).not.toThrow();
  });

  it('rejects invalid UUID for id', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, id: 'not-a-uuid' })).toThrow();
  });

  it('rejects empty name', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, name: '' })).toThrow();
  });

  it('rejects slug with uppercase letters', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, slug: 'MyTemplate' })).toThrow();
  });

  it('rejects slug with spaces', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, slug: 'my template' })).toThrow();
  });

  it('rejects slug with underscores', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, slug: 'my-template_v2' })).toThrow();
  });

  it('accepts slug with hyphens', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, slug: 'my-template-v2' })).not.toThrow();
  });

  it('rejects slug shorter than 2 characters', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, slug: 'a' })).toThrow();
  });

  it('rejects slug with consecutive hyphens', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, slug: 'my--template' })).toThrow();
  });

  it('rejects slug with leading or trailing hyphens', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, slug: '-my-template' })).toThrow();
    expect(() => TemplateDtoSchema.parse({ ...valid, slug: 'my-template-' })).toThrow();
  });

  it('accepts null category', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, category: null })).not.toThrow();
  });

  it('accepts null currentVersionId', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, currentVersionId: null })).not.toThrow();
  });
});

// ── CreateTemplateRequestSchema ─────────────────────────────────────────────

describe('CreateTemplateRequestSchema', () => {
  it('parses valid create request', () => {
    expect(() => CreateTemplateRequestSchema.parse({ name: 'Test', slug: 'test-template' })).not.toThrow();
  });

  it('rejects empty name', () => {
    expect(() => CreateTemplateRequestSchema.parse({ name: '', slug: 'test' })).toThrow();
  });

  it('rejects empty slug', () => {
    expect(() => CreateTemplateRequestSchema.parse({ name: 'Test', slug: '' })).toThrow();
  });

  it('rejects slug with invalid characters or underscores', () => {
    expect(() => CreateTemplateRequestSchema.parse({ name: 'Test', slug: 'Test Template!' })).toThrow();
    expect(() => CreateTemplateRequestSchema.parse({ name: 'Test', slug: 'test_template' })).toThrow();
  });

  it('rejects slug shorter than 2 characters', () => {
    expect(() => CreateTemplateRequestSchema.parse({ name: 'Test', slug: 'a' })).toThrow();
  });

  it('accepts optional htmlContent', () => {
    expect(() => CreateTemplateRequestSchema.parse({
      name: 'Test',
      slug: 'test',
      htmlContent: '<!DOCTYPE html><html></html>',
    })).not.toThrow();
  });
});

// ── SaveTemplateHtmlRequestSchema ───────────────────────────────────────────

describe('SaveTemplateHtmlRequestSchema', () => {
  it('parses valid request', () => {
    expect(() => SaveTemplateHtmlRequestSchema.parse({ html: '<p>Hello</p>' })).not.toThrow();
  });

  it('rejects empty html', () => {
    expect(() => SaveTemplateHtmlRequestSchema.parse({ html: '' })).toThrow();
  });

  it('accepts optional samplePayload and changeNote', () => {
    expect(() => SaveTemplateHtmlRequestSchema.parse({
      html: '<p>Hello</p>',
      samplePayload: '{"name":"Alice"}',
      changeNote: 'Update layout',
    })).not.toThrow();
  });
});

// ── TemplateVersionDtoSchema ────────────────────────────────────────────────

describe('TemplateVersionDtoSchema', () => {
  const valid = {
    id: '123e4567-e89b-12d3-a456-426614174000',
    templateId: '123e4567-e89b-12d3-a456-426614174001',
    version: 1,
    storageKey: 'templates/archive/contract_v1.html',
    createdAt: '2026-09-01T00:00:00Z',
  };

  it('parses a valid version DTO', () => {
    expect(() => TemplateVersionDtoSchema.parse(valid)).not.toThrow();
  });

  it('rejects non-integer version', () => {
    expect(() => TemplateVersionDtoSchema.parse({ ...valid, version: 1.5 })).toThrow();
  });
});

// ── TemplateValidationResultSchema ─────────────────────────────────────────

describe('TemplateValidationResultSchema', () => {
  it('parses valid result', () => {
    const result = TemplateValidationResultSchema.parse({ valid: true, errors: [], fields: ['name', 'amount'] });
    expect(result.valid).toBe(true);
    expect(result.fields).toHaveLength(2);
  });

  it('defaults errors and fields to empty arrays', () => {
    const result = TemplateValidationResultSchema.parse({ valid: false });
    expect(result.errors).toEqual([]);
    expect(result.fields).toEqual([]);
  });
});

// ── TemplateStudioDtoSchema & TemplateSchemaDtoSchema ───────────────────────

describe('TemplateStudioDto and TemplateSchemaDto Schemas', () => {
  it('should validate TemplateStudioDto', () => {
    const studio = {
      html: '<h1>Template</h1>',
      samplePayload: '{"amount":100}',
      dataSchema: '{"type":"object"}',
      version: 2,
    };
    const parsedStudio = TemplateStudioDtoSchema.parse(studio);
    expect(parsedStudio.version).toBe(2);
  });

  it('should validate TemplateSchemaDto', () => {
    const schema = {
      templateId: '11111111-1111-1111-1111-111111111111',
      slug: 'contract-test',
      format: 'html',
      version: 1,
      dataSchema: '{"type":"object"}',
      samplePayload: '{"test":true}',
    };
    const parsedSchema = TemplateSchemaDtoSchema.parse(schema);
    expect(parsedSchema.slug).toBe('contract-test');
    expect(parsedSchema.format).toBe('html');
  });
});

// ── GenerateDocumentRequestSchema ───────────────────────────────────────────

describe('GenerateDocumentRequestSchema', () => {
  it('should accept valid GenerateDocumentRequest', () => {
    const valid = {
      payload: { contractNo: 'SMK-001', amount: 1000 },
      outputFormat: 'pdf',
      documentRef: 'DOC-001',
    };
    const parsed = GenerateDocumentRequestSchema.parse(valid);
    expect(parsed.outputFormat).toBe('pdf');
    expect(parsed.payload.amount).toBe(1000);
  });

  it('should reject invalid output format in GenerateDocumentRequest', () => {
    const invalid = {
      payload: {},
      outputFormat: 'bmp', // invalid
    };
    expect(() => GenerateDocumentRequestSchema.parse(invalid)).toThrow();
  });
});

// ── SaveFieldMappingItemSchema ──────────────────────────────────────────────

describe('SaveFieldMappingItemSchema', () => {
  it('should validate FieldMapping items', () => {
    const valid = {
      placeholder: 'totalAmount',
      sourcePath: 'payment.total',
      label: 'ยอดชำระสุทธิ',
      required: true,
      transform: 'thai_baht_text',
      sortOrder: 1,
    };
    const parsed = SaveFieldMappingItemSchema.parse(valid);
    expect(parsed.transform).toBe('thai_baht_text');
  });
});

// ── CreateApiKeyRequestSchema ───────────────────────────────────────────────

describe('CreateApiKeyRequestSchema', () => {
  it('should validate CreateApiKeyRequest', () => {
    const valid = {
      name: 'Sales App Key',
      callerApp: 'sales-app',
    };
    expect(CreateApiKeyRequestSchema.parse(valid).callerApp).toBe('sales-app');

    const empty = {
      name: '',
      callerApp: '',
    };
    expect(() => CreateApiKeyRequestSchema.parse(empty)).toThrow();
  });
});

// ── PagedLogsResponseSchema & PaginationMetadataSchema ─────────────────────

describe('Log Schemas Validation Integrity', () => {
  it('should validate PagedLogsResponseSchema conforming to global pagination standard', () => {
    const valid = {
      data: [
        {
          id: '01926b42-7c3a-7000-8000-123456789abc',
          status: 'success',
          createdAt: '2026-10-10T00:00:00Z',
        },
      ],
      pagination: {
        page: 1,
        limit: 20,
        totalCount: 42,
        totalPages: 3,
      },
    };
    const parsed = PagedLogsResponseSchema.parse(valid);
    expect(parsed.data).toHaveLength(1);
    expect(parsed.pagination.totalCount).toBe(42);
    expect(parsed.pagination.totalPages).toBe(3);
    expect(parsed.pagination.limit).toBe(20);
    expect(parsed.pagination.page).toBe(1);
    expect(() => PaginationMetadataSchema.parse(valid.pagination)).not.toThrow();
  });
});

// ── Datasources and Datasets Zod Schemas ───────────────────────────────────

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
        templateId: '223e4567-e89b-12d3-a456-426614174001',
        datasetId: '323e4567-e89b-12d3-a456-426614174002',
        datasetName: 'Orders Dataset',
        alias: 'orders',
        sortOrder: 0,
      };
      const parsed = TemplateDatasetDtoSchema.parse(valid);
      expect(parsed.datasetName).toBe('Orders Dataset');
    });
  });
});

// ── Auth Schemas ────────────────────────────────────────────────────────────

describe('loginRequestSchema', () => {
  it('parses valid credentials', () => {
    expect(() => loginRequestSchema.parse({ email: 'user@sammakorn.co.th', password: 'secret123' })).not.toThrow();
  });

  it('rejects invalid email format', () => {
    expect(() => loginRequestSchema.parse({ email: 'not-an-email', password: 'secret' })).toThrow();
  });

  it('rejects empty password', () => {
    expect(() => loginRequestSchema.parse({ email: 'user@example.com', password: '' })).toThrow();
  });
});

describe('loginResponseSchema', () => {
  const valid = {
    token: 'eyJhbGciOiJIUzI1NiJ9.test.sig',
    expiresAt: '2026-10-01T00:00:00Z',
    user: { email: 'user@sammakorn.co.th', firstName: 'สมชาย', lastName: 'ใจดี' },
  };

  it('parses a valid login response', () => {
    expect(() => loginResponseSchema.parse(valid)).not.toThrow();
  });

  it('rejects missing user fields', () => {
    expect(() => loginResponseSchema.parse({ ...valid, user: { email: 'a@b.com' } })).toThrow();
  });
});

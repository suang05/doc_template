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
import {
  SaveFieldMappingItemSchema,
  FieldMappingDtoSchema,
  ThaiTransformTypes,
} from '@/schemas/mapping.schema';
import { CreateApiKeyRequestSchema } from '@/schemas/apikey.schema';
import { PagedLogsResponseSchema, PaginationMetadataSchema, LogPayloadResultDtoSchema } from '@/schemas/log.schema';

// ── ThaiTransformTypes Compatibility ───────────────────────────────────────

describe('ThaiTransformTypes', () => {
  it('defines 7 required transformation options with Thai label and example', () => {
    const values = ThaiTransformTypes.map((t) => t.value);
    expect(values).toEqual([
      'thai_baht_text',
      'thai_date',
      'thai_currency',
      'thai_id_card',
      'thai_phone',
      'uppercase',
      'lowercase',
    ]);
    ThaiTransformTypes.forEach((t) => expect(t.label.length > 0 && t.example.length > 0).toBe(true));
  });
});

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

  it.each([
    ['base valid DTO', {}],
    ['slug with hyphens and numbers', { slug: 'my-template-v2' }],
    ['null category', { category: null }],
    ['null currentVersionId', { currentVersionId: null }],
  ])('accepts valid variation (%s)', (_, override) => {
    expect(() => TemplateDtoSchema.parse({ ...valid, ...override })).not.toThrow();
  });

  it.each([
    ['invalid UUID id', { id: 'not-a-uuid' }],
    ['empty name', { name: '' }],
    ['slug with uppercase', { slug: 'MyTemplate' }],
    ['slug with spaces', { slug: 'my template' }],
    ['slug with underscores', { slug: 'my-template_v2' }],
    ['slug too short', { slug: 'a' }],
    ['slug with consecutive hyphens', { slug: 'my--template' }],
    ['slug with leading hyphen', { slug: '-my-template' }],
    ['slug with trailing hyphen', { slug: 'my-template-' }],
  ])('rejects invalid variation (%s)', (_, override) => {
    expect(() => TemplateDtoSchema.parse({ ...valid, ...override })).toThrow();
  });
});

// ── CreateTemplateRequestSchema ─────────────────────────────────────────────

describe('CreateTemplateRequestSchema', () => {
  const valid = { name: 'Test', slug: 'test-template' };

  it.each([
    ['minimal valid', {}],
    ['with optional htmlContent', { htmlContent: '<!DOCTYPE html><html></html>' }],
    ['with null category', { category: null }],
  ])('accepts valid variation (%s)', (_, override) => {
    expect(() => CreateTemplateRequestSchema.parse({ ...valid, ...override })).not.toThrow();
  });

  it.each([
    ['empty name', { name: '' }],
    ['empty slug', { slug: '' }],
    ['slug with spaces and special chars', { slug: 'Test Template!' }],
    ['slug with underscores', { slug: 'test_template' }],
    ['slug shorter than 2 chars', { slug: 'a' }],
  ])('rejects invalid variation (%s)', (_, override) => {
    expect(() => CreateTemplateRequestSchema.parse({ ...valid, ...override })).toThrow();
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
    expect(() =>
      SaveTemplateHtmlRequestSchema.parse({
        html: '<p>Hello</p>',
        samplePayload: '{"name":"Alice"}',
        changeNote: 'Update layout',
      }),
    ).not.toThrow();
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
    const result = TemplateValidationResultSchema.parse({
      valid: true,
      errors: [],
      fields: ['name', 'amount'],
    });
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
      outputFormat: 'bmp',
    };
    expect(() => GenerateDocumentRequestSchema.parse(invalid)).toThrow();
  });
});

// ── FieldMapping Schemas ────────────────────────────────────────────────────

describe('FieldMapping Schemas', () => {
  const validItem = {
    placeholder: 'totalAmount',
    sourcePath: 'payment.total',
    label: 'ยอดชำระสุทธิ',
    required: true,
    transform: 'thai_baht_text',
    sortOrder: 1,
  };

  it('validates SaveFieldMappingItemSchema and derived FieldMappingDtoSchema', () => {
    const item = SaveFieldMappingItemSchema.parse(validItem);
    expect(item.transform).toBe('thai_baht_text');

    const dto = FieldMappingDtoSchema.parse({
      ...validItem,
      id: '123e4567-e89b-12d3-a456-426614174000',
      templateId: '123e4567-e89b-12d3-a456-426614174001',
    });
    expect(dto.id).toBe('123e4567-e89b-12d3-a456-426614174000');
    expect(dto.placeholder).toBe('totalAmount');
  });

  it.each([
    ['empty placeholder', { placeholder: '' }],
    ['empty label', { label: '' }],
    ['invalid dataSourceType', { dataSourceType: 'unsupported' }],
  ])('rejects invalid field mapping (%s)', (_, override) => {
    expect(() => SaveFieldMappingItemSchema.parse({ ...validItem, ...override })).toThrow();
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

  it('should validate LogPayloadResultDtoSchema for inline and offloaded payloads', () => {
    const inline = {
      logId: '01926b42-7c3a-7000-8000-123456789abc',
      payloadJson: '{"name":"Somchai"}',
      isOffloaded: false,
      offloadedKey: null,
    };
    const parsedInline = LogPayloadResultDtoSchema.parse(inline);
    expect(parsedInline.isOffloaded).toBe(false);
    expect(parsedInline.payloadJson).toBe('{"name":"Somchai"}');

    const offloaded = {
      logId: '01926b42-7c3a-7000-8000-123456789abc',
      payloadJson: '{"large":true}',
      isOffloaded: true,
      offloadedKey: 'audit/proj-1/2026/10/24/abc.json',
    };
    const parsedOffloaded = LogPayloadResultDtoSchema.parse(offloaded);
    expect(parsedOffloaded.isOffloaded).toBe(true);
    expect(parsedOffloaded.offloadedKey).toBe('audit/proj-1/2026/10/24/abc.json');
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
    expect(() =>
      loginRequestSchema.parse({ email: 'user@sammakorn.co.th', password: 'secret123' }),
    ).not.toThrow();
  });

  it.each([
    ['invalid email format', { email: 'not-an-email', password: 'secret' }],
    ['empty password', { email: 'user@example.com', password: '' }],
  ])('rejects invalid credentials (%s)', (_, payload) => {
    expect(() => loginRequestSchema.parse(payload)).toThrow();
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

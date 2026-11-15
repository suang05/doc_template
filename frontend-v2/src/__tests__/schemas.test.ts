import { describe, it, expect } from 'vitest';
import { ThaiTransformTypes } from '@/constants/domain';
import {
  CreateTemplateRequestSchema,
  SaveTemplateHtmlRequestSchema,
} from '@/features/templates/schemas/template.schema';
import { loginRequestSchema } from '@/features/auth/schemas/auth.schema';
import {
  createDataConnectionSchema,
  testDataConnectionSchema,
} from '@/features/datasources/schemas/dataconnection.schema';
import { CreateDatasetSchema } from '@/features/datasources/schemas/dataset.schema';
import { GenerateDocumentRequestSchema } from '@/features/generator/schemas/document.schema';
import { CreateApiKeyRequestSchema } from '@/features/apikeys/schemas/apikey.schema';
import { CreateDocumentJobFormSchema } from '@/features/jobs/schemas/job.schema';
import { InviteUserRequestSchema } from '@/features/users/schemas/users.schema';
import {
  createProjectSchema,
  grantBreakGlassSchema,
  parseAndValidateEgressCidrs,
  upsertProjectQuotaSchema,
} from '@/features/projects/schemas/project.schema';
import {
  jsonPointerToFieldKey,
  mapPointerErrors,
  parseRetryAfterSeconds,
} from '@/lib/api/problemDetails';
import { toFieldErrorMap } from '@/utils/zod';

// ── Domain SSoT: ThaiTransformTypes ─────────────────────────────────────────

describe('ThaiTransformTypes SSoT', () => {
  it('defines all 10 transformation options with non-empty Thai labels', () => {
    const values = ThaiTransformTypes.map((t) => t.value);
    expect(values).toEqual([
      'none',
      'thai_date',
      'thai_date_short',
      'thai_baht_text',
      'thai_currency',
      'thai_number',
      'thai_id_card',
      'thai_phone',
      'uppercase',
      'lowercase',
    ]);
    ThaiTransformTypes.forEach((t) => expect(t.label.trim().length > 0).toBe(true));
  });
});

// ── Shared Zod Helpers ──────────────────────────────────────────────────────

describe('toFieldErrorMap', () => {
  it('flattens Zod issues to first message per dot-path key', () => {
    const res = InviteUserRequestSchema.safeParse({
      email: 'bad-email',
      password: '123',
      firstName: '',
      lastName: '',
      role: 'Viewer',
    });
    expect(res.success).toBe(false);
    if (!res.success) {
      const map = toFieldErrorMap(res.error);
      expect(map.email).toBeDefined();
      expect(map.password).toBeDefined();
      expect(map.firstName).toBeDefined();
      expect(map.lastName).toBeDefined();
    }
  });
});

// ── Client Input & Form Validation Schemas ──────────────────────────────────

describe('CreateTemplateRequestSchema', () => {
  const valid = { name: 'Test', slug: 'test-template' };

  it.each([
    ['minimal valid', {}],
    ['with optional html', { html: '<!DOCTYPE html><html></html>' }],
    ['with explicit category', { category: 'invoice' }],
  ])('accepts valid variation (%s)', (_, override) => {
    expect(() => CreateTemplateRequestSchema.parse({ ...valid, ...override })).not.toThrow();
  });

  it('defaults category to contract when omitted', () => {
    expect(CreateTemplateRequestSchema.parse(valid).category).toBe('contract');
  });

  it.each([
    ['empty name', { name: '' }],
    ['empty slug', { slug: '' }],
    ['slug with uppercase', { slug: 'TestTemplate' }],
    ['slug with spaces and special chars', { slug: 'Test Template!' }],
    ['slug with underscores', { slug: 'test_template' }],
    ['slug shorter than 3 chars', { slug: 'ab' }],
  ])('rejects invalid variation (%s)', (_, override) => {
    expect(() => CreateTemplateRequestSchema.parse({ ...valid, ...override })).toThrow();
  });
});

describe('SaveTemplateHtmlRequestSchema', () => {
  it('parses valid HTML request and rejects too-short HTML', () => {
    expect(() =>
      SaveTemplateHtmlRequestSchema.parse({
        html: '<p>Hello World</p>',
        samplePayload: '{"name":"Alice"}',
        changeNote: 'Update layout',
      }),
    ).not.toThrow();
    expect(() => SaveTemplateHtmlRequestSchema.parse({ html: '<p>Hi</p>' })).toThrow();
  });
});

describe('GenerateDocumentRequestSchema', () => {
  it('accepts valid request and rejects unsupported outputFormat', () => {
    const parsed = GenerateDocumentRequestSchema.parse({
      payload: { contractNo: 'SMK-001', amount: 1000 },
      outputFormat: 'pdf',
      documentRef: 'DOC-001',
    });
    expect(parsed.outputFormat).toBe('pdf');
    expect(parsed.payload.amount).toBe(1000);

    expect(() =>
      GenerateDocumentRequestSchema.parse({ payload: {}, outputFormat: 'bmp' }),
    ).toThrow();
  });
});

describe('CreateApiKeyRequestSchema', () => {
  it('validates required trimmed fields', () => {
    expect(
      CreateApiKeyRequestSchema.parse({ name: 'Sales App Key', callerApp: 'sales-app' }).callerApp,
    ).toBe('sales-app');
    expect(() =>
      CreateApiKeyRequestSchema.parse({ name: '  ', callerApp: '' }),
    ).toThrow();
  });
});

describe('Datasource Form Schemas', () => {
  it('validates createDataConnectionSchema & testDataConnectionSchema', () => {
    expect(
      createDataConnectionSchema.parse({
        name: 'Main DB',
        provider: 'PostgreSQL',
        connectionString: 'Host=db;Port=5432;',
      }).connectionString,
    ).toBe('Host=db;Port=5432;');

    expect(() =>
      createDataConnectionSchema.parse({
        name: 'Main DB',
        provider: 'PostgreSQL',
        connectionString: '',
      }),
    ).toThrow();

    expect(
      testDataConnectionSchema.parse({
        provider: 'PostgreSQL',
        connectionString: 'Host=localhost;',
      }).provider,
    ).toBe('PostgreSQL');
  });

  it('rejects invalid UUID in CreateDatasetSchema', () => {
    expect(() =>
      CreateDatasetSchema.parse({
        name: 'Invalid Dataset',
        dataConnectionId: 'not-a-uuid',
        sqlQuery: 'SELECT 1',
      }),
    ).toThrow();
  });
});

describe('loginRequestSchema', () => {
  it('parses valid credentials and rejects malformed inputs', () => {
    expect(() =>
      loginRequestSchema.parse({ email: 'user@sammakorn.co.th', password: 'secret123' }),
    ).not.toThrow();
    expect(() =>
      loginRequestSchema.parse({ email: 'not-an-email', password: 'secret' }),
    ).toThrow();
    expect(() =>
      loginRequestSchema.parse({ email: 'user@example.com', password: '' }),
    ).toThrow();
  });
});

describe('Control Plane, Jobs, and RFC 9457 Helpers', () => {
  it('validates CreateDocumentJobFormSchema', () => {
    const parsed = CreateDocumentJobFormSchema.parse({
      slug: 'invoice-v2',
      outputFormat: 'pdf',
      maxAttempts: 3,
      webhookUrl: '',
    });
    expect(parsed.slug).toBe('invoice-v2');
    expect(() =>
      CreateDocumentJobFormSchema.parse({ slug: '', webhookUrl: 'not-a-url' }),
    ).toThrow();
  });

  it('validates Control Plane project, CIDR, quota, and break-glass schemas', () => {
    expect(
      createProjectSchema.parse({ name: 'Claims Portal', slug: 'claims-portal' }).slug,
    ).toBe('claims-portal');
    expect(() =>
      createProjectSchema.parse({ name: 'X', slug: 'Invalid Slug' }),
    ).toThrow();

    expect(parseAndValidateEgressCidrs('10.0.0.0/8\n192.168.1.0/24')).toEqual({
      cidrs: ['10.0.0.0/8', '192.168.1.0/24'],
    });
    expect(parseAndValidateEgressCidrs('not-a-cidr').error).toBeDefined();

    const quota = upsertProjectQuotaSchema.parse({
      maxRequestsPerMinute: '120',
      maxConcurrentJobs: '5',
      maxBatchItems: '100',
      maxStorageBytes: '1048576',
      maxActiveApiKeys: '2',
      isActive: true,
    });
    expect(quota.maxRequestsPerMinute).toBe(120);

    const grant = grantBreakGlassSchema.parse({
      targetProjectId: '123e4567-e89b-12d3-a456-426614174000',
      reason: 'Emergency incident diagnostics',
      durationMinutes: '60',
    });
    expect(grant.durationMinutes).toBe(60);
  });

  it('maps RFC 6901 JSON Pointers and parses Retry-After headers', () => {
    expect(jsonPointerToFieldKey('/customer/tax~1id')).toBe('customer.tax/id');
    expect(
      mapPointerErrors([
        { pointer: '/payload/amount', code: 'MIN', message: 'Must be >= 1' },
      ]),
    ).toEqual({ 'payload.amount': 'Must be >= 1' });
    expect(parseRetryAfterSeconds('15')).toBe(15);
  });
});

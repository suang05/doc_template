import { describe, it, expect } from 'vitest';
import {
  GenerateDocumentRequestSchema,
  CreateTemplateRequestSchema,
  SaveTemplateHtmlRequestSchema,
  TemplateStudioDtoSchema,
  TemplateSchemaDtoSchema,
  SaveFieldMappingItemSchema,
  CreateApiKeyRequestSchema,
} from '../types/api';

describe('Zod Schemas Validation Integrity', () => {
  it('should accept valid GenerateDocumentRequest', () => {
    const valid = {
      data: { contractNo: 'SMK-001', amount: 1000 },
      output: 'pdf',
      documentRef: 'DOC-001',
    };
    const parsed = GenerateDocumentRequestSchema.parse(valid);
    expect(parsed.output).toBe('pdf');
    expect(parsed.data.amount).toBe(1000);
  });

  it('should reject invalid output format in GenerateDocumentRequest', () => {
    const invalid = {
      data: {},
      output: 'bmp', // invalid
    };
    expect(() => GenerateDocumentRequestSchema.parse(invalid)).toThrow();
  });

  it('should enforce slug format in CreateTemplateRequestSchema', () => {
    const valid = {
      name: 'Sales Contract',
      slug: 'sales-contract-2026',
    };
    expect(CreateTemplateRequestSchema.parse(valid).slug).toBe('sales-contract-2026');

    const invalid = {
      name: 'Sales Contract',
      slug: 'Sales Contract With Spaces!', // invalid
    };
    expect(() => CreateTemplateRequestSchema.parse(invalid)).toThrow();
  });

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

  it('should validate SaveTemplateHtmlRequest with optional samplePayload', () => {
    const valid = {
      html: '<h1>Hello World</h1>',
      samplePayload: '{"name":"Sammakorn"}',
      changeNote: 'Initial draft',
    };
    const parsed = SaveTemplateHtmlRequestSchema.parse(valid);
    expect(parsed.html).toBe('<h1>Hello World</h1>');
    expect(parsed.samplePayload).toBe('{"name":"Sammakorn"}');
  });

  it('should validate TemplateStudioDto and TemplateSchemaDto', () => {
    const studio = {
      html: '<h1>Template</h1>',
      samplePayload: '{"amount":100}',
      dataSchema: '{"type":"object"}',
      version: 2,
    };
    const parsedStudio = TemplateStudioDtoSchema.parse(studio);
    expect(parsedStudio.version).toBe(2);

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


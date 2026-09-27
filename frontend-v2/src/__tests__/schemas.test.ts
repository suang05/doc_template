import { describe, it, expect } from 'vitest';
import {
  TemplateDtoSchema,
  CreateTemplateRequestSchema,
  SaveTemplateHtmlRequestSchema,
  TemplateVersionDtoSchema,
  TemplateValidationResultSchema,
} from '@/schemas/template.schema';
import { loginRequestSchema, loginResponseSchema } from '@/schemas/auth.schema';

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

  it('accepts slug with hyphens and underscores', () => {
    expect(() => TemplateDtoSchema.parse({ ...valid, slug: 'my-template_v2' })).not.toThrow();
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

  it('rejects slug with invalid characters', () => {
    expect(() => CreateTemplateRequestSchema.parse({ name: 'Test', slug: 'Test Template!' })).toThrow();
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

// ── loginRequestSchema ──────────────────────────────────────────────────────

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

// ── loginResponseSchema ─────────────────────────────────────────────────────

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

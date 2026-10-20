import { describe, it, expect } from 'vitest';
import { extractVariablesFromHtml } from '@/lib/html/variableExtractor';

describe('extractVariablesFromHtml', () => {
  it('returns same reference when html has no variables', () => {
    const data = { name: 'Alice' };
    const result = extractVariablesFromHtml('<p>No placeholders</p>', data);
    expect(result).toBe(data);
  });

  it.each([
    [
      'adds missing scalar variable with sample string',
      '<p>{{customerName}}</p>',
      {},
      'customerName',
      (val: unknown) => typeof val === 'string',
    ],
    [
      'preserves existing scalar value without overwriting',
      '<p>{{customerName}}</p>',
      { customerName: 'Alice' },
      'customerName',
      (val: unknown) => val === 'Alice',
    ],
    [
      'generates numeric sample for amount field',
      '<p>{{amount}}</p>',
      {},
      'amount',
      (val: unknown) => typeof val === 'number',
    ],
    [
      'generates numeric sample for total/price field',
      '<p>{{totalPrice}}</p>',
      {},
      'totalPrice',
      (val: unknown) => typeof val === 'number',
    ],
    [
      'generates date string for date fields (YYYY-MM-DD)',
      '<p>{{contractDate}}</p>',
      {},
      'contractDate',
      (val: unknown) => typeof val === 'string' && /\d{4}-\d{2}-\d{2}/.test(val),
    ],
    [
      'generates valid tax ID string for tax_id fields',
      '<p>{{tax_id}}</p>',
      {},
      'tax_id',
      (val: unknown) => val === '0107536000123',
    ],
    [
      'generates email string with @ for email fields',
      '<p>{{contactEmail}}</p>',
      {},
      'contactEmail',
      (val: unknown) => typeof val === 'string' && val.includes('@'),
    ],
  ])('%s', (_, html, initialData, key, predicate) => {
    const result = extractVariablesFromHtml(html, initialData);
    expect(predicate(result[key])).toBe(true);
  });

  it('extracts array from #each block and generates 2 sample rows', () => {
    const html = `<table>{{#each items}}<tr><td>{{no}}</td><td>{{description}}</td></tr>{{/each}}</table>`;
    const result = extractVariablesFromHtml(html, {});
    expect(Array.isArray(result['items'])).toBe(true);
    expect((result['items'] as unknown[]).length).toBe(2);
  });

  it('does not replace existing array that already has items', () => {
    const existing = { items: [{ no: '1', description: 'Existing' }] };
    const html = `{{#each items}}<td>{{no}}</td>{{/each}}`;
    const result = extractVariablesFromHtml(html, existing);
    expect(result['items']).toBe(existing['items']);
  });

  it('extracts nested object variable (customer.name)', () => {
    const result = extractVariablesFromHtml('<p>{{customer.name}}</p>', {});
    expect(typeof result['customer']).toBe('object');
    expect((result['customer'] as Record<string, unknown>)['name']).toBeDefined();
  });

  it('does not overwrite existing nested object field', () => {
    const data = { customer: { name: 'Alice' } };
    const result = extractVariablesFromHtml('<p>{{customer.name}}</p>', data);
    expect((result['customer'] as Record<string, unknown>)['name']).toBe('Alice');
  });

  it('skips helper keywords (addOne, inc, @index, this, else, if)', () => {
    const html = '{{#each items}}{{addOne @index}}{{this}}{{else}}{{/each}}';
    const result = extractVariablesFromHtml(html, {});
    expect(result).not.toHaveProperty('addOne');
    expect(result).not.toHaveProperty('else');
    expect(result).not.toHaveProperty('this');
  });

  it('handles {{qr:field}} and {{barcode:field}} prefixes', () => {
    const result = extractVariablesFromHtml('{{qr:trackingUrl}}{{barcode:code}}', {});
    expect(result).toHaveProperty('trackingUrl');
    expect(result).toHaveProperty('code');
  });

  it('handles transform suffix syntax {{field:thai_baht_text}}', () => {
    const result = extractVariablesFromHtml('{{amount:thai_baht_text}}', {});
    expect(result).toHaveProperty('amount');
  });
});

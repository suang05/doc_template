import { describe, it, expect } from 'vitest';
import { extractVariablesFromHtml } from '@/lib/html/variableExtractor';

describe('extractVariablesFromHtml', () => {
  it('returns same reference when html has no variables', () => {
    const data = { name: 'Alice' };
    const result = extractVariablesFromHtml('<p>No placeholders</p>', data);
    expect(result).toBe(data); // same reference = no change
  });

  it('adds missing scalar variable with sample value', () => {
    const result = extractVariablesFromHtml('<p>{{customerName}}</p>', {});
    expect(result).toHaveProperty('customerName');
    expect(typeof result['customerName']).toBe('string');
  });

  it('does not overwrite existing scalar value', () => {
    const data = { customerName: 'Alice' };
    const result = extractVariablesFromHtml('<p>{{customerName}}</p>', data);
    expect(result['customerName']).toBe('Alice');
  });

  it('generates numeric sample for amount/price/total fields', () => {
    const result = extractVariablesFromHtml('<p>{{amount}}</p>', {});
    expect(typeof result['amount']).toBe('number');
    const result2 = extractVariablesFromHtml('<p>{{totalPrice}}</p>', {});
    expect(typeof result2['totalPrice']).toBe('number');
  });

  it('generates date string for date fields', () => {
    const result = extractVariablesFromHtml('<p>{{contractDate}}</p>', {});
    expect(String(result['contractDate'])).toMatch(/\d{4}-\d{2}-\d{2}/);
  });

  it('generates tax ID string for tax_id fields', () => {
    const result = extractVariablesFromHtml('<p>{{tax_id}}</p>', {});
    expect(result['tax_id']).toBe('0107536000123');
  });

  it('generates email string for email fields', () => {
    const result = extractVariablesFromHtml('<p>{{contactEmail}}</p>', {});
    expect(String(result['contactEmail'])).toContain('@');
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
    expect(result['items']).toBe(existing['items']); // same reference
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

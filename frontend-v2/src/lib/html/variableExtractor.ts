/**
 * Extracts Handlebars variable references from an HTML template string
 * and generates sample values for any key not already present in existingData.
 *
 * Pure function — no side effects, fully testable.
 */
export function extractVariablesFromHtml(
  html: string,
  existingData: Record<string, unknown>,
): Record<string, unknown> {
  const result: Record<string, unknown> = { ...existingData };
  const arrayKeys = new Set<string>();
  let changed = false;

  // 1. Detect arrays from {{#each key}}
  const eachRegex = /{{#each\s+([a-zA-Z0-9_.]+)\s*}}([\s\S]*?){{\/each}}/g;
  let eachMatch: RegExpExecArray | null;
  while ((eachMatch = eachRegex.exec(html)) !== null) {
    const arrayKey = eachMatch[1];
    const innerContent = eachMatch[2];
    arrayKeys.add(arrayKey);

    if (!Array.isArray(result[arrayKey]) || (result[arrayKey] as unknown[]).length === 0) {
      const innerFieldRegex = /{{\s*([a-zA-Z0-9_.]+)(?::\w+)?\s*}}/g;
      let innerFieldMatch: RegExpExecArray | null;
      const innerItem: Record<string, unknown> = {};

      while ((innerFieldMatch = innerFieldRegex.exec(innerContent)) !== null) {
        const f = innerFieldMatch[1];
        if (['addOne', 'inc', '@index', 'this', 'else', 'if'].includes(f)) continue;
        innerItem[f] = sampleValue(f);
      }

      if (Object.keys(innerItem).length === 0) innerItem['name'] = 'ตัวอย่างรายการที่ 1';

      const innerItem2 = { ...innerItem };
      if (typeof innerItem2['no'] === 'string') innerItem2['no'] = '2';
      if (typeof innerItem2['amount'] === 'number') innerItem2['amount'] = 2500.0;

      result[arrayKey] = [innerItem, innerItem2];
      changed = true;
    }
  }

  // 2. Detect scalar variables and nested objects (e.g. customer.name)
  const varRegex = /{{\s*(?:qr:|barcode:|image:)?([a-zA-Z0-9_.]+)(?::\w+)?\s*}}/g;
  let varMatch: RegExpExecArray | null;
  while ((varMatch = varRegex.exec(html)) !== null) {
    const v = varMatch[1];
    if (v.startsWith('#') || v.startsWith('/') || v.startsWith('@') || v.startsWith('^')) continue;
    if (['addOne', 'inc', 'else', 'this', 'if', 'each', 'ifEquals'].includes(v)) continue;
    if (arrayKeys.has(v)) continue;

    if (v.includes('.')) {
      const parts = v.split('.');
      const objKey = parts[0];
      const fieldKey = parts.slice(1).join('.');

      if (!result[objKey] || typeof result[objKey] !== 'object' || Array.isArray(result[objKey])) {
        result[objKey] = {};
      }
      const obj = result[objKey] as Record<string, unknown>;
      if (obj[fieldKey] === undefined) {
        obj[fieldKey] = sampleValue(fieldKey);
        changed = true;
      }
    } else {
      if (result[v] === undefined) {
        result[v] = sampleValue(v);
        changed = true;
      }
    }
  }

  return changed ? result : existingData;
}

function sampleValue(key: string): unknown {
  const lk = key.toLowerCase();
  if (lk.includes('tax_id') || lk.includes('citizen_id') || lk.includes('idcard')) return '0107536000123';
  if (lk.includes('email')) return 'contact@sammakorn.co.th';
  if (lk.includes('amount') || lk.includes('total') || lk.includes('price')) return 250000.0;
  if (lk.includes('date')) return '2026-09-20';
  if (lk.includes('no') && lk.length <= 4) return '1';
  return `ตัวอย่าง ${key}`;
}

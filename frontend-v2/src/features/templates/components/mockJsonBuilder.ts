/**
 * Converts a flat placeholder list into a nested mock JSON string.
 * Array detection: a root key with 2+ child sub-keys AND indexed notation (items.0.x / items[0].x)
 * is rendered as an array of objects. Nested objects without index are plain objects.
 */

type JsonNode = Record<string, unknown>;

function mockValue(key: string): unknown {
  const k = key.toLowerCase();
  if (/price|amount|total|qty|quantity|count|num/.test(k)) return 100;
  if (/date|วันที่/.test(k)) return '2024-01-01';
  if (/phone|tel|โทร/.test(k)) return '02-000-0000';
  if (/email/.test(k)) return 'test@example.com';
  if (/no$|number$|id$|ref/.test(k)) return 'DOC-001';
  if (/address|ที่อยู่/.test(k)) return '123 ถ.ทดสอบ กรุงเทพฯ 10000';
  if (/name|ชื่อ/.test(k)) return 'บริษัท ทดสอบ จำกัด';
  return 'ทดสอบ';
}

// Normalise "items[0].name" → "items.0.name"
function normalisePath(raw: string): string {
  return raw.replace(/\[(\d+)\]/g, '.$1');
}

export function buildMockJson(placeholders: string[]): string {
  const root: JsonNode = {};

  for (const ph of placeholders) {
    const parts = normalisePath(ph).split('.');
    let node: JsonNode = root;

    for (let i = 0; i < parts.length; i++) {
      const part = parts[i];
      const isLast = i === parts.length - 1;
      const nextIsIndex = !isLast && /^\d+$/.test(parts[i + 1]);

      if (isLast) {
        if (!(part in node)) node[part] = mockValue(part);
      } else if (nextIsIndex) {
        // Current part is an array root
        if (!Array.isArray(node[part])) node[part] = [];
        const arr = node[part] as JsonNode[];
        const idx = Number(parts[i + 1]);
        while (arr.length <= idx) arr.push({});
        node = arr[idx];
        i++; // skip the index part
      } else {
        if (typeof node[part] !== 'object' || Array.isArray(node[part])) node[part] = {};
        node = node[part] as JsonNode;
      }
    }
  }

  return JSON.stringify(root, null, 2);
}

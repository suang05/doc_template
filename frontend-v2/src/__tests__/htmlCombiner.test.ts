import { describe, it, expect } from 'vitest';
import { parseCombinedHtml, getCombinedHtml } from '@/hooks/useTemplateStudio';

// ── parseCombinedHtml ───────────────────────────────────────────────────────

describe('parseCombinedHtml', () => {
  it('returns full content as main when no template tags present', () => {
    const html = '<html><body><h1>Hello</h1></body></html>';
    const { main, header, footer } = parseCombinedHtml(html);
    expect(main).toBe(html.trim());
    expect(header).toBe('');
    expect(footer).toBe('');
  });

  it('extracts header template inner content', () => {
    const html = '<body>Main</body>\n<template id="header"><p>Header</p></template>';
    const { main, header, footer } = parseCombinedHtml(html);
    expect(header).toBe('<p>Header</p>');
    expect(footer).toBe('');
    expect(main).not.toContain('<template');
  });

  it('extracts footer template inner content', () => {
    const html = '<body>Main</body>\n<template id="footer"><p>Footer</p></template>';
    const { main, header, footer } = parseCombinedHtml(html);
    expect(footer).toBe('<p>Footer</p>');
    expect(header).toBe('');
    expect(main).not.toContain('<template');
  });

  it('extracts both header and footer, leaving clean main', () => {
    const html = `<body>Content</body>
<template id="header"><h1>Top</h1></template>
<template id="footer"><h1>Bottom</h1></template>`;
    const { main, header, footer } = parseCombinedHtml(html);
    expect(header).toBe('<h1>Top</h1>');
    expect(footer).toBe('<h1>Bottom</h1>');
    expect(main).toContain('<body>Content</body>');
    expect(main).not.toContain('<template');
  });

  it('trims whitespace from main and inner content', () => {
    const html = '  <body>X</body>  \n<template id="header">\n  <p>H</p>\n</template>';
    const { main, header } = parseCombinedHtml(html);
    expect(main).toBe('<body>X</body>');
    expect(header).toBe('<p>H</p>');
  });

  it('handles empty string without throwing', () => {
    const { main, header, footer } = parseCombinedHtml('');
    expect(main).toBe('');
    expect(header).toBe('');
    expect(footer).toBe('');
  });

  it('is case-insensitive for template tag', () => {
    const html = '<body>X</body><TEMPLATE ID="header"><p>H</p></TEMPLATE>';
    const { header } = parseCombinedHtml(html);
    expect(header).toBe('<p>H</p>');
  });
});

// ── getCombinedHtml ─────────────────────────────────────────────────────────

describe('getCombinedHtml', () => {
  it('returns main only when header and footer are empty', () => {
    const result = getCombinedHtml('<body>X</body>', '', '');
    expect(result).toBe('<body>X</body>');
  });

  it('appends header template when header is non-empty', () => {
    const result = getCombinedHtml('<body>X</body>', '<p>H</p>', '');
    expect(result).toContain('<template id="header">');
    expect(result).toContain('<p>H</p>');
    expect(result).not.toContain('footer');
  });

  it('appends footer template when footer is non-empty', () => {
    const result = getCombinedHtml('<body>X</body>', '', '<p>F</p>');
    expect(result).toContain('<template id="footer">');
    expect(result).toContain('<p>F</p>');
    expect(result).not.toContain('header');
  });

  it('appends both when both are non-empty', () => {
    const result = getCombinedHtml('<body>X</body>', '<p>H</p>', '<p>F</p>');
    expect(result).toContain('<template id="header">');
    expect(result).toContain('<template id="footer">');
  });

  it('does not append header template when header is only whitespace', () => {
    const result = getCombinedHtml('<body>X</body>', '   ', '');
    expect(result).not.toContain('<template');
  });
});

// ── Round-trip idempotency ──────────────────────────────────────────────────

describe('parseCombinedHtml + getCombinedHtml round-trip', () => {
  it('is idempotent: parse → combine → parse → combine produces same result', () => {
    const original = `<!DOCTYPE html>
<html><body><h1>Doc</h1></body></html>
<template id="header">
<p>Header</p>
</template>
<template id="footer">
<p>Footer</p>
</template>`;

    const { main: m1, header: h1, footer: f1 } = parseCombinedHtml(original);
    const combined1 = getCombinedHtml(m1, h1, f1);

    const { main: m2, header: h2, footer: f2 } = parseCombinedHtml(combined1);
    const combined2 = getCombinedHtml(m2, h2, f2);

    expect(combined2).toBe(combined1);
  });

  it('isDirty stays false after load: normalizedHtml equals getCombinedHtml of parsed parts', () => {
    const raw = `<!DOCTYPE html><html><body><p>{{name}}</p></body></html>
<template id="header"><div>{{companyName}}</div></template>`;
    const { main, header, footer } = parseCombinedHtml(raw);
    const normalized = getCombinedHtml(main, header, footer);
    // Second pass must produce the same string (no false isDirty on re-render)
    const { main: m2, header: h2, footer: f2 } = parseCombinedHtml(normalized);
    expect(getCombinedHtml(m2, h2, f2)).toBe(normalized);
  });
});

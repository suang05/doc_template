import { describe, it, expect } from 'vitest';
import { parseCombinedHtml, getCombinedHtml } from '@/features/studio/hooks/useTemplateStudio';

// ── parseCombinedHtml ───────────────────────────────────────────────────────

describe('parseCombinedHtml', () => {
  it.each([
    [
      'no template tags',
      '<html><body><h1>Hello</h1></body></html>',
      { main: '<html><body><h1>Hello</h1></body></html>', header: '', footer: '' },
    ],
    [
      'header template only',
      '<body>Main</body>\n<template id="header"><p>Header</p></template>',
      { main: '<body>Main</body>', header: '<p>Header</p>', footer: '' },
    ],
    [
      'footer template only',
      '<body>Main</body>\n<template id="footer"><p>Footer</p></template>',
      { main: '<body>Main</body>', header: '', footer: '<p>Footer</p>' },
    ],
    [
      'both header and footer',
      '<body>Content</body>\n<template id="header"><h1>Top</h1></template>\n<template id="footer"><h1>Bottom</h1></template>',
      { main: '<body>Content</body>', header: '<h1>Top</h1>', footer: '<h1>Bottom</h1>' },
    ],
    [
      'whitespace trimming',
      '  <body>X</body>  \n<template id="header">\n  <p>H</p>\n</template>',
      { main: '<body>X</body>', header: '<p>H</p>', footer: '' },
    ],
    [
      'empty string input',
      '',
      { main: '', header: '', footer: '' },
    ],
    [
      'case-insensitive TEMPLATE tag',
      '<body>X</body><TEMPLATE ID="header"><p>H</p></TEMPLATE>',
      { main: '<body>X</body>', header: '<p>H</p>', footer: '' },
    ],
  ])('correctly parses %s', (_, html, expected) => {
    expect(parseCombinedHtml(html)).toEqual(expected);
  });
});

// ── getCombinedHtml ─────────────────────────────────────────────────────────

describe('getCombinedHtml', () => {
  it.each([
    [
      'main only when header and footer empty',
      '<body>X</body>', '', '',
      '<body>X</body>',
    ],
    [
      'appends header template when non-empty',
      '<body>X</body>', '<p>H</p>', '',
      '<body>X</body>\n<template id="header">\n<p>H</p>\n</template>',
    ],
    [
      'appends footer template when non-empty',
      '<body>X</body>', '', '<p>F</p>',
      '<body>X</body>\n<template id="footer">\n<p>F</p>\n</template>',
    ],
    [
      'appends both header and footer',
      '<body>X</body>', '<p>H</p>', '<p>F</p>',
      '<body>X</body>\n<template id="header">\n<p>H</p>\n</template>\n<template id="footer">\n<p>F</p>\n</template>',
    ],
    [
      'ignores whitespace-only header',
      '<body>X</body>', '   ', '',
      '<body>X</body>',
    ],
  ])('%s', (_, main, header, footer, expected) => {
    expect(getCombinedHtml(main, header, footer)).toBe(expected);
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
});

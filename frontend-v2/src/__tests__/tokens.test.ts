import { describe, it, expect } from 'vitest';
import { tokens } from '@/tokens';

describe('SSoT Design Tokens Integrity', () => {
  it('should enforce strict 2px - 4px border radius', () => {
    expect(tokens.radius.sm).toBe('2px');
    expect(tokens.radius.md).toBe('4px');
  });

  it('should have ice-white canvas and no dark navy background', () => {
    expect(tokens.colors.canvas).toBe('#f8fbfe');
    expect(tokens.colors.primary).toBe('#0284c7');
    const allColors = Object.values(tokens.colors);
    expect(allColors).not.toContain('#002D62');
  });

  it.each(['contract', 'financial', 'official', 'hr', 'operations'] as const)(
    'defines functional category accents for %s',
    (category) => {
      const entry = tokens.categories[category];
      expect(entry).toEqual({
        id: category,
        label: expect.any(String),
        primary: expect.any(String),
        light: expect.any(String),
        border: expect.any(String),
        text: expect.any(String),
      });
    },
  );

  it.each(['pdf', 'docx', 'xlsx', 'html'] as const)(
    'defines document output format %s',
    (format) => {
      expect(tokens.formats[format]).toBeDefined();
      expect(tokens.formats[format].primary).toBeDefined();
    },
  );
});

import { describe, it, expect } from 'vitest';
import { tokens } from '../tokens';

describe('SSoT Design Tokens Integrity', () => {
  it('should enforce strict 2px - 4px border radius', () => {
    expect(tokens.radius.sm).toBe('2px');
    expect(tokens.radius.md).toBe('4px');
  });

  it('should have ice-white canvas and no dark navy background', () => {
    expect(tokens.colors.canvas).toBe('#f8fbfe');
    expect(tokens.colors.primary).toBe('#0284c7');
    // Ensure no dark navy #002D62 or similar
    const allColors = Object.values(tokens.colors);
    expect(allColors).not.toContain('#002D62');
  });

  it('should define all 5 functional category accents correctly', () => {
    const requiredCategories = ['contract', 'financial', 'official', 'hr', 'operations'];
    for (const cat of requiredCategories) {
      expect(tokens.categories).toHaveProperty(cat);
      const entry = (tokens.categories as any)[cat];
      expect(entry).toHaveProperty('primary');
      expect(entry).toHaveProperty('light');
      expect(entry).toHaveProperty('border');
      expect(entry).toHaveProperty('text');
    }
  });

  it('should define 4 document output formats', () => {
    expect(tokens.formats).toHaveProperty('pdf');
    expect(tokens.formats).toHaveProperty('docx');
    expect(tokens.formats).toHaveProperty('xlsx');
    expect(tokens.formats).toHaveProperty('html');
  });
});

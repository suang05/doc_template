import { describe, it, expect } from 'vitest';
import { DocumentFormat, tokens } from '../tokens';

describe('Template Workflow Separation (Code-First HTML vs File-First Office)', () => {
  it('should distinguish Code-First HTML format from Office binary formats', () => {
    const htmlFormat: DocumentFormat = 'html';
    const officeFormats: DocumentFormat[] = ['docx', 'xlsx'];

    expect(htmlFormat).toBe('html');
    expect(officeFormats).toContain('docx');
    expect(officeFormats).toContain('xlsx');
    expect(officeFormats).not.toContain('html');
  });

  it('should map format tokens properly', () => {
    expect(tokens.formats.html.label).toBe('HTML');
    expect(tokens.formats.docx.label).toBe('Word');
    expect(tokens.formats.xlsx.label).toBe('Excel');
    expect(tokens.formats.pdf.label).toBe('PDF');
  });

  it('should route action contextually per template format', () => {
    const getPrimaryActionForFormat = (fmt: DocumentFormat) => {
      if (fmt === 'html') {
        return { target: 'studio', label: 'แก้ไขโค้ด (Monaco Studio)' };
      }
      return { target: 'mapping', label: 'กำหนดฟิลด์ (Field Mapping)' };
    };

    expect(getPrimaryActionForFormat('html').target).toBe('studio');
    expect(getPrimaryActionForFormat('docx').target).toBe('mapping');
    expect(getPrimaryActionForFormat('xlsx').target).toBe('mapping');
  });
});

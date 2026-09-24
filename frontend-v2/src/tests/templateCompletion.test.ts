import { describe, it, expect, vi } from 'vitest';
import { registerTemplateCompletion } from '../lib/monaco/templateCompletion';

describe('registerTemplateCompletion', () => {
  it('should register completion provider on monaco.languages for html', () => {
    let registeredLanguage = '';
    let registeredProvider: any = null;

    const mockMonaco = {
      languages: {
        CompletionItemKind: {
          Snippet: 27,
          Function: 1,
          Field: 3,
          Property: 9,
        },
        CompletionItemInsertTextRule: {
          InsertAsSnippet: 4,
        },
        registerCompletionItemProvider: vi.fn((lang, provider) => {
          registeredLanguage = lang;
          registeredProvider = provider;
          return { dispose: vi.fn() };
        }),
      },
    };

    const disposable = registerTemplateCompletion(mockMonaco, {
      getAvailableFields: () => ['customerName', 'totalAmount'],
    });

    expect(mockMonaco.languages.registerCompletionItemProvider).toHaveBeenCalledTimes(1);
    expect(registeredLanguage).toBe('html');
    expect(registeredProvider).toBeDefined();
    expect(registeredProvider.triggerCharacters).toContain('{');
    expect(registeredProvider.triggerCharacters).toContain('#');
    expect(registeredProvider.triggerCharacters).toContain(':');

    // Simulate requesting suggestions
    const mockModel = {
      getValueInRange: () => '{{',
      getWordUntilPosition: () => ({ startColumn: 1, endColumn: 3 }),
    };
    const mockPosition = { lineNumber: 1, column: 3 };

    const result = registeredProvider.provideCompletionItems(mockModel, mockPosition);
    expect(result.suggestions).toBeDefined();

    const labels = result.suggestions.map((s: any) => s.label);
    expect(labels).toContain('{{#each items}}');
    expect(labels).toContain('{{#if condition}}');
    expect(labels).toContain('{{#ifEquals val1 val2}}');
    expect(labels).toContain('{{addOne @index}}');
    expect(labels).toContain('template-header');
    expect(labels).toContain('template-footer');
    expect(labels).toContain('page-break');
    expect(labels).toContain('{{customerName}}');
    expect(labels).toContain('{{totalAmount:number}}');

    // Test disposal
    disposable.dispose();
  });
});

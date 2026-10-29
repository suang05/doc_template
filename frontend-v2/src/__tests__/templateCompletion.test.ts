import { describe, it, expect, vi } from 'vitest';
import { registerTemplateCompletion } from '@/features/studio/domain/templateCompletion';

describe('registerTemplateCompletion', () => {
  it('handles null or invalid monaco instance gracefully', () => {
    const disposable = registerTemplateCompletion(null);
    expect(disposable).toBeDefined();
    expect(typeof disposable.dispose).toBe('function');
    expect(() => disposable.dispose()).not.toThrow();
  });

  it('registers completion provider on monaco.languages for html and provides completions', () => {
    let registeredLanguage = '';
    let registeredProvider: {
      triggerCharacters: string[];
      provideCompletionItems: (
        model: { getValueInRange: () => string; getWordUntilPosition: () => { startColumn: number; endColumn: number } },
        position: { lineNumber: number; column: number },
      ) => { suggestions: Array<{ label: string; insertText: string }> };
    } | null = null;

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
    expect(registeredProvider!.triggerCharacters).toEqual(
      expect.arrayContaining(['{', ':', '#']),
    );

    // 1. Simulate typing '{{' placeholder trigger
    const mockModel = {
      getValueInRange: () => '{{',
      getWordUntilPosition: () => ({ startColumn: 1, endColumn: 3 }),
    };
    const mockPosition = { lineNumber: 1, column: 3 };

    const result = registeredProvider!.provideCompletionItems(mockModel, mockPosition);
    expect(result.suggestions).toBeDefined();

    const labels = result.suggestions.map((s) => s.label);
    expect(labels).toEqual(
      expect.arrayContaining([
        '{{#each items}}',
        '{{#if condition}}',
        '{{#ifEquals val1 val2}}',
        '{{addOne @index}}',
        'template-header',
        'template-footer',
        'page-break',
        '{{customerName}}',
        '{{totalAmount:number}}',
      ]),
    );

    // 2. Simulate typing ':' transform trigger (e.g. '{{amount:')
    const colonModel = {
      getValueInRange: () => '{{amount:',
      getWordUntilPosition: () => ({ startColumn: 10, endColumn: 10 }),
    };
    const colonPosition = { lineNumber: 1, column: 10 };
    const colonResult = registeredProvider!.provideCompletionItems(colonModel, colonPosition);
    const bahtSuggestion = colonResult.suggestions.find((s) => s.label === ':thai_baht_text');
    expect(bahtSuggestion).toBeDefined();
    expect(bahtSuggestion?.insertText).toBe('thai_baht_text}}');

    // Test disposal
    disposable.dispose();
  });
});

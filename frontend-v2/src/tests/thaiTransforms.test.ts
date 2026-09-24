import { describe, it, expect } from 'vitest';
import { ThaiTransformTypes } from '../schemas/mapping.schema';

describe('Thai Data Transform Types Compatibility', () => {
  it('should include all required 7 transformation functions', () => {
    const values = ThaiTransformTypes.map((t) => t.value);
    expect(values).toContain('thai_baht_text');
    expect(values).toContain('thai_date');
    expect(values).toContain('thai_currency');
    expect(values).toContain('thai_id_card');
    expect(values).toContain('thai_phone');
    expect(values).toContain('uppercase');
    expect(values).toContain('lowercase');
  });

  it('should provide Thai label and example for each transform type', () => {
    for (const item of ThaiTransformTypes) {
      expect(item.label.length).toBeGreaterThan(0);
      expect(item.example.length).toBeGreaterThan(0);
    }
  });
});

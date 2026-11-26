import { TestBed } from '@angular/core/testing';
import { VintageSizeConverterComponent } from './vintage-size-converter.component';

describe('VintageSizeConverterComponent', () => {
  function convert(ptp: number, waist: number, length = 45) {
    const c = TestBed.createComponent(VintageSizeConverterComponent).componentInstance;
    c.calculate(String(ptp), String(waist), String(length));
    return c.result()!;
  }

  // Pit to pit, waist → expected size and whether it warns (from the sizing brief).
  const cases: [number, number, string, boolean][] = [
    [17, 13, 'UK 8–10', true],
    [17.5, 14, 'UK 8–10', false],
    [18.5, 15, 'UK 10–12', false],
    [19.5, 15, 'UK 12–14', true],
    [21.5, 18.5, 'UK 16–18', false],
    [22.5, 19, 'UK 18–20', false],
  ];

  for (const [ptp, waist, size, warns] of cases) {
    it(`gives ${size} for ${ptp}" pit to pit and a ${waist}" waist${warns ? ', with a warning' : ''}`, () => {
      const r = convert(ptp, waist);
      expect(r.ukSize).toBe(size);
      expect(r.warning !== null).toBe(warns);
    });
  }

  it('does not warn when the waist is bigger than the chest', () => {
    expect(convert(17, 16).warning).toBeNull();
  });

  it('describes every length, including knee length', () => {
    expect(convert(18, 14, 36).note).toContain('around knee length');
    expect(convert(18, 14, 45).note).toContain('a midi');
    expect(convert(18, 14, 52).note).toContain('ankle to floor');
    expect(convert(18, 14, 30).note).toContain('mini or short');
  });
});

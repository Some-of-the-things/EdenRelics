import { TestBed } from '@angular/core/testing';
import { DOCUMENT } from '@angular/common';
import { BrandingService } from './branding.service';

/**
 * The compiled-in palette is written onto documentElement as inline custom properties, and an
 * inline style beats :root — so whatever BrandingService applies is what every visitor sees,
 * regardless of what styles.scss says.
 *
 * That is exactly how six WCAG AA failures reached production: the pre-rebrand coral values
 * shipped in the constant and silently overrode the palette every component was contrast-checked
 * against. Nothing failed, because nothing was checking. These tests check.
 */

function parseHex(hex: string): { r: number; g: number; b: number } {
  const h = hex.replace('#', '');
  const full = h.length === 3 ? h.split('').map((c) => c + c).join('') : h;
  return {
    r: parseInt(full.slice(0, 2), 16),
    g: parseInt(full.slice(2, 4), 16),
    b: parseInt(full.slice(4, 6), 16),
  };
}

function relativeLuminance(hex: string): number {
  const { r, g, b } = parseHex(hex);
  const channel = (v: number): number => {
    const s = v / 255;
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}

function contrast(a: string, b: string): number {
  const [hi, lo] = [relativeLuminance(a), relativeLuminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

describe('BrandingService palette', () => {
  let applied: Record<string, string>;

  beforeEach(async () => {
    TestBed.configureTestingModule({});
    const doc = TestBed.inject(DOCUMENT);
    const service = TestBed.inject(BrandingService);
    await service.load();
    const style = doc.documentElement.style;
    applied = {
      bgPrimary: style.getPropertyValue('--bg-primary').trim(),
      bgSecondary: style.getPropertyValue('--bg-secondary').trim(),
      bgCard: style.getPropertyValue('--bg-card').trim(),
      bgDark: style.getPropertyValue('--bg-dark').trim(),
      textPrimary: style.getPropertyValue('--text-primary').trim(),
      textSecondary: style.getPropertyValue('--text-secondary').trim(),
      textMuted: style.getPropertyValue('--text-muted').trim(),
      textInverse: style.getPropertyValue('--text-inverse').trim(),
      accent: style.getPropertyValue('--accent').trim(),
    };
  });

  it('applies a full palette to the document element', () => {
    for (const [name, value] of Object.entries(applied)) {
      expect(value, `${name} was not applied`).toMatch(/^#[0-9a-fA-F]{3,8}$/);
    }
  });

  // Every pair below is a combination the site actually paints. The two involving
  // bgSecondary are the ones the coral palette broke.
  const pairs: [string, string, string][] = [
    ['body text on the page background', 'textPrimary', 'bgPrimary'],
    ['secondary text on the page background', 'textSecondary', 'bgPrimary'],
    ['muted text on the page background', 'textMuted', 'bgPrimary'],
    ['muted text on a secondary block', 'textMuted', 'bgSecondary'],
    ['accent text on a secondary block', 'accent', 'bgSecondary'],
    ['muted text on a card', 'textMuted', 'bgCard'],
    ['accent text on a card', 'accent', 'bgCard'],
    ['inverse text on a dark block', 'textInverse', 'bgDark'],
  ];

  for (const [label, fg, bg] of pairs) {
    it(`meets AA 4.5:1 for ${label}`, () => {
      const ratio = contrast(applied[fg], applied[bg]);
      expect(
        Math.round(ratio * 100) / 100,
        `${fg} ${applied[fg]} on ${bg} ${applied[bg]}`,
      ).toBeGreaterThanOrEqual(4.5);
    });
  }

  it('rejects the coral palette that shipped the AA failures', () => {
    // Regression guard with the actual values that were live: accent on bgSecondary
    // measured 3.04:1 in the browser, and this reproduces it from the hex alone.
    expect(contrast('#523417', '#B97534')).toBeLessThan(4.5);
    expect(applied['bgSecondary'].toLowerCase()).not.toBe('#b97534');
  });
});

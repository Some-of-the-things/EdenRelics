import { TestBed } from '@angular/core/testing';
import { DomSanitizer } from '@angular/platform-browser';
import { SecurityContext } from '@angular/core';

/**
 * Blog post HTML is injected with [innerHTML], so everything in it goes through
 * Angular's sanitiser first. Which attributes survive that is load-bearing for
 * how posts can be styled and tuned, and it is not documented anywhere we
 * control — so assert it, rather than assuming and shipping markup that gets
 * silently dropped.
 */
describe('Angular sanitiser: what survives in injected post HTML', () => {
  let sanitizer: DomSanitizer;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    sanitizer = TestBed.inject(DomSanitizer);
  });

  function survives(html: string, attr: string): boolean {
    const clean = sanitizer.sanitize(SecurityContext.HTML, html) ?? '';
    return new RegExp(`\\b${attr}\\s*=`, 'i').test(clean);
  }

  /**
   * The practical consequences, measured rather than assumed:
   *
   *   width/height survive  → CLS on in-post images is fixable, by putting the
   *                           intrinsic dimensions in the stored HTML.
   *   loading is STRIPPED   → native lazy-loading cannot be applied to injected
   *                           post images at all. Adding loading="lazy" to the
   *                           stored HTML looks right in the database and does
   *                           nothing in the browser. Deferring these would mean
   *                           rendering them as real components instead of
   *                           innerHTML, or supplying a custom sanitiser.
   */
  it('reports which <img> attributes are kept', () => {
    const img = '<img src="/a.webp" alt="A dress" width="800" height="456" loading="lazy" decoding="async" sizes="100vw" srcset="/a.webp 800w">';
    const kept = ['src', 'alt', 'width', 'height', 'loading', 'decoding', 'sizes', 'srcset']
      .filter((a) => survives(img, a));
    // Pinned so an Angular upgrade that changes the allowlist fails here rather
    // than silently degrading posts.
    expect(kept).toEqual(['src', 'alt', 'width', 'height', 'sizes', 'srcset']);
  });

  it('keeps the legacy presentational attributes the email-HTML posts rely on', () => {
    const table = '<table bgcolor="#f0ebe0" cellpadding="13" cellspacing="0" border="0" width="100%"><tr><td align="center"><font face="Georgia" size="4" color="#1c1510">x</font></td></tr></table>';
    for (const attr of ['bgcolor', 'cellpadding', 'align', 'width', 'face', 'size', 'color']) {
      expect(survives(table, attr)).toBe(true);
    }
  });

  it('still strips style, which is why post formatting has to live in the stylesheet', () => {
    expect(survives('<p style="color:red">x</p>', 'style')).toBe(false);
  });
});

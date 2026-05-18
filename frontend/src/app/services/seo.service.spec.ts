import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { SeoService, SUPPORTED_LOCALES } from './seo.service';

describe('SeoService', () => {
  let service: SeoService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SeoService);
    document.head.innerHTML = '';
  });

  function meta(name: string): string | null {
    return document.head
      .querySelector<HTMLMetaElement>(`meta[name="${name}"]`)
      ?.getAttribute('content') ?? null;
  }

  function metaProp(prop: string): string | null {
    return document.head
      .querySelector<HTMLMetaElement>(`meta[property="${prop}"]`)
      ?.getAttribute('content') ?? null;
  }

  function hreflangs(): { hreflang: string; href: string }[] {
    return Array.from(
      document.head.querySelectorAll<HTMLLinkElement>('link[rel="alternate"][hreflang]')
    ).map((el) => ({
      hreflang: el.getAttribute('hreflang') ?? '',
      href: el.href,
    }));
  }

  describe('updateTags', () => {
    it('sets title, description, OG and Twitter meta and canonical', () => {
      service.updateTags({
        title: 'Test Page',
        description: 'A description',
        url: '/test',
      });

      expect(document.title).toBe('Test Page | Eden Relics');
      expect(meta('description')).toBe('A description');
      expect(metaProp('og:title')).toBe('Test Page | Eden Relics');
      expect(metaProp('og:description')).toBe('A description');
      expect(metaProp('og:url')).toBe('https://edenrelics.co.uk/test');
      expect(meta('twitter:card')).toBe('summary_large_image');

      const canonical = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
      expect(canonical?.href).toBe('https://edenrelics.co.uk/test');
    });

    it('strips HTML and truncates description over 300 chars', () => {
      const long = '<p>' + 'Word '.repeat(80) + '</p>';
      service.updateTags({ description: long, url: '/' });
      const desc = meta('description') ?? '';
      expect(desc.length).toBeLessThanOrEqual(300);
      expect(desc).not.toContain('<');
    });
  });

  describe('noIndex', () => {
    it('emits robots noindex,nofollow when noIndex is true', () => {
      service.updateTags({ title: 'Private', url: '/admin', noIndex: true });
      expect(meta('robots')).toBe('noindex, nofollow');
    });

    it('removes robots meta when noIndex is omitted', () => {
      service.updateTags({ title: 'Private', url: '/admin', noIndex: true });
      service.updateTags({ title: 'Public', url: '/blog' });
      expect(meta('robots')).toBeNull();
    });
  });

  describe('hreflang', () => {
    it('emits no hreflang tags by default', () => {
      service.updateTags({ url: '/blog' });
      expect(hreflangs().length).toBe(0);
    });

    it('emits hreflang alternates for every supported locale plus x-default and en-GB', () => {
      service.updateTags({ url: '/blog', hreflang: true });
      const tags = hreflangs();
      const codes = tags.map((t) => t.hreflang);

      expect(codes).toContain('en-GB');
      expect(codes).toContain('x-default');
      for (const locale of SUPPORTED_LOCALES) {
        if (locale === 'en') continue;
        expect(codes).toContain(locale);
      }
    });

    it('points en-GB and x-default at the canonical URL with no locale query', () => {
      service.updateTags({ url: '/blog', hreflang: true });
      const tags = hreflangs();
      const enGb = tags.find((t) => t.hreflang === 'en-GB');
      const xDefault = tags.find((t) => t.hreflang === 'x-default');
      expect(enGb?.href).toBe('https://edenrelics.co.uk/blog');
      expect(xDefault?.href).toBe('https://edenrelics.co.uk/blog');
    });

    it('emits ?locale=X for non-English variants', () => {
      service.updateTags({ url: '/blog', hreflang: true });
      const tags = hreflangs();
      const fr = tags.find((t) => t.hreflang === 'fr');
      const ja = tags.find((t) => t.hreflang === 'ja');
      expect(fr?.href).toBe('https://edenrelics.co.uk/blog?locale=fr');
      expect(ja?.href).toBe('https://edenrelics.co.uk/blog?locale=ja');
    });

    it('strips any pre-existing locale query before building variants', () => {
      service.updateTags({ url: '/blog?locale=de&foo=bar', hreflang: true });
      const tags = hreflangs();
      const enGb = tags.find((t) => t.hreflang === 'en-GB');
      const fr = tags.find((t) => t.hreflang === 'fr');
      // Canonical keeps the unrelated query param but drops locale.
      expect(enGb?.href).toBe('https://edenrelics.co.uk/blog?foo=bar');
      expect(fr?.href).toBe('https://edenrelics.co.uk/blog?foo=bar&locale=fr');
    });

    it('clears previous hreflang tags on subsequent calls without hreflang', () => {
      service.updateTags({ url: '/blog', hreflang: true });
      expect(hreflangs().length).toBeGreaterThan(0);
      service.updateTags({ url: '/contact' });
      expect(hreflangs().length).toBe(0);
    });
  });
});

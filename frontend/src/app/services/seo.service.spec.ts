import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { SeoService } from './seo.service';

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

    it('emits og:locale=en_GB and og:site_name', () => {
      service.updateTags({ url: '/' });
      expect(metaProp('og:locale')).toBe('en_GB');
      expect(metaProp('og:site_name')).toBe('Eden Relics');
    });

    it('emits twitter:site and twitter:creator handles', () => {
      service.updateTags({ url: '/' });
      expect(meta('twitter:site')).toBe('@edenrelics');
      expect(meta('twitter:creator')).toBe('@edenrelics');
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
    // Removed 2026-08-04. The eleven ?locale=X alternates pointed at pages that
    // served English SSR HTML and canonicalised back to the bare URL, so the
    // annotation was self-contradictory and only bought ~1,300 duplicate
    // crawlable URLs. See SeoService.clearHreflang.
    it('never emits hreflang alternates', () => {
      service.updateTags({ url: '/blog' });
      expect(hreflangs().length).toBe(0);
    });

    it('removes stale alternates left in server-rendered HTML', () => {
      const stale = document.createElement('link');
      stale.rel = 'alternate';
      stale.setAttribute('hreflang', 'de');
      stale.href = 'https://edenrelics.co.uk/blog?locale=de';
      document.head.appendChild(stale);
      expect(hreflangs().length).toBe(1);

      service.updateTags({ url: '/blog' });
      expect(hreflangs().length).toBe(0);
    });
  });
});

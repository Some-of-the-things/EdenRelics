import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { DOCUMENT } from '@angular/common';
import { htmlToText } from '../utils/html-to-text';

/**
 * Locales the backend can translate product name and description into.
 * Mirrors backend TranslationService.SupportedLocales. Used by the locale
 * switcher; deliberately NOT advertised via hreflang — see clearHreflang().
 */
export const SUPPORTED_LOCALES = [
  'en', 'fr', 'de', 'es', 'it', 'nl', 'pt', 'sv', 'da', 'nb', 'ja', 'ko',
] as const;

export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number];

@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);

  private readonly defaultTitle = 'Eden Relics — Vintage Dresses & Womenswear, 1950s–90s';
  private readonly defaultDescription = 'Hand-picked vintage dresses and womenswear from the 1950s to the 90s — each piece one of a kind, inspected and ready to wear again. UK vintage, thoughtfully sourced.';
  private readonly defaultImage = 'https://edenrelics.co.uk/og-image.png';
  private readonly siteUrl = 'https://edenrelics.co.uk';
  private readonly ogLocale = 'en_GB';
  /** Twitter handle for site / creator attribution. Set to '' to disable. */
  private readonly twitterSite = '@edenrelics';

  updateTags(config: {
    title?: string;
    description?: string;
    url?: string;
    image?: string;
    type?: string;
    noIndex?: boolean;
  }): void {
    const pageTitle = config.title
      ? `${config.title} | Eden Relics`
      : this.defaultTitle;
    const description = this.normaliseDescription(config.description) ?? this.defaultDescription;
    const url = config.url ? `${this.siteUrl}${config.url}` : this.siteUrl;
    const type = config.type ?? 'website';

    this.title.setTitle(pageTitle);
    this.meta.updateTag({ name: 'description', content: description });

    // Open Graph
    this.meta.updateTag({ property: 'og:title', content: pageTitle });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:url', content: url });
    this.meta.updateTag({ property: 'og:type', content: type });
    this.meta.updateTag({ property: 'og:locale', content: this.ogLocale });
    this.meta.updateTag({ property: 'og:site_name', content: 'Eden Relics' });
    const image = config.image ?? this.defaultImage;
    this.meta.updateTag({ property: 'og:image', content: image });

    // Twitter Card
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: pageTitle });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    this.meta.updateTag({ name: 'twitter:image', content: image });
    if (this.twitterSite) {
      this.meta.updateTag({ name: 'twitter:site', content: this.twitterSite });
      this.meta.updateTag({ name: 'twitter:creator', content: this.twitterSite });
    }

    // Canonical URL
    this.updateCanonical(url);

    // Robots
    if (config.noIndex) {
      this.meta.updateTag({ name: 'robots', content: 'noindex, nofollow' });
    } else {
      this.meta.removeTag('name="robots"');
    }

    // No hreflang. See clearHreflang() for why; the call also strips stale
    // alternates out of SSR HTML still sitting in the CDN from before removal.
    this.clearHreflang();
  }

  setJsonLd(schema: object): void {
    const existingScript = this.document.head.querySelector('script[type="application/ld+json"]');
    if (existingScript) {
      existingScript.textContent = JSON.stringify(schema);
    } else {
      const script = this.document.createElement('script');
      script.type = 'application/ld+json';
      script.textContent = JSON.stringify(schema);
      this.document.head.appendChild(script);
    }
  }

  private normaliseDescription(raw: string | undefined): string | undefined {
    if (!raw) return undefined;
    const plain = htmlToText(raw);
    return plain.length > 300 ? plain.slice(0, 297).trimEnd() + '…' : plain;
  }

  private updateCanonical(url: string): void {
    let link = this.document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (link) {
      link.href = url;
    } else {
      link = this.document.createElement('link');
      link.rel = 'canonical';
      link.href = url;
      this.document.head.appendChild(link);
    }
  }

  /**
   * We emit no hreflang alternates, and this removes any that are still around.
   *
   * We used to advertise eleven `?locale=X` variants per page. The translations
   * behind them are real — TranslationService renders product name and
   * description into all eleven, and the API returns them — but LocaleService
   * only runs in the browser, so the SSR HTML a crawler receives at
   * `?locale=de` is English, with an English <title> and <h1>, and it
   * canonicalises back to the bare URL. An alternate that canonicalises
   * elsewhere is a conflicting signal: Google discards the annotation, having
   * first crawled ~1,300 duplicate URLs to find that out. Two of them were
   * already in Bing's index.
   *
   * Re-adding this needs more than a flag. SSR would have to honour ?locale,
   * set <html lang>, translate <title>/description and self-canonicalise — and
   * only product name and description are translated today, so the rest of the
   * page (nav, headings, category and blog copy) would still be English. Mixed-
   * language pages are a poor ranking asset, so the translation coverage is the
   * real prerequisite, not the tags.
   */
  private clearHreflang(): void {
    this.document.head
      .querySelectorAll('link[rel="alternate"][hreflang]')
      .forEach((el) => el.remove());
  }
}

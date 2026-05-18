import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { DOCUMENT } from '@angular/common';

/** Locales the backend can translate content into. Mirrors backend SupportedLocales. */
export const SUPPORTED_LOCALES = [
  'en', 'fr', 'de', 'es', 'it', 'nl', 'pt', 'sv', 'da', 'nb', 'ja', 'ko',
] as const;

export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number];

@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);

  private readonly defaultTitle = 'Eden Relics — Vintage Clothing';
  private readonly defaultDescription = 'Vintage women’s clothing from the 1960s, 70s, 80s and 90s. Thoughtfully sourced, carefully assessed — slow fashion worth wearing again.';
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
    /** Emit hreflang alternates pointing to ?locale=X variants for this path. */
    hreflang?: boolean;
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

    // hreflang alternates
    if (config.hreflang && config.url !== undefined) {
      this.updateHreflang(config.url);
    } else {
      this.clearHreflang();
    }
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
    const plain = raw
      .replace(/<br\s*\/?>/gi, ' ')
      .replace(/<\/p>/gi, ' ')
      .replace(/<[^>]+>/g, '')
      .replace(/&nbsp;/g, ' ')
      .replace(/&amp;/g, '&')
      .replace(/&lt;/g, '<')
      .replace(/&gt;/g, '>')
      .replace(/&quot;/g, '"')
      .replace(/&#39;/g, "'")
      .replace(/\s+/g, ' ')
      .trim();
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

  private updateHreflang(path: string): void {
    // Strip any existing locale query so we build clean variant URLs.
    const [pathOnly, query] = path.split('?');
    const params = new URLSearchParams(query ?? '');
    params.delete('locale');
    const cleanQuery = params.toString();
    const basePath = cleanQuery ? `${pathOnly}?${cleanQuery}` : pathOnly;
    const baseUrl = `${this.siteUrl}${basePath}`;

    this.clearHreflang();

    // English / canonical: the bare URL.
    this.appendHreflang('en-GB', baseUrl);
    this.appendHreflang('x-default', baseUrl);

    for (const locale of SUPPORTED_LOCALES) {
      if (locale === 'en') {
        continue;
      }
      const sep = baseUrl.includes('?') ? '&' : '?';
      this.appendHreflang(locale, `${baseUrl}${sep}locale=${locale}`);
    }
  }

  private clearHreflang(): void {
    this.document.head
      .querySelectorAll('link[rel="alternate"][hreflang]')
      .forEach((el) => el.remove());
  }

  private appendHreflang(hreflang: string, href: string): void {
    const link = this.document.createElement('link');
    link.rel = 'alternate';
    link.setAttribute('hreflang', hreflang);
    link.href = href;
    this.document.head.appendChild(link);
  }
}

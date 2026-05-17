import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { DOCUMENT } from '@angular/common';

@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);

  private readonly defaultTitle = 'Eden Relics — Vintage Clothing';
  private readonly defaultDescription = 'Shop curated vintage dresses from the 1970s to today at Eden Relics. Carefully sourced bohemian maxis, power dresses, silk slips and Y2K styles.';
  private readonly defaultImage = 'https://edenrelics.co.uk/og-image.png';
  private readonly siteUrl = 'https://edenrelics.co.uk';

  updateTags(config: {
    title?: string;
    description?: string;
    url?: string;
    image?: string;
    type?: string;
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
    const image = config.image ?? this.defaultImage;
    this.meta.updateTag({ property: 'og:image', content: image });

    // Twitter Card
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: pageTitle });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    this.meta.updateTag({ name: 'twitter:image', content: image });

    // Canonical URL
    this.updateCanonical(url);
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
}

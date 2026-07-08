import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe, isPlatformBrowser } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { SeoService } from '../../services/seo.service';
import { buildFaqPage } from '../../utils/faq-schema';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { DesignerProfile, findDesignerBySlug, matchProductsToDesigner } from './designers.data';

@Component({
  selector: 'app-designer-page',
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './designer-page.component.html',
  styleUrl: './designers.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DesignerPageComponent {
  private readonly seo = inject(SeoService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly productStore = inject(ProductStore);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  // Present only during server render; null in the browser.
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  private readonly slug = toSignal(
    this.route.paramMap,
    { initialValue: this.route.snapshot.paramMap },
  );

  readonly designer = computed<DesignerProfile | undefined>(() => {
    const s = this.slug().get('slug');
    return s ? findDesignerBySlug(s) : undefined;
  });

  readonly products = computed<Product[]>(() => {
    const d = this.designer();
    if (!d) {
      return [];
    }
    return matchProductsToDesigner(this.productStore.liveProducts(), d);
  });

  private readonly seoApplied = signal('');

  constructor() {
    effect(() => {
      const d = this.designer();
      if (!d) {
        // Unknown slug. Real visitors get bounced to /designers; crawlers get
        // a genuine 404 (a soft redirect would otherwise be indexed as 200).
        if (this.slug().get('slug')) {
          if (this.isBrowser) {
            this.router.navigate(['/designers']);
          } else {
            this.markNotFound();
          }
        }
        return;
      }
      // Avoid re-emitting tags for the same designer on every CD cycle.
      if (this.seoApplied() === d.slug) {
        return;
      }
      this.seoApplied.set(d.slug);
      this.applySeo(d);
    });
  }

  /** Server-only: emit a 404 status for an unknown designer slug. No-op in the browser. */
  private markNotFound(): void {
    if (this.responseInit) {
      this.responseInit.status = 404;
    }
    this.seo.updateTags({ title: 'Designer not found', noIndex: true });
  }

  private applySeo(d: DesignerProfile): void {
    this.seo.updateTags({
      title: d.metaTitle,
      description: d.metaDescription,
      url: `/designers/${d.slug}`,
    });

    const products = matchProductsToDesigner(this.productStore.liveProducts(), d);
    const pageUrl = `https://edenrelics.co.uk/designers/${d.slug}`;
    // FAQ schema from the visible identification section — the answer text is the
    // same tips shown on the page (Google's requirement for FAQPage).
    const idHeading = d.identificationHeading || `How to identify authentic ${d.name}`;
    const faqPage = buildFaqPage([
      { question: `${idHeading}?`, answer: d.identification.join(' ') },
    ]);
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'CollectionPage',
          name: `Vintage ${d.name}`,
          description: d.intro,
          url: pageUrl,
          isPartOf: {
            '@type': 'WebSite',
            '@id': 'https://edenrelics.co.uk/#website',
            name: 'Eden Relics',
            url: 'https://edenrelics.co.uk',
          },
          mainEntity: {
            '@type': 'ItemList',
            numberOfItems: products.length,
            itemListElement: products.map((p, idx) => ({
              '@type': 'ListItem',
              position: idx + 1,
              url: `https://edenrelics.co.uk/product/${p.slug || p.id}`,
              name: p.name,
              image: p.imageUrl,
            })),
          },
        },
        {
          '@type': 'BreadcrumbList',
          itemListElement: [
            {
              '@type': 'ListItem',
              position: 1,
              name: 'Home',
              item: 'https://edenrelics.co.uk',
            },
            {
              '@type': 'ListItem',
              position: 2,
              name: 'Designers',
              item: 'https://edenrelics.co.uk/designers',
            },
            {
              '@type': 'ListItem',
              position: 3,
              name: d.name,
              item: pageUrl,
            },
          ],
        },
        ...(faqPage ? [faqPage] : []),
      ],
    });
  }
}

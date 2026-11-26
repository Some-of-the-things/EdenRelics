import { ShownPricePipe } from '../../pipes/shown-price.pipe';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe, isPlatformBrowser } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { SeoService } from '../../services/seo.service';
import { buildFaqPage } from '../../utils/faq-schema';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { CategoryService, SubCategory } from '../../services/category.service';
import { followMissingSubCategory, paragraphs } from '../../utils/category-redirect';
import { DesignerProfile, findDesignerBySlug } from './designers.data';

/**
 * A designer / label page under /designers. The page itself (name, slug, short text, SEO,
 * visibility, which pieces) is managed from the admin Categories tab; the longer written sections
 * of the launch pages (history, how to identify, further reading) are still held in
 * designers.data.ts and found through the page's contentKey.
 */
@Component({
  selector: 'app-designer-page',
  imports: [ShownPricePipe, RouterLink, CurrencyPipe],
  templateUrl: './designer-page.component.html',
  styleUrl: './designers.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DesignerPageComponent {
  private readonly seo = inject(SeoService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly productStore = inject(ProductStore);
  private readonly categories = inject(CategoryService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  // Present only during server render; null in the browser.
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  private readonly slug = toSignal(
    this.route.paramMap,
    { initialValue: this.route.snapshot.paramMap },
  );

  readonly designer = computed<SubCategory | undefined>(() => {
    const s = this.slug().get('slug');
    return s ? this.categories.find('designers', s) : undefined;
  });

  /** Written sections kept in code for the launch pages. */
  readonly written = computed<DesignerProfile | undefined>(() => {
    const key = this.designer()?.contentKey;
    return key ? findDesignerBySlug(key) : undefined;
  });

  readonly aboutParagraphs = computed<string[]>(() => {
    const d = this.designer();
    if (!d) {
      return [];
    }
    return d.body ? paragraphs(d.body) : (this.written()?.history ?? []);
  });

  readonly identification = computed<string[]>(() => this.written()?.identification ?? []);

  readonly identificationHeading = computed(() => {
    const d = this.designer();
    return this.written()?.identificationHeading || `How to identify authentic ${d?.name ?? ''}`;
  });

  readonly relatedPosts = computed(() => this.written()?.relatedPosts ?? []);

  readonly products = computed<Product[]>(() => {
    const d = this.designer();
    return d ? this.categories.productsIn(d, this.productStore.liveProducts()) : [];
  });

  private readonly seoApplied = signal('');

  constructor() {
    this.categories.load();
    effect(() => {
      // Decide 404/redirect and set SEO only against the live list, not the built-in fallback.
      if (!this.categories.loaded()) {
        return;
      }
      const d = this.designer();
      const slug = this.slug().get('slug');
      if (!d) {
        if (slug) {
          this.handleMissing(slug);
        }
        return;
      }
      const key = `${d.id}:${d.slug}:${d.metaTitle}:${d.metaDescription}`;
      if (this.seoApplied() === key) {
        return;
      }
      this.seoApplied.set(key);
      this.applySeo(d);
    });
  }

  private handleMissing(slug: string): void {
    // Renamed, deleted or hidden pages go where they now live; anything else is a real 404.
    if (followMissingSubCategory(this.categories.resolveMissing('designers', slug), this.router, this.responseInit)) {
      return;
    }
    // Real visitors get bounced to /designers; crawlers get a genuine 404
    // (a soft redirect would otherwise be indexed as 200).
    if (this.isBrowser) {
      this.router.navigate(['/designers']);
    } else {
      this.markNotFound();
    }
  }

  /** Server-only: emit a 404 status for an unknown designer slug. No-op in the browser. */
  private markNotFound(): void {
    if (this.responseInit) {
      this.responseInit.status = 404;
    }
    this.seo.updateTags({ title: 'Designer not found', noIndex: true });
  }

  private applySeo(d: SubCategory): void {
    this.seo.updateTags({
      title: d.metaTitle || `Vintage ${d.name}`,
      description: d.metaDescription || d.intro,
      url: `/designers/${d.slug}`,
    });

    const products = this.categories.productsIn(d, this.productStore.liveProducts());
    const pageUrl = `https://edenrelics.co.uk/designers/${d.slug}`;
    // FAQ schema from the visible identification section — the answer text is the
    // same tips shown on the page (Google's requirement for FAQPage).
    const tips = this.identification();
    const faqPage = tips.length > 0
      ? buildFaqPage([{ question: `${this.identificationHeading()}?`, answer: tips.join(' ') }])
      : null;
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

import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe, isPlatformBrowser } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { SeoService } from '../../services/seo.service';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { CollectionProfile, collectionProductSlugs, findCollectionBySlug, orderedCollectionProducts } from './collections.data';

@Component({
  selector: 'app-collection-page',
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './collection-page.component.html',
  styleUrl: './collections.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CollectionPageComponent {
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

  readonly collection = computed<CollectionProfile | undefined>(() => {
    const s = this.slug().get('slug');
    return s ? findCollectionBySlug(s) : undefined;
  });

  readonly products = computed<Product[]>(() => {
    const c = this.collection();
    if (!c) {
      return [];
    }
    return orderedCollectionProducts(this.productStore.liveOrSoldProducts(), collectionProductSlugs(c));
  });

  private readonly seoApplied = signal('');

  constructor() {
    effect(() => {
      const c = this.collection();
      if (!c) {
        // Unknown slug. Real visitors get bounced home; crawlers get a genuine
        // 404 (a soft redirect would otherwise be indexed as 200).
        if (this.slug().get('slug')) {
          if (this.isBrowser) {
            this.router.navigate(['/']);
          } else {
            this.markNotFound();
          }
        }
        return;
      }
      // Avoid re-emitting tags for the same collection on every CD cycle.
      if (this.seoApplied() === c.slug) {
        return;
      }
      this.seoApplied.set(c.slug);
      this.applySeo(c);
    });
  }

  /** Server-only: emit a 404 status for an unknown collection slug. No-op in the browser. */
  private markNotFound(): void {
    if (this.responseInit) {
      this.responseInit.status = 404;
    }
    this.seo.updateTags({ title: 'Collection not found', noIndex: true });
  }

  private applySeo(c: CollectionProfile): void {
    this.seo.updateTags({
      title: c.metaTitle,
      description: c.metaDescription,
      url: `/collections/${c.slug}`,
    });

    const products = orderedCollectionProducts(this.productStore.liveProducts(), collectionProductSlugs(c));
    const pageUrl = `https://edenrelics.co.uk/collections/${c.slug}`;
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'CollectionPage',
          name: c.name,
          description: c.intro,
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
              name: c.name,
              item: pageUrl,
            },
          ],
        },
      ],
    });
  }
}

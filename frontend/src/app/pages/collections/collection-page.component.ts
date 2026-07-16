import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe, isPlatformBrowser } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { SeoService } from '../../services/seo.service';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { CollectionProfile, collectionProductSlugs, findCollectionBySlug, orderedCollectionProducts, orderedProductsById } from './collections.data';
import { TopPicksService } from '../../services/top-picks.service';

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

  private readonly topPicks = inject(TopPicksService);

  private readonly slug = toSignal(
    this.route.paramMap,
    { initialValue: this.route.snapshot.paramMap },
  );

  /**
   * Slug from the /collections/:slug param, or from a fixed route (e.g. /top-picks) that
   * supplies it via route data — lets one component back both the generic and dedicated pages.
   */
  private readonly routeSlug = computed<string | null>(() =>
    this.slug().get('slug') ??
    (this.route.snapshot.data['collectionSlug'] as string | undefined) ??
    null,
  );

  readonly collection = computed<CollectionProfile | undefined>(() => {
    const s = this.routeSlug();
    const c = s ? findCollectionBySlug(s) : undefined;
    if (c?.dynamicMembership) {
      // Top Picks lives only at its canonical /top-picks route (which supplies the slug via route
      // data), and only once its own switch is on. Reaching it via /collections/top-picks, or while
      // gated, is a genuine not-found — avoids a duplicate URL and keeps it dormant until launch.
      const viaGenericRoute = this.slug().get('slug') === c.slug;
      if (viaGenericRoute || !this.topPicks.enabled()) {
        return undefined;
      }
    }
    return c;
  });

  /** Canonical URL path — a dedicated route can override via data.canonicalPath (e.g. /top-picks). */
  private readonly pagePath = computed<string>(() => {
    const custom = this.route.snapshot.data['canonicalPath'] as string | undefined;
    const c = this.collection();
    return custom ?? (c ? `/collections/${c.slug}` : '/collections');
  });

  readonly products = computed<Product[]>(() => {
    const c = this.collection();
    if (!c) {
      return [];
    }
    // Top Picks resolves by product ID from the DB-curated list; static collections resolve by slug.
    if (c.dynamicMembership) {
      return orderedProductsById(this.productStore.liveOrSoldProducts(), this.topPicks.productIds());
    }
    return orderedCollectionProducts(this.productStore.liveOrSoldProducts(), collectionProductSlugs(c));
  });

  private readonly seoApplied = signal('');

  constructor() {
    // Needed so the gated Top Picks edit resolves (flag + curated SKUs) on this page too.
    this.topPicks.load();
    effect(() => {
      const c = this.collection();
      if (!c) {
        // Unknown/gated slug. Real visitors get bounced home; crawlers get a genuine
        // 404 (a soft redirect would otherwise be indexed as 200).
        if (this.routeSlug()) {
          if (this.isBrowser) {
            this.router.navigate(['/']);
          } else {
            this.markNotFound();
          }
        }
        return;
      }
      // Meta tags depend only on the collection — apply once per slug.
      if (this.seoApplied() !== c.slug) {
        this.seoApplied.set(c.slug);
        this.applyMeta(c);
      }
      // The JSON-LD ItemList depends on the resolved products, which populate
      // after this effect first runs. Re-emit reactively as they load so the
      // list isn't frozen empty from the initial (pre-hydration) pass.
      this.applyJsonLd(c, this.products());
    });
  }

  /** Server-only: emit a 404 status for an unknown collection slug. No-op in the browser. */
  private markNotFound(): void {
    if (this.responseInit) {
      this.responseInit.status = 404;
    }
    this.seo.updateTags({ title: 'Collection not found', noIndex: true });
  }

  private applyMeta(c: CollectionProfile): void {
    this.seo.updateTags({
      title: c.metaTitle,
      description: c.metaDescription,
      url: this.pagePath(),
    });
  }

  private applyJsonLd(c: CollectionProfile, products: readonly Product[]): void {
    const pageUrl = `https://edenrelics.co.uk${this.pagePath()}`;
    const collectionPage: Record<string, unknown> = {
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
    };

    // Only attach the ItemList once products have resolved — an empty list
    // would otherwise be served to crawlers as a thin, item-less page.
    if (products.length > 0) {
      collectionPage['mainEntity'] = {
        '@type': 'ItemList',
        numberOfItems: products.length,
        itemListElement: products.map((p, idx) => ({
          '@type': 'ListItem',
          position: idx + 1,
          url: `https://edenrelics.co.uk/product/${p.slug || p.id}`,
          name: p.name,
          image: p.imageUrl,
        })),
      };
    }

    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@graph': [
        collectionPage,
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

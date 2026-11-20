import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  PLATFORM_ID,
  RESPONSE_INIT,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CurrencyPipe, isPlatformBrowser } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { SeoService } from '../../services/seo.service';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { CategoryService, HubKey, SubCategory, subCategoryPath } from '../../services/category.service';
import { followMissingSubCategory, paragraphs } from '../../utils/category-redirect';
import { CategoryHub, LookForPoint, findHub } from './category.data';

/**
 * A sub-category page under /style or /dresses. The page itself (name, slug, short text, SEO,
 * visibility, which pieces) is managed from the admin Categories tab; the longer written sections
 * of the launch pages ("what to look for", related links) are still held in category.data.ts and
 * found through the page's contentKey.
 */
@Component({
  selector: 'app-category-hub',
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './category-hub.component.html',
  styleUrl: './category.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CategoryHubComponent {
  private readonly seo = inject(SeoService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly productStore = inject(ProductStore);
  private readonly categories = inject(CategoryService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  // Present only during server render; null in the browser.
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  /** 'style' or 'garment', from the route's static data. */
  private readonly kind: CategoryHub['kind'] = this.route.snapshot.data['kind'];
  private readonly hubKey: HubKey = this.kind === 'style' ? 'style' : 'dresses';

  private readonly slug = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });

  readonly hub = computed<SubCategory | undefined>(() => {
    const s = this.slug().get('slug');
    return s ? this.categories.find(this.hubKey, s) : undefined;
  });

  /** Written sections kept in code for the launch pages. */
  private readonly written = computed<CategoryHub | undefined>(() => {
    const key = this.hub()?.contentKey;
    return key ? findHub(this.kind, key) : undefined;
  });

  readonly bodyParagraphs = computed<string[]>(() => {
    const h = this.hub();
    if (!h) {
      return [];
    }
    return h.body ? paragraphs(h.body) : (this.written()?.body ?? []);
  });

  readonly relatedLinks = computed(() => this.written()?.relatedLinks ?? []);

  readonly products = computed<Product[]>(() => {
    const h = this.hub();
    return h ? this.categories.productsIn(h, this.productStore.liveProducts()) : [];
  });

  /**
   * "What to look for" bullets normalised to one shape, so the template does not
   * have to branch on the string-or-object union in `CategoryHub.lookFor`.
   */
  readonly lookForPoints = computed<
    (LookForPoint | { text: string; link?: undefined; textAfter?: undefined })[]
  >(() =>
    (this.written()?.lookFor ?? []).map((point) =>
      typeof point === 'string' ? { text: point } : point,
    ),
  );

  /** Index path for the "back" link and breadcrumb — /style or /dresses. */
  readonly indexPath = `/${this.hubKey}`;
  readonly indexLabel = this.kind === 'style' ? 'Shop by Style' : 'Dress Types';

  private readonly seoApplied = signal('');

  constructor() {
    this.categories.load();
    effect(() => {
      // Decide 404/redirect and set SEO only against the live list, not the built-in fallback.
      if (!this.categories.loaded()) {
        return;
      }
      const h = this.hub();
      const slug = this.slug().get('slug');
      if (!h) {
        if (slug) {
          this.handleMissing(slug);
        }
        return;
      }
      const key = `${h.id}:${h.slug}:${h.metaTitle}:${h.metaDescription}`;
      if (this.seoApplied() === key) {
        return;
      }
      this.seoApplied.set(key);
      this.applySeo(h);
    });
  }

  private handleMissing(slug: string): void {
    // Renamed, deleted or hidden pages go where they now live; anything else is a real 404.
    if (followMissingSubCategory(this.categories.resolveMissing(this.hubKey, slug), this.router, this.responseInit)) {
      return;
    }
    // Real visitors get bounced to the index; crawlers get a genuine 404
    // (a soft redirect would otherwise be indexed as 200).
    if (this.isBrowser) {
      this.router.navigate([this.indexPath]);
    } else {
      this.markNotFound();
    }
  }

  private markNotFound(): void {
    if (this.responseInit) {
      this.responseInit.status = 404;
    }
    this.seo.updateTags({ title: 'Page not found', noIndex: true });
  }

  private applySeo(hub: SubCategory): void {
    const path = subCategoryPath(hub);
    const title = hub.metaTitle || `Vintage ${hub.name}`;
    const description = hub.metaDescription || hub.intro;
    this.seo.updateTags({ title, description, url: path });

    const products = this.categories.productsIn(hub, this.productStore.liveProducts());
    const pageUrl = `https://edenrelics.co.uk${path}`;
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'CollectionPage',
          name: title,
          description,
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
            itemListElement: products.slice(0, 30).map((p, idx) => ({
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
            { '@type': 'ListItem', position: 1, name: 'Home', item: 'https://edenrelics.co.uk' },
            {
              '@type': 'ListItem',
              position: 2,
              name: this.indexLabel,
              item: `https://edenrelics.co.uk${this.indexPath}`,
            },
            { '@type': 'ListItem', position: 3, name: hub.name, item: pageUrl },
          ],
        },
      ],
    });
  }
}

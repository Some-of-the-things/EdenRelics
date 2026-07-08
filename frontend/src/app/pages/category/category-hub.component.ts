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
import { CategoryHub, findHub, hubPath, matchProductsToHub } from './category.data';

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
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  // Present only during server render; null in the browser.
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  /** 'style' or 'garment', from the route's static data. */
  private readonly kind: CategoryHub['kind'] = this.route.snapshot.data['kind'];

  private readonly slug = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });

  readonly hub = computed<CategoryHub | undefined>(() => {
    const s = this.slug().get('slug');
    return s ? findHub(this.kind, s) : undefined;
  });

  readonly products = computed<Product[]>(() => {
    const h = this.hub();
    return h ? matchProductsToHub(this.productStore.liveProducts(), h) : [];
  });

  /** Index path for the "back" link and breadcrumb — /style or /dresses. */
  readonly indexPath = this.kind === 'style' ? '/style' : '/dresses';
  readonly indexLabel = this.kind === 'style' ? 'Shop by Style' : 'Dress Types';

  private readonly seoApplied = signal('');

  constructor() {
    effect(() => {
      const h = this.hub();
      if (!h) {
        // Unknown slug. Real visitors get bounced to the index; crawlers get a
        // genuine 404 (a soft redirect would otherwise be indexed as 200).
        if (this.slug().get('slug')) {
          if (this.isBrowser) {
            this.router.navigate([this.indexPath]);
          } else {
            this.markNotFound();
          }
        }
        return;
      }
      if (this.seoApplied() === h.slug) {
        return;
      }
      this.seoApplied.set(h.slug);
      this.applySeo(h);
    });
  }

  private markNotFound(): void {
    if (this.responseInit) {
      this.responseInit.status = 404;
    }
    this.seo.updateTags({ title: 'Page not found', noIndex: true });
  }

  private applySeo(hub: CategoryHub): void {
    const path = hubPath(hub);
    this.seo.updateTags({
      title: hub.metaTitle,
      description: hub.metaDescription,
      url: path,
      hreflang: true,
    });

    const products = matchProductsToHub(this.productStore.liveProducts(), hub);
    const pageUrl = `https://edenrelics.co.uk${path}`;
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'CollectionPage',
          name: hub.metaTitle,
          description: hub.metaDescription,
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

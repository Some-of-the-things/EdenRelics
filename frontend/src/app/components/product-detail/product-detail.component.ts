import { Component, computed, effect, inject, input, signal, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CurrencyPipe, isPlatformBrowser, TitleCasePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ProductStore } from '../../store/product.store';
import { CartStore } from '../../store/cart.store';
import { Product } from '../../models/product.model';
import { SeoService } from '../../services/seo.service';
import { ProductService } from '../../services/product.service';
import { AnalyticsService } from '../../services/analytics.service';
import { AuthService } from '../../services/auth.service';
import { FavouritesService } from '../../services/favourites.service';
import { LocalPricePipe } from '../../pipes/local-price.pipe';
import { imageSrcAt, imageSrcset } from '../../utils/image-variant-loader';
import { ShareButtonsComponent } from '../share-buttons/share-buttons.component';
import { resolveProductStatus } from '../../utils/product-status';
import { findDesignerForProduct } from '../../pages/designers/designers.data';
import { FocusTrapDirective } from '../../directives/focus-trap.directive';

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function stripHtml(html: string): string {
  return html
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
    .trim()
    .slice(0, 300);
}

function schemaCondition(condition: string): string {
  switch (condition.toLowerCase()) {
    case 'mint':
    case 'excellent':
      return 'https://schema.org/NewCondition';
    case 'very good':
    case 'good':
    case 'fair':
    default:
      return 'https://schema.org/UsedCondition';
  }
}

@Component({
  selector: 'app-product-detail',
  imports: [RouterLink, CurrencyPipe, TitleCasePipe, LocalPricePipe, ShareButtonsComponent, FocusTrapDirective],
  templateUrl: './product-detail.component.html',
  styleUrl: './product-detail.component.scss',
})
export class ProductDetailComponent {
  readonly id = input.required<string>();
  private readonly productStore = inject(ProductStore);
  readonly cartStore = inject(CartStore);
  private readonly seo = inject(SeoService);
  private readonly productService = inject(ProductService);
  private readonly analytics = inject(AnalyticsService);
  private readonly auth = inject(AuthService);
  private readonly favourites = inject(FavouritesService);
  private readonly router = inject(Router);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  // Present only during server render; null in the browser. Lets us return a
  // real 404 status for product URLs that don't resolve to a product (e.g.
  // legacy numeric IDs) instead of a soft-404 (200 + "Product not found").
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  private readonly http = inject(HttpClient);

  readonly selectedImage = signal<string | null>(null);
  readonly showSalePrompt = signal(false);
  /** The published care guide for this product's fabric, if one exists. */
  readonly careGuide = signal<{ slug: string; name: string } | null>(null);
  private lastResolvedMaterial: string | null = null;
  private pendingFavouriteId: string | null = null;

  private readonly fetchedProduct = signal<Product | null>(null);

  readonly product = computed(() => {
    const param = this.id();
    const store = this.productStore.products();
    return store.find(p => p.id === param) ?? store.find(p => p.slug === param) ?? this.fetchedProduct();
  });

  readonly allImages = computed(() => {
    const p = this.product();
    if (!p) return [];
    return [p.imageUrl, ...(p.additionalImageUrls ?? [])];
  });

  /** A sold one-of-one piece can't be bought again — surface a "Sold" state instead of Add to Cart. */
  readonly isSold = computed(() => {
    const p = this.product();
    return p ? resolveProductStatus(p) === 'sold' : false;
  });

  /** The designer collection to point a sold-item visitor toward, if the piece matches one. */
  readonly soldDesigner = computed(() => {
    const p = this.product();
    return p ? findDesignerForProduct(p.name) : undefined;
  });

  readonly currentImage = computed(() =>
    this.selectedImage() ?? this.product()?.imageUrl ?? ''
  );

  readonly srcset = imageSrcset;
  readonly srcAt = imageSrcAt;

  readonly productShareUrl = computed(() => {
    const p = this.product();
    if (!p) return '';
    const segment = p.slug || p.id;
    return `https://edenrelics.co.uk/product/${segment}`;
  });

  readonly productShareDescription = computed(() => {
    const p = this.product();
    return p ? stripHtml(p.description) : '';
  });

  selectImage(url: string): void {
    this.selectedImage.set(url);
  }

  toggleFavourite(productId: string): void {
    if (!this.auth.isAuthenticated()) {
      const p = this.product();
      const segment = p?.slug ?? productId;
      this.router.navigate(['/login'], { queryParams: { returnUrl: `/product/${segment}` } });
      return;
    }
    if (this.favourites.isFavourite(productId)) {
      this.favourites.remove(productId);
    } else {
      this.pendingFavouriteId = productId;
      this.showSalePrompt.set(true);
    }
  }

  confirmSaleNotification(notify: boolean): void {
    if (this.pendingFavouriteId) {
      this.favourites.add(this.pendingFavouriteId, notify);
    }
    this.pendingFavouriteId = null;
    this.showSalePrompt.set(false);
  }

  isFavourite(productId: string): boolean {
    return this.favourites.isFavourite(productId);
  }

  /**
   * Marks the current render as a 404. On the server this mutates the shared
   * RESPONSE_INIT (read when @angular/ssr builds the final Response), so the
   * crawler gets a genuine 404 rather than a 200 soft-404. In the browser
   * responseInit is null, so we only tag the page noindex.
   */
  private markNotFound(): void {
    if (this.responseInit) {
      this.responseInit.status = 404;
    }
    this.seo.updateTags({ title: 'Product not found', noIndex: true });
  }

  constructor() {
    if (this.auth.isAuthenticated()) {
      this.favourites.load();
    }
    // Fetch from API if not in store (e.g., direct navigation or newly created product)
    effect(() => {
      const param = this.id();
      const store = this.productStore.products();
      const inStore = store.find(p => p.id === param || p.slug === param);
      if (!inStore && !this.fetchedProduct() && param) {
        const fetch$ = UUID_PATTERN.test(param)
          ? this.productService.getById(param)
          : this.productService.getBySlug(param);
        fetch$.subscribe({
          next: (p) => {
            this.fetchedProduct.set(p ?? null);
            if (!p) {
              this.markNotFound();
            }
          },
        });
      }
    });
    // Resolve the fabric's care guide (if published) so we can cross-link to it.
    effect(() => {
      const material = this.product()?.material?.trim();
      if (!material) {
        this.careGuide.set(null);
        return;
      }
      if (material === this.lastResolvedMaterial) {
        return;
      }
      this.lastResolvedMaterial = material;
      this.http
        .get<{ slug: string; name: string }>(
          `${environment.apiUrl}/api/care/resolve?material=${encodeURIComponent(material)}`,
        )
        .subscribe({
          next: (g) => this.careGuide.set(g),
          error: () => this.careGuide.set(null),
        });
    });
    // If we landed on a UUID URL but the product has a slug, swap the URL in-place
    effect(() => {
      const param = this.id();
      const product = this.product();
      if (this.isBrowser && product?.slug && param !== product.slug && UUID_PATTERN.test(param)) {
        this.router.navigate(['/product', product.slug], { replaceUrl: true });
      }
    });
    effect(() => {
      const product = this.product();
      if (product) {
        const canonicalPath = product.slug ? `/product/${product.slug}` : `/product/${product.id}`;
        this.seo.updateTags({
          title: product.name,
          description: stripHtml(product.description),
          url: canonicalPath,
          image: product.imageUrl,
          type: 'product',
          hreflang: true,
        });
        this.analytics.viewProduct(product.id, product.name, product.price);
        if (this.isBrowser) {
          const params = new URLSearchParams(window.location.search);
          this.productService.recordView(product.id, {
            referrer: document.referrer || undefined,
            utmSource: params.get('utm_source') || undefined,
            utmMedium: params.get('utm_medium') || undefined,
            utmCampaign: params.get('utm_campaign') || undefined,
            screenResolution: `${window.screen.width}x${window.screen.height}`,
          }).subscribe();
        }
        const activePrice = product.showReduction && product.salePrice ? product.salePrice : product.price;
        const productUrl = `https://edenrelics.co.uk${canonicalPath}`;
        const description = stripHtml(product.description);
        const uploadDate = product.createdAtUtc ?? new Date().toISOString();
        const videos = (product.videoUrls ?? []).map((url, idx) => ({
          '@type': 'VideoObject',
          name: `${product.name}${product.videoUrls && product.videoUrls.length > 1 ? ` — Video ${idx + 1}` : ''}`,
          description,
          thumbnailUrl: product.imageUrl,
          uploadDate,
          contentUrl: url,
        }));
        const graph: object[] = [
          {
            '@type': 'BreadcrumbList',
            itemListElement: [
              { '@type': 'ListItem', position: 1, name: 'Home', item: 'https://edenrelics.co.uk' },
              { '@type': 'ListItem', position: 2, name: product.name, item: productUrl },
            ],
          },
          {
            '@type': 'Product',
            name: product.name,
            description,
            image: [product.imageUrl, ...(product.additionalImageUrls ?? [])],
            sku: product.id,
            url: productUrl,
            brand: { '@type': 'Brand', name: 'Eden Relics' },
            category: product.era,
            itemCondition: schemaCondition(product.condition),
            ...(videos.length > 0 ? { video: videos.length === 1 ? videos[0] : videos } : {}),
            offers: {
              '@type': 'Offer',
              url: productUrl,
              price: activePrice,
              priceCurrency: 'GBP',
              availability: product.inStock
                ? 'https://schema.org/InStock'
                : 'https://schema.org/OutOfStock',
              itemCondition: schemaCondition(product.condition),
              seller: { '@type': 'Organization', name: 'Eden Relics' },
            },
          },
        ];
        this.seo.setJsonLd({
          '@context': 'https://schema.org',
          '@graph': graph,
        });
      }
    });
  }
}

import { Component, computed, effect, inject, input, signal, PLATFORM_ID } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CurrencyPipe, isPlatformBrowser, TitleCasePipe } from '@angular/common';
import { ProductStore } from '../../store/product.store';
import { CartStore } from '../../store/cart.store';
import { Product } from '../../models/product.model';
import { SeoService } from '../../services/seo.service';
import { ProductService } from '../../services/product.service';
import { AnalyticsService } from '../../services/analytics.service';
import { AuthService } from '../../services/auth.service';
import { FavouritesService } from '../../services/favourites.service';
import { LocalPricePipe } from '../../pipes/local-price.pipe';

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
  imports: [RouterLink, CurrencyPipe, TitleCasePipe, LocalPricePipe],
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

  readonly selectedImage = signal<string | null>(null);
  readonly showSalePrompt = signal(false);
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

  readonly currentImage = computed(() =>
    this.selectedImage() ?? this.product()?.imageUrl ?? ''
  );

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
          next: (p) => this.fetchedProduct.set(p ?? null),
        });
      }
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
        this.seo.setJsonLd({
          '@context': 'https://schema.org',
          '@type': 'Product',
          name: product.name,
          description: stripHtml(product.description),
          image: [product.imageUrl, ...(product.additionalImageUrls ?? [])],
          sku: product.id,
          url: productUrl,
          brand: {
            '@type': 'Brand',
            name: 'Eden Relics',
          },
          category: product.era,
          itemCondition: schemaCondition(product.condition),
          offers: {
            '@type': 'Offer',
            url: productUrl,
            price: activePrice,
            priceCurrency: 'GBP',
            availability: product.inStock
              ? 'https://schema.org/InStock'
              : 'https://schema.org/OutOfStock',
            itemCondition: schemaCondition(product.condition),
            seller: {
              '@type': 'Organization',
              name: 'Eden Relics',
            },
          },
        });
      }
    });
  }
}

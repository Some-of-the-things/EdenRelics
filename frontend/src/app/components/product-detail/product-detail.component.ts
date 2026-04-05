import { Component, computed, effect, inject, input, signal, PLATFORM_ID } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CurrencyPipe, DecimalPipe, isPlatformBrowser, TitleCasePipe } from '@angular/common';
import { ProductStore } from '../../store/product.store';
import { CartStore } from '../../store/cart.store';
import { Product } from '../../models/product.model';
import { SeoService } from '../../services/seo.service';
import { ProductService } from '../../services/product.service';
import { AnalyticsService } from '../../services/analytics.service';
import { AuthService } from '../../services/auth.service';
import { FavouritesService } from '../../services/favourites.service';
import { LocalPricePipe } from '../../pipes/local-price.pipe';

@Component({
  selector: 'app-product-detail',
  imports: [RouterLink, CurrencyPipe, TitleCasePipe, DecimalPipe, LocalPricePipe],
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

  readonly product = computed(() =>
    this.productStore.products().find(p => p.id === this.id()) ?? this.fetchedProduct()
  );

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
      this.router.navigate(['/login'], { queryParams: { returnUrl: `/product/${productId}` } });
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
      const id = this.id();
      const inStore = this.productStore.products().find(p => p.id === id);
      if (!inStore && !this.fetchedProduct() && id) {
        this.productService.getById(id).subscribe({
          next: (p) => this.fetchedProduct.set(p ?? null),
        });
      }
    });
    effect(() => {
      const product = this.product();
      if (product) {
        this.seo.updateTags({
          title: product.name,
          description: product.description,
          url: `/product/${product.id}`,
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
        this.seo.setJsonLd({
          '@context': 'https://schema.org',
          '@type': 'Product',
          name: product.name,
          description: product.description,
          image: product.imageUrl,
          offers: {
            '@type': 'Offer',
            price: product.price,
            priceCurrency: 'GBP',
            availability: product.inStock
              ? 'https://schema.org/InStock'
              : 'https://schema.org/OutOfStock',
          },
        });
      }
    });
  }
}

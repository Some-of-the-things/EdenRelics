import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CurrencyPipe, DecimalPipe, TitleCasePipe } from '@angular/common';
import { ProductStore } from '../../store/product.store';
import { CartStore } from '../../store/cart.store';
import { SeoService } from '../../services/seo.service';
import { ProductService } from '../../services/product.service';
import { AnalyticsService } from '../../services/analytics.service';
import { AuthService } from '../../services/auth.service';
import { FavouritesService } from '../../services/favourites.service';

@Component({
  selector: 'app-product-detail',
  imports: [RouterLink, CurrencyPipe, TitleCasePipe, DecimalPipe],
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

  readonly selectedImage = signal<string | null>(null);

  readonly product = computed(() =>
    this.productStore.products().find(p => p.id === this.id())
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
    this.favourites.toggle(productId);
  }

  isFavourite(productId: string): boolean {
    return this.favourites.isFavourite(productId);
  }

  constructor() {
    if (this.auth.isAuthenticated()) {
      this.favourites.load();
    }
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
        this.productService.recordView(product.id).subscribe();
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

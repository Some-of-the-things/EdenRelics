import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, TitleCasePipe } from '@angular/common';
import { ProductStore } from '../../store/product.store';
import { CartStore } from '../../store/cart.store';
import { SeoService } from '../../services/seo.service';

@Component({
  selector: 'app-product-detail',
  imports: [RouterLink, CurrencyPipe, TitleCasePipe],
  templateUrl: './product-detail.component.html',
  styleUrl: './product-detail.component.scss',
})
export class ProductDetailComponent {
  readonly id = input.required<string>();
  private readonly productStore = inject(ProductStore);
  readonly cartStore = inject(CartStore);
  private readonly seo = inject(SeoService);

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

  constructor() {
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

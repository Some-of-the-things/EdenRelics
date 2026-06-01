import { ChangeDetectionStrategy, Component, computed, inject, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProductStore } from '../../store/product.store';
import { CartStore } from '../../store/cart.store';
import { Product } from '../../models/product.model';
import { CurrencyPipe, NgOptimizedImage, TitleCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { FavouritesService } from '../../services/favourites.service';
import { ProductService } from '../../services/product.service';
import { LocalPricePipe } from '../../pipes/local-price.pipe';

@Component({
  selector: 'app-product-list',
  imports: [RouterLink, CurrencyPipe, TitleCasePipe, FormsModule, NgOptimizedImage, LocalPricePipe],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductListComponent {
  readonly productStore = inject(ProductStore);
  private readonly cartStore = inject(CartStore);
  private readonly auth = inject(AuthService);
  private readonly favourites = inject(FavouritesService);
  private readonly productService = inject(ProductService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly platformId = inject(PLATFORM_ID);

  readonly showSalePrompt = signal(false);
  readonly cartInterestCounts = signal<Record<string, number>>({});
  readonly pageNumbers = computed(() => {
    const total = this.productStore.totalPages();
    return Array.from({ length: total }, (_, i) => i + 1);
  });
  private pendingFavouriteId: string | null = null;
  private sessionId = this.getSessionId();

  constructor() {
    if (this.auth.isAuthenticated()) {
      this.favourites.load();
    }
  }

  goToPage(page: number): void {
    const target = Math.min(Math.max(1, page), this.productStore.totalPages());
    if (target === this.productStore.currentPage()) {
      return;
    }
    this.productStore.setPage(target);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: target > 1 ? target : null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
    if (isPlatformBrowser(this.platformId)) {
      // Defer until the new page's items have rendered. Paging is client-side,
      // so setPage changes the layout on the next tick; scrolling synchronously
      // would target the old (taller) layout and the browser's scroll anchoring
      // then leaves the viewport at the bottom once the shorter page paints.
      setTimeout(() => {
        const grid = document.querySelector('.products__grid');
        if (grid) {
          grid.scrollIntoView({ behavior: 'smooth', block: 'start' });
        } else {
          window.scrollTo({ top: 0, behavior: 'smooth' });
        }
      });
    }
  }

  addToCart(product: Product): void {
    this.cartStore.addToCart(product);
    this.productService.addCartInterest(product.id, this.sessionId).subscribe({
      next: (res) => {
        this.cartInterestCounts.update(counts => ({ ...counts, [product.id]: res.count }));
      },
    });
  }

  getCartCount(productId: string): number {
    return this.cartInterestCounts()[productId] ?? 0;
  }

  private getSessionId(): string {
    const key = 'eden_session_id';
    let id = typeof localStorage !== 'undefined' ? localStorage.getItem(key) : null;
    if (!id) {
      id = Math.random().toString(36).slice(2) + Date.now().toString(36);
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(key, id);
      }
    }
    return id;
  }

  toggleFavourite(event: Event, productId: string): void {
    event.stopPropagation();
    event.preventDefault();
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: '/' } });
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
}

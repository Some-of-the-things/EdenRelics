import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ProductStore } from '../../store/product.store';
import { CartStore } from '../../store/cart.store';
import { Product } from '../../models/product.model';
import { CurrencyPipe, DecimalPipe, NgOptimizedImage, TitleCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { FavouritesService } from '../../services/favourites.service';

@Component({
  selector: 'app-product-list',
  imports: [RouterLink, CurrencyPipe, TitleCasePipe, FormsModule, NgOptimizedImage, DecimalPipe],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.scss',
})
export class ProductListComponent {
  readonly productStore = inject(ProductStore);
  private readonly cartStore = inject(CartStore);
  private readonly auth = inject(AuthService);
  private readonly favourites = inject(FavouritesService);
  private readonly router = inject(Router);

  constructor() {
    if (this.auth.isAuthenticated()) {
      this.favourites.load();
    }
  }

  addToCart(product: Product): void {
    this.cartStore.addToCart(product);
  }

  toggleFavourite(event: Event, productId: string): void {
    event.stopPropagation();
    if (!this.auth.isAuthenticated()) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: '/' } });
      return;
    }
    this.favourites.toggle(productId);
  }

  isFavourite(productId: string): boolean {
    return this.favourites.isFavourite(productId);
  }
}

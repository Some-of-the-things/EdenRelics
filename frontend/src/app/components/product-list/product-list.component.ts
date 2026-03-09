import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProductStore } from '../../store/product.store';
import { CartStore } from '../../store/cart.store';
import { Product } from '../../models/product.model';
import { CurrencyPipe, NgOptimizedImage, TitleCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-product-list',
  imports: [RouterLink, CurrencyPipe, TitleCasePipe, FormsModule, NgOptimizedImage],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.scss',
})
export class ProductListComponent {
  readonly productStore = inject(ProductStore);
  private readonly cartStore = inject(CartStore);

  addToCart(product: Product): void {
    this.cartStore.addToCart(product);
  }
}

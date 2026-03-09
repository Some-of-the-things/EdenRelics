import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CartStore } from '../../store/cart.store';
import { AuthService } from '../../services/auth.service';
import { OrderService } from '../../services/order.service';

@Component({
  selector: 'app-cart',
  imports: [RouterLink, CurrencyPipe, FormsModule],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.scss',
})
export class CartComponent {
  readonly cartStore = inject(CartStore);
  readonly auth = inject(AuthService);
  private readonly orderService = inject(OrderService);
  private readonly router = inject(Router);

  readonly showCheckoutOptions = signal(false);
  readonly showGuestForm = signal(false);
  readonly processing = signal(false);
  readonly error = signal('');
  guestEmail = '';

  beginCheckout(): void {
    if (this.auth.isAuthenticated()) {
      this.showCheckoutOptions.set(true);
    } else {
      this.showCheckoutOptions.set(true);
    }
  }

  checkoutAuthenticated(): void {
    this.placeOrder();
  }

  checkoutAsGuest(): void {
    this.placeOrder(this.guestEmail);
  }

  private placeOrder(guestEmail?: string): void {
    this.processing.set(true);
    this.error.set('');

    const items = this.cartStore.items().map(item => ({
      productId: item.product.id,
      quantity: item.quantity,
    }));

    this.orderService.checkout(items, guestEmail).subscribe({
      next: (order) => {
        this.cartStore.clearCart();
        this.router.navigate(['/order-confirmation', order.id]);
      },
      error: () => {
        this.processing.set(false);
        this.error.set('Checkout failed. Please try again.');
      },
    });
  }
}

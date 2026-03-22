import { Component, inject, signal, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CartStore } from '../../store/cart.store';
import { AuthService } from '../../services/auth.service';
import { OrderService, OrderAddress } from '../../services/order.service';

@Component({
  selector: 'app-cart',
  imports: [RouterLink, CurrencyPipe, FormsModule],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.scss',
})
export class CartComponent implements OnInit {
  readonly cartStore = inject(CartStore);
  readonly auth = inject(AuthService);
  private readonly orderService = inject(OrderService);
  private readonly router = inject(Router);

  readonly step = signal<'cart' | 'checkout'>('cart');
  readonly processing = signal(false);
  readonly error = signal('');

  guestEmail = '';
  shippingMethod = 'standard';
  billingSameAsShipping = true;

  shipping: OrderAddress = { addressLine1: '', city: '', postcode: '', country: 'United Kingdom' };
  billing: OrderAddress = { addressLine1: '', city: '', postcode: '', country: 'United Kingdom' };

  readonly shippingOptions = [
    { value: 'standard', label: 'Standard UK Delivery (3–5 days)', price: 3.95 },
    { value: 'express', label: 'Express UK Delivery (1–2 days)', price: 6.95 },
    { value: 'next-day', label: 'Next Day UK Delivery', price: 9.95 },
    { value: 'international', label: 'International Delivery (7–14 days)', price: 12.95 },
  ];

  get shippingCost(): number {
    return this.shippingOptions.find(o => o.value === this.shippingMethod)?.price ?? 3.95;
  }

  get orderTotal(): number {
    return this.cartStore.totalPrice() + this.shippingCost;
  }

  ngOnInit(): void {
    if (this.auth.isAuthenticated()) {
      this.auth.getProfile().subscribe({
        next: (profile) => {
          if (profile.deliveryAddress?.addressLine1) {
            this.shipping = {
              addressLine1: profile.deliveryAddress.addressLine1 ?? '',
              addressLine2: profile.deliveryAddress.addressLine2 ?? '',
              city: profile.deliveryAddress.city ?? '',
              county: profile.deliveryAddress.county ?? '',
              postcode: profile.deliveryAddress.postcode ?? '',
              country: profile.deliveryAddress.country || 'United Kingdom',
            };
          }
          if (profile.billingAddress?.addressLine1) {
            this.billingSameAsShipping = false;
            this.billing = {
              addressLine1: profile.billingAddress.addressLine1 ?? '',
              addressLine2: profile.billingAddress.addressLine2 ?? '',
              city: profile.billingAddress.city ?? '',
              county: profile.billingAddress.county ?? '',
              postcode: profile.billingAddress.postcode ?? '',
              country: profile.billingAddress.country || 'United Kingdom',
            };
          }
        },
      });
    }
  }

  beginCheckout(): void {
    this.step.set('checkout');
  }

  backToCart(): void {
    this.step.set('cart');
  }

  placeOrder(): void {
    this.processing.set(true);
    this.error.set('');

    const items = this.cartStore.items().map(item => ({
      productId: item.product.id,
      quantity: 1,
    }));

    this.orderService.checkout({
      items,
      guestEmail: this.auth.isAuthenticated() ? null : this.guestEmail || null,
      shippingAddress: this.shipping,
      billingAddress: this.billingSameAsShipping ? this.shipping : this.billing,
      shippingMethod: this.shippingMethod,
    }).subscribe({
      next: (res) => {
        this.cartStore.clearCart();
        window.location.href = res.checkoutUrl;
      },
      error: () => {
        this.processing.set(false);
        this.error.set('Checkout failed. Please try again.');
      },
    });
  }
}

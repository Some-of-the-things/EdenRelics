import { Component, inject, signal, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CartStore } from '../../store/cart.store';
import { AuthService } from '../../services/auth.service';
import { OrderService, OrderAddress } from '../../services/order.service';
import { ShippingService, ShippingCountry } from '../../services/shipping.service';
import { LocaleService } from '../../services/locale.service';

interface ShippingOption {
  value: string;
  label: string;
  price: number;
  estimate: string;
}

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
  private readonly shippingService = inject(ShippingService);
  private readonly localeService = inject(LocaleService);
  private readonly router = inject(Router);

  readonly step = signal<'cart' | 'checkout'>('cart');
  readonly processing = signal(false);
  readonly error = signal('');

  guestEmail = '';
  shippingMethod = 'standard';
  billingSameAsShipping = true;

  shipping: OrderAddress = { addressLine1: '', city: '', postcode: '', country: '' };
  billing: OrderAddress = { addressLine1: '', city: '', postcode: '', country: '' };

  countries: ShippingCountry[] = [];
  shippingOptions: ShippingOption[] = [
    { value: 'standard', label: 'Standard UK Delivery', price: 3.95, estimate: '3\u20135 working days' },
    { value: 'express', label: 'Express UK Delivery', price: 6.95, estimate: '1\u20132 working days' },
  ];

  get shippingCost(): number {
    return this.shippingOptions.find(o => o.value === this.shippingMethod)?.price ?? 3.95;
  }

  get orderTotal(): number {
    return this.cartStore.totalPrice() + this.shippingCost;
  }

  ngOnInit(): void {
    // Detect user's country and pre-select it
    const detectedCountry = this.localeService.locale().countryCode || 'GB';

    this.shippingService.getZones().subscribe(zones => {
      // Build unique country list from all zones (excluding UK-only duplicates)
      const seen = new Set<string>();
      this.countries = [];
      for (const zone of zones) {
        for (const c of zone.countries) {
          if (!seen.has(c.code)) {
            seen.add(c.code);
            this.countries.push(c);
          }
        }
      }
      this.countries.sort((a, b) => {
        if (a.code === 'GB') { return -1; }
        if (b.code === 'GB') { return 1; }
        return a.name.localeCompare(b.name);
      });

      // Pre-select detected country if supported, otherwise default to GB
      const supported = this.countries.find(c => c.code === detectedCountry);
      this.shipping.country = supported ? detectedCountry : 'GB';
      this.billing.country = this.shipping.country;
      this.onCountryChange();
    });

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
              country: profile.deliveryAddress.country || 'GB',
            };
            this.onCountryChange();
          }
          if (profile.billingAddress?.addressLine1) {
            this.billingSameAsShipping = false;
            this.billing = {
              addressLine1: profile.billingAddress.addressLine1 ?? '',
              addressLine2: profile.billingAddress.addressLine2 ?? '',
              city: profile.billingAddress.city ?? '',
              county: profile.billingAddress.county ?? '',
              postcode: profile.billingAddress.postcode ?? '',
              country: profile.billingAddress.country || 'GB',
            };
          }
        },
      });
    }
  }

  onCountryChange(): void {
    const country = this.shipping.country;
    if (country === 'GB') {
      this.shippingOptions = [
        { value: 'standard', label: 'Standard UK Delivery', price: 3.95, estimate: '3\u20135 working days' },
        { value: 'express', label: 'Express UK Delivery', price: 6.95, estimate: '1\u20132 working days' },
      ];
      if (this.shippingMethod !== 'standard' && this.shippingMethod !== 'express') {
        this.shippingMethod = 'standard';
      }
    } else {
      this.shippingService.getRate(country).subscribe({
        next: (rate) => {
          this.shippingOptions = [
            { value: rate.method, label: `${rate.label} Delivery`, price: rate.price, estimate: rate.deliveryEstimate },
          ];
          this.shippingMethod = rate.method;
        },
        error: () => {
          this.shippingOptions = [
            { value: 'international', label: 'International Delivery', price: 18.95, estimate: '10\u201321 working days' },
          ];
          this.shippingMethod = 'international';
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

import { Component, inject, signal, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CartStore } from '../../store/cart.store';
import { AuthService } from '../../services/auth.service';
import { OrderService, OrderAddress } from '../../services/order.service';
import { ShippingService, ShippingCountry } from '../../services/shipping.service';
import { LocaleService } from '../../services/locale.service';
import { ReviewTrustLineComponent } from '../reviews/review-trust-line.component';
import { CHRISTMAS_RETURNS_TEXT, christmasReturnsOpenForOrders } from '../../utils/christmas-returns';
import { SignupOfferService } from '../../services/signup-offer.service';
import { EarlyAccessService } from '../../services/early-access.service';

interface ShippingOption {
  value: string;
  label: string;
  price: number;
  estimate: string;
}

@Component({
  selector: 'app-cart',
  imports: [RouterLink, CurrencyPipe, FormsModule, ReviewTrustLineComponent],
  templateUrl: './cart.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './cart.component.scss',
})
export class CartComponent implements OnInit {
  readonly cartStore = inject(CartStore);
  readonly auth = inject(AuthService);
  private readonly orderService = inject(OrderService);
  private readonly shippingService = inject(ShippingService);
  private readonly localeService = inject(LocaleService);
  private readonly router = inject(Router);
  private readonly offers = inject(SignupOfferService);
  private readonly earlyAccess = inject(EarlyAccessService);

  readonly step = signal<'cart' | 'checkout'>('cart');
  readonly processing = signal(false);
  readonly error = signal('');

  guestEmail = '';
  shippingMethod = 'standard';
  billingSameAsShipping = true;

  // Gift option — limits match the backend (Order.GiftMessageMaxLength / GiftToMaxLength).
  readonly giftMessageMax = 200;
  readonly giftToMax = 40;
  isGift = false;
  giftMessage = '';
  giftTo = '';
  readonly christmasReturnsText = CHRISTMAS_RETURNS_TEXT;
  readonly showChristmasReturns = christmasReturnsOpenForOrders();

  shipping: OrderAddress = { addressLine1: '', city: '', postcode: '', country: '' };
  billing: OrderAddress = { addressLine1: '', city: '', postcode: '', country: '' };

  countries: ShippingCountry[] = [];
  shippingOptions: ShippingOption[] = [
    {
      value: 'standard',
      label: 'Standard UK Delivery',
      price: 0,
      estimate: '3\u20135 working days',
    },
    {
      value: 'express',
      label: 'Express UK Delivery',
      price: 6.95,
      estimate: '1\u20132 working days',
    },
  ];

  get shippingCost(): number {
    return this.shippingOptions.find((o) => o.value === this.shippingMethod)?.price ?? 0;
  }

  get orderTotal(): number {
    return Math.max(0, this.cartStore.totalPrice() - this.discountAmount()) + this.shippingCost;
  }

  // Sign-up discount code. Checked here so the saving shows before paying; the server checks it
  // again when the order is created, which is the check that counts.
  discountInput = '';
  readonly appliedCode = signal<string | null>(null);
  readonly discountAmount = signal(0);
  readonly discountLabel = signal('');
  readonly discountMessage = signal('');
  readonly checkingCode = signal(false);

  applyCode(): void {
    const code = this.discountInput.trim();
    if (!code || this.checkingCode()) {
      return;
    }
    const email = this.auth.isAuthenticated() ? null : this.guestEmail.trim();
    if (!this.auth.isAuthenticated() && !email) {
      this.discountMessage.set('Enter your email address above first: the code is tied to the address it was sent to.');
      return;
    }
    this.checkingCode.set(true);
    this.discountMessage.set('');
    const ids = this.cartStore.items().map((i) => i.product.id);
    this.offers.check(code, email, ids, this.isGift).subscribe({
      next: (r) => {
        this.checkingCode.set(false);
        if (r.valid && r.code) {
          this.appliedCode.set(r.code);
          this.discountAmount.set(r.discount);
          this.discountLabel.set(r.description ?? 'Discount');
        } else {
          this.removeCode();
          this.discountMessage.set(r.message ?? 'That code can\'t be used.');
        }
      },
      error: () => {
        this.checkingCode.set(false);
        this.discountMessage.set('The code could not be checked. Please try again.');
      },
    });
  }

  removeCode(): void {
    this.appliedCode.set(null);
    this.discountAmount.set(0);
    this.discountLabel.set('');
  }

  ngOnInit(): void {
    // Detect user's country and pre-select it
    const detectedCountry = this.localeService.locale().countryCode || 'GB';

    this.shippingService.getZones().subscribe((zones) => {
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
        if (a.code === 'GB') {
          return -1;
        }
        if (b.code === 'GB') {
          return 1;
        }
        return a.name.localeCompare(b.name);
      });

      // Pre-select detected country if supported, otherwise default to GB
      const supported = this.countries.find((c) => c.code === detectedCountry);
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
        {
          value: 'standard',
          label: 'Standard UK Delivery',
          price: 0,
          estimate: '3\u20135 working days',
        },
        {
          value: 'express',
          label: 'Express UK Delivery',
          price: 6.95,
          estimate: '1\u20132 working days',
        },
      ];
      if (this.shippingMethod !== 'standard' && this.shippingMethod !== 'express') {
        this.shippingMethod = 'standard';
      }
    } else {
      this.shippingService.getRate(country).subscribe({
        next: (rate) => {
          this.shippingOptions = [
            {
              value: rate.method,
              label: `${rate.label} Delivery`,
              price: rate.price,
              estimate: rate.deliveryEstimate,
            },
          ];
          this.shippingMethod = rate.method;
        },
        error: () => {
          this.shippingOptions = [
            {
              value: 'international',
              label: 'International Delivery',
              price: 18.95,
              estimate: '10\u201321 working days',
            },
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

    const items = this.cartStore.items().map((item) => ({
      productId: item.product.id,
      quantity: 1,
    }));

    this.orderService
      .checkout({
        items,
        guestEmail: this.auth.isAuthenticated() ? null : this.guestEmail || null,
        shippingAddress: this.shipping,
        billingAddress: this.billingSameAsShipping ? this.shipping : this.billing,
        shippingMethod: this.shippingMethod,
        // Unticking keeps whatever was typed in the (now hidden) fields; only send it for a gift.
        isGift: this.isGift,
        giftMessage: this.isGift ? this.giftMessage.trim() || null : null,
        giftTo: this.isGift ? this.giftTo.trim() || null : null,
        discountCode: this.appliedCode(),
        earlyAccessToken: this.earlyAccess.token(),
      })
      .subscribe({
        next: (res) => {
          this.cartStore.clearCart();
          window.location.href = res.checkoutUrl;
        },
        error: (e) => {
          this.processing.set(false);
          const message: unknown = e?.error?.message;
          // Code problems (wrong address, expired, gift order) are worth showing as they are.
          if (this.appliedCode() && typeof message === 'string' && /code/i.test(message)) {
            this.removeCode();
            this.discountMessage.set(message);
            this.error.set('Your code was removed: ' + message);
            return;
          }
          this.error.set('Checkout failed. Please try again.');
        },
      });
  }
}

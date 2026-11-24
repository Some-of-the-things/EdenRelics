import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Product } from '../../models/product.model';
import { ProductService } from '../../services/product.service';
import { NewsletterService } from '../../services/newsletter.service';
import { SeoService } from '../../services/seo.service';
import { AuthService } from '../../services/auth.service';
import { CartStore } from '../../store/cart.store';
import { imageSrcAt } from '../../utils/image-variant-loader';

/**
 * New pieces subscribers can see and buy before they go public. Reached from the link in an
 * early-access email (which carries the subscriber's token) or when signed in with a subscribed
 * account. Everyone else gets a short explanation and the sign-up box. Not indexed.
 */
@Component({
  selector: 'app-early-access-page',
  imports: [RouterLink, CurrencyPipe, DatePipe, FormsModule],
  templateUrl: './early-access-page.component.html',
  styleUrl: './early-access-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EarlyAccessPageComponent implements OnInit {
  private readonly products = inject(ProductService);
  private readonly newsletter = inject(NewsletterService);
  private readonly seo = inject(SeoService);
  readonly auth = inject(AuthService);
  readonly cart = inject(CartStore);

  readonly loading = signal(true);
  readonly access = signal(false);
  readonly pieces = signal<Product[]>([]);
  readonly failed = signal(false);

  email = '';
  readonly subscribing = signal(false);
  readonly subscribed = signal(false);
  readonly subscribeError = signal(false);

  readonly srcAt = imageSrcAt;

  ngOnInit(): void {
    this.seo.updateTags({ title: 'Early access', url: '/early-access', noIndex: true });
    this.products.getEarlyAccess().subscribe({
      next: (r) => {
        this.access.set(r.access);
        this.pieces.set(r.pieces);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  inCart(id: string): boolean {
    return this.cart.items().some((i) => i.product.id === id);
  }

  subscribe(): void {
    const email = this.email.trim();
    if (!email || this.subscribing()) {
      return;
    }
    this.subscribing.set(true);
    this.subscribeError.set(false);
    this.newsletter.subscribe(email, 'Early Access').subscribe({
      next: () => {
        this.subscribing.set(false);
        this.subscribed.set(true);
      },
      error: () => {
        this.subscribing.set(false);
        this.subscribeError.set(true);
      },
    });
  }
}

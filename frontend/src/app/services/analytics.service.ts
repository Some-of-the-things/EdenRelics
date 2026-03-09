import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

declare const gtag: (...args: unknown[]) => void;

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);
  private initialized = false;

  init(): void {
    if (this.initialized || !isPlatformBrowser(this.platformId)) return;
    this.initialized = true;

    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe((e) => {
        this.pageView((e as NavigationEnd).urlAfterRedirects);
      });
  }

  pageView(path: string): void {
    if (typeof gtag === 'undefined') return;
    gtag('event', 'page_view', { page_path: path });
  }

  event(name: string, params: Record<string, unknown> = {}): void {
    if (typeof gtag === 'undefined') return;
    gtag('event', name, params);
  }

  addToCart(productId: string, name: string, price: number): void {
    this.event('add_to_cart', {
      currency: 'GBP',
      value: price,
      items: [{ item_id: productId, item_name: name, price }],
    });
  }

  removeFromCart(productId: string, name: string, price: number): void {
    this.event('remove_from_cart', {
      currency: 'GBP',
      value: price,
      items: [{ item_id: productId, item_name: name, price }],
    });
  }

  viewProduct(productId: string, name: string, price: number): void {
    this.event('view_item', {
      currency: 'GBP',
      value: price,
      items: [{ item_id: productId, item_name: name, price }],
    });
  }

  purchase(orderId: string, total: number, items: { id: string; name: string; price: number }[]): void {
    this.event('purchase', {
      transaction_id: orderId,
      currency: 'GBP',
      value: total,
      items: items.map((i) => ({ item_id: i.id, item_name: i.name, price: i.price })),
    });
  }

  signup(method: string): void {
    this.event('sign_up', { method });
  }

  login(method: string): void {
    this.event('login', { method });
  }
}

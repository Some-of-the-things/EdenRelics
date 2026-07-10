import { Injectable, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

/** localStorage key recording that the discount popup has been resolved for this visitor. */
const STORAGE_KEY = 'eden_discount_popup';
/** Cumulative *active* seconds spent in shop areas before the popup fires. */
const THRESHOLD_SECONDS = 180;
/** No interaction for this long ⇒ treated as idle; active time stops accruing. */
const IDLE_MS = 30_000;
/** Routes that count as "actively shopping": /shop, /shop/:decade and /product/:id. */
const SHOP_ROUTE = /^\/(shop($|\/)|product\/)/;
/** Deliberate-interaction events — a parked tab with no input never accrues time. */
const INTERACTION_EVENTS = ['pointerdown', 'keydown', 'scroll', 'wheel', 'touchstart'] as const;

/**
 * Measures how long a visitor is *actively* browsing shop pages — accruing time
 * only while the tab is visible AND the visitor has interacted within the last
 * {@link IDLE_MS} — and flips {@link showPopup} once they cross the threshold.
 * Fires at most once per visitor (persisted in localStorage). Browser-only:
 * {@link init} must be called from the app root's afterNextRender.
 */
@Injectable({ providedIn: 'root' })
export class ShopEngagementService {
  private readonly router = inject(Router);

  /** Latches true once the visitor has actively browsed the shop past the threshold. */
  readonly showPopup = signal(false);

  private activeSeconds = 0;
  private lastInteractionAt = 0;
  private onShopRoute = false;
  private timer: ReturnType<typeof setInterval> | null = null;
  private started = false;
  private readonly mark = (): void => {
    this.lastInteractionAt = Date.now();
  };

  /** Call once, in the browser only (app root afterNextRender). No-op if already resolved. */
  init(): void {
    if (this.started || this.isResolved()) {
      return;
    }
    this.started = true;

    this.onShopRoute = SHOP_ROUTE.test(this.router.url.split('?')[0]);
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => {
        this.onShopRoute = SHOP_ROUTE.test(e.urlAfterRedirects.split('?')[0]);
      });

    for (const evt of INTERACTION_EVENTS) {
      window.addEventListener(evt, this.mark, { passive: true });
    }
    this.lastInteractionAt = Date.now();
    this.timer = setInterval(() => this.tick(), 1000);
  }

  /** Persist the outcome so the popup never fires again for this visitor. */
  resolve(state: 'dismissed' | 'subscribed'): void {
    this.stop();
    try {
      localStorage.setItem(STORAGE_KEY, state);
    } catch {
      /* private mode / storage disabled — non-fatal, popup just isn't suppressed next visit */
    }
  }

  private tick(): void {
    const visible = document.visibilityState === 'visible';
    const active = Date.now() - this.lastInteractionAt < IDLE_MS;
    if (this.onShopRoute && visible && active) {
      this.activeSeconds += 1;
      if (this.activeSeconds >= THRESHOLD_SECONDS) {
        this.showPopup.set(true);
        this.stop();
      }
    }
  }

  private isResolved(): boolean {
    try {
      return localStorage.getItem(STORAGE_KEY) !== null;
    } catch {
      return false;
    }
  }

  private stop(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
    for (const evt of INTERACTION_EVENTS) {
      window.removeEventListener(evt, this.mark);
    }
  }
}

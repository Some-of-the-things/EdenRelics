import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

/** One curated pick: the product (by globally-unique ID) and whether it shows on the homepage strip. */
export interface TopPickItem {
  productId: string;
  featured: boolean;
}

interface TopPicksPublic {
  enabled: boolean;
  productIds: string[];
  featuredProductIds: string[];
}

export interface TopPicksAdmin {
  enabled: boolean;
  items: TopPickItem[];
}

/**
 * The curated "Our Top Picks" edit. Independent of the marketplace: it's a hand-picked selection of
 * live products (house or, once the marketplace is live, any seller's), gated by its own
 * `TopPicks:Enabled` flag. This fetches the public payload once (the gate flag + curated product IDs)
 * and caches it; the gated surfaces — the homepage strip, the /top-picks page and the nav link —
 * read `enabled()`, and resolve `productIds()`/`featuredProductIds()` against the product store.
 * Fails closed: if the payload can't be read, Top Picks is treated as off. The admin methods drive
 * the curation UI and are unaffected by the gate.
 */
@Injectable({ providedIn: 'root' })
export class TopPicksService {
  private readonly http = inject(HttpClient);

  private readonly _enabled = signal(false);
  private readonly _productIds = signal<string[]>([]);
  private readonly _featuredProductIds = signal<string[]>([]);
  readonly enabled = this._enabled.asReadonly();
  readonly productIds = this._productIds.asReadonly();
  readonly featuredProductIds = this._featuredProductIds.asReadonly();

  private loadPromise: Promise<boolean> | null = null;

  /** Fetches the public payload once (cached). Awaitable — used by the route guard on server + browser. */
  ensureLoaded(): Promise<boolean> {
    if (!this.loadPromise) {
      this.loadPromise = firstValueFrom(
        this.http.get<TopPicksPublic>(`${environment.apiUrl}/api/top-picks`),
      )
        .then((r) => {
          this._enabled.set(!!r?.enabled);
          this._productIds.set(r?.productIds ?? []);
          this._featuredProductIds.set(r?.featuredProductIds ?? []);
          return this._enabled();
        })
        .catch(() => {
          this._enabled.set(false);
          this._productIds.set([]);
          this._featuredProductIds.set([]);
          return false;
        });
    }
    return this.loadPromise;
  }

  /** Fire-and-forget load for components that only read the signals reactively. */
  load(): void {
    void this.ensureLoaded();
  }

  /** Admin: the full curated list (regardless of the gate) plus the current flag. */
  getAdmin(): Observable<TopPicksAdmin> {
    return this.http.get<TopPicksAdmin>(`${environment.apiUrl}/api/top-picks/admin`);
  }

  /** Admin: replace the whole curated list, in display order. */
  save(items: TopPickItem[]): Observable<TopPicksAdmin> {
    return this.http.put<TopPicksAdmin>(`${environment.apiUrl}/api/top-picks/admin`, { items });
  }
}

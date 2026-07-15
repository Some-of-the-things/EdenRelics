import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

/**
 * Exposes whether the multi-seller marketplace ("multi-user" mode) is live. The backend keeps the
 * master switch (`Marketplace:Enabled`); this fetches the public `/status` flag once and caches it.
 * Gated surfaces — the "Our Top Picks" homepage section and /top-picks page — read `enabled()`.
 * Fails closed: if the flag can't be read, the marketplace is treated as off.
 */
@Injectable({ providedIn: 'root' })
export class MarketplaceService {
  private readonly http = inject(HttpClient);

  private readonly _enabled = signal(false);
  readonly enabled = this._enabled.asReadonly();

  private loadPromise: Promise<boolean> | null = null;

  /** Fetches the flag once (cached). Awaitable — used by the route guard on server and browser. */
  ensureLoaded(): Promise<boolean> {
    if (!this.loadPromise) {
      this.loadPromise = firstValueFrom(
        this.http.get<{ enabled: boolean }>(`${environment.apiUrl}/api/marketplace/status`),
      )
        .then((r) => {
          this._enabled.set(!!r?.enabled);
          return this._enabled();
        })
        .catch(() => {
          this._enabled.set(false);
          return false;
        });
    }
    return this.loadPromise;
  }

  /** Fire-and-forget load for components that only read the `enabled()` signal reactively. */
  load(): void {
    void this.ensureLoaded();
  }
}

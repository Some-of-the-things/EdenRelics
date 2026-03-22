import { Injectable, inject, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

interface FavouriteEntry {
  productId: string;
  notifyOnSale: boolean;
}

@Injectable({ providedIn: 'root' })
export class FavouritesService {
  private readonly http = inject(HttpClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly apiUrl = `${environment.apiUrl}/api/favourites`;

  readonly favouriteIds = signal<Set<string>>(new Set());
  readonly notifyOnSaleIds = signal<Set<string>>(new Set());
  private loaded = false;

  load(): void {
    if (!isPlatformBrowser(this.platformId) || this.loaded) {
      return;
    }
    this.loaded = true;
    this.http.get<FavouriteEntry[]>(this.apiUrl).subscribe({
      next: (entries) => {
        this.favouriteIds.set(new Set(entries.map(e => e.productId)));
        this.notifyOnSaleIds.set(new Set(entries.filter(e => e.notifyOnSale).map(e => e.productId)));
      },
      error: () => {},
    });
  }

  isFavourite(productId: string): boolean {
    return this.favouriteIds().has(productId);
  }

  add(productId: string, notifyOnSale: boolean): void {
    this.favouriteIds.update(s => new Set(s).add(productId));
    if (notifyOnSale) {
      this.notifyOnSaleIds.update(s => new Set(s).add(productId));
    }
    this.http.post(`${this.apiUrl}/${productId}`, { notifyOnSale }).subscribe({ error: () => {
      this.favouriteIds.update(s => { const n = new Set(s); n.delete(productId); return n; });
      this.notifyOnSaleIds.update(s => { const n = new Set(s); n.delete(productId); return n; });
    }});
  }

  remove(productId: string): void {
    this.favouriteIds.update(s => { const n = new Set(s); n.delete(productId); return n; });
    this.notifyOnSaleIds.update(s => { const n = new Set(s); n.delete(productId); return n; });
    this.http.delete(`${this.apiUrl}/${productId}`).subscribe({ error: () => {
      this.favouriteIds.update(s => new Set(s).add(productId));
    }});
  }

  reset(): void {
    this.favouriteIds.set(new Set());
    this.notifyOnSaleIds.set(new Set());
    this.loaded = false;
  }
}

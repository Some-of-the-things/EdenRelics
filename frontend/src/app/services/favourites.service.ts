import { Injectable, inject, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class FavouritesService {
  private readonly http = inject(HttpClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly apiUrl = `${environment.apiUrl}/api/favourites`;

  readonly favouriteIds = signal<Set<string>>(new Set());
  private loaded = false;

  load(): void {
    if (!isPlatformBrowser(this.platformId) || this.loaded) {
      return;
    }
    this.loaded = true;
    this.http.get<string[]>(this.apiUrl).subscribe({
      next: (ids) => this.favouriteIds.set(new Set(ids)),
      error: () => {},
    });
  }

  isFavourite(productId: string): boolean {
    return this.favouriteIds().has(productId);
  }

  toggle(productId: string): void {
    if (this.isFavourite(productId)) {
      this.favouriteIds.update(s => { const n = new Set(s); n.delete(productId); return n; });
      this.http.delete(`${this.apiUrl}/${productId}`).subscribe({ error: () => {
        this.favouriteIds.update(s => new Set(s).add(productId));
      }});
    } else {
      this.favouriteIds.update(s => new Set(s).add(productId));
      this.http.post(`${this.apiUrl}/${productId}`, {}).subscribe({ error: () => {
        this.favouriteIds.update(s => { const n = new Set(s); n.delete(productId); return n; });
      }});
    }
  }

  reset(): void {
    this.favouriteIds.set(new Set());
    this.loaded = false;
  }
}

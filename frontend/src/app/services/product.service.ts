import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, catchError } from 'rxjs';
import { Product } from '../models/product.model';
import { environment } from '../../environments/environment';
import { LocaleService } from './locale.service';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly http = inject(HttpClient);
  private readonly localeService = inject(LocaleService);
  private readonly apiUrl = `${environment.apiUrl}/api/products`;

  private get localeParam(): string {
    const lang = this.localeService.locale().locale.split('-')[0];
    return lang && lang !== 'en' ? `?locale=${lang}` : '';
  }

  getAll(): Observable<Product[]> {
    return this.http.get<Product[]>(`${this.apiUrl}${this.localeParam}`).pipe(
      catchError((err) => {
        console.error('Failed to load products from API:', err);
        return of([]);
      })
    );
  }

  getById(id: string): Observable<Product | undefined> {
    return this.http.get<Product>(`${this.apiUrl}/${id}${this.localeParam}`).pipe(
      catchError((err) => {
        console.error(`Failed to load product ${id} from API:`, err);
        return of(undefined);
      })
    );
  }

  getBySlug(slug: string): Observable<Product | undefined> {
    return this.http.get<Product>(`${this.apiUrl}/by-slug/${encodeURIComponent(slug)}${this.localeParam}`).pipe(
      catchError(() => of(undefined))
    );
  }

  add(product: Omit<Product, 'id'>): Observable<Product> {
    return this.http.post<Product>(this.apiUrl, product);
  }

  update(id: string, changes: Partial<Omit<Product, 'id'>>): Observable<Product> {
    return this.http.put<Product>(`${this.apiUrl}/${id}`, changes);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  recordView(id: string, meta?: { referrer?: string; utmSource?: string; utmMedium?: string; utmCampaign?: string; screenResolution?: string }): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/view`, meta ?? {});
  }

  uploadImage(file: File): Observable<{ imageUrl: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ imageUrl: string }>(`${this.apiUrl}/upload-image`, formData);
  }

  addCartInterest(productId: string, sessionId: string): Observable<{ count: number }> {
    return this.http.post<{ count: number }>(`${this.apiUrl}/${productId}/cart-interest`, { sessionId });
  }

  removeCartInterest(productId: string, sessionId: string): Observable<{ count: number }> {
    return this.http.delete<{ count: number }>(`${this.apiUrl}/${productId}/cart-interest?sessionId=${sessionId}`);
  }

  getCartInterest(productId: string): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.apiUrl}/${productId}/cart-interest`);
  }

  uploadVideo(file: File): Observable<{ videoUrl: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ videoUrl: string }>(`${this.apiUrl}/upload-video`, formData);
  }
}

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, catchError } from 'rxjs';
import { Product } from '../models/product.model';
import { environment } from '../../environments/environment';
import { LocaleService } from './locale.service';

const MOCK_PRODUCTS: Product[] = [
  {
    id: '1',
    name: 'Bohemian Maxi Dress',
    description: 'Flowing 1970s bohemian maxi dress with earthy floral print. Empire waist and angel sleeves in lightweight cotton gauze.',
    price: 195,
    era: '1970s',
    category: '70s',
    size: '10',
    condition: 'good',
    imageUrl: 'https://placehold.co/400x500/FF6347/FFF?text=Boho+Maxi+Dress',
    inStock: true,
  },
  {
    id: '2',
    name: 'Wrap Dress',
    description: 'Iconic 1970s wrap dress in a bold geometric print. Flattering silhouette with tie waist and flutter sleeves.',
    price: 275,
    era: '1970s',
    category: '70s',
    size: '12',
    condition: 'excellent',
    imageUrl: 'https://placehold.co/400x500/556B2F/FFF?text=Wrap+Dress',
    inStock: true,
  },
  {
    id: '3',
    name: 'Power Shoulder Dress',
    description: 'Bold 1980s power dress in electric blue with structured shoulders and nipped waist. Gold button details down the front.',
    price: 185,
    era: '1980s',
    category: '80s',
    size: '8',
    condition: 'excellent',
    imageUrl: 'https://placehold.co/400x500/191970/FFF?text=Power+Dress',
    inStock: true,
  },
  {
    id: '4',
    name: 'Sequin Party Dress',
    description: 'Dazzling 1980s sequin mini dress in hot pink. All-over sequin embellishment with dramatic puff sleeves.',
    price: 220,
    era: '1980s',
    category: '80s',
    size: '6',
    condition: 'good',
    imageUrl: 'https://placehold.co/400x500/8B0000/FFF?text=Sequin+Dress',
    inStock: true,
  },
  {
    id: '5',
    name: 'Silk Slip Dress',
    description: 'Minimalist 1990s silk slip dress in champagne. Bias-cut with delicate spaghetti straps and lace trim at the hem.',
    price: 210,
    era: '1990s',
    category: '90s',
    size: '8',
    condition: 'mint',
    imageUrl: 'https://placehold.co/400x500/DAA520/FFF?text=Silk+Slip+Dress',
    inStock: true,
  },
  {
    id: '6',
    name: 'Grunge Babydoll Dress',
    description: 'Classic 1990s babydoll dress in dark floral. Oversized fit with empire waist and velvet ribbon trim.',
    price: 145,
    era: '1990s',
    category: '90s',
    size: '14',
    condition: 'good',
    imageUrl: 'https://placehold.co/400x500/2F4F4F/FFF?text=Babydoll+Dress',
    inStock: true,
  },
  {
    id: '7',
    name: 'Butterfly Halter Dress',
    description: 'Early 2000s halter dress with butterfly print. Low-rise fit with handkerchief hem and rhinestone buckle detail.',
    price: 165,
    era: '2000s',
    category: 'y2k',
    size: '6',
    condition: 'excellent',
    imageUrl: 'https://placehold.co/400x500/FF69B4/FFF?text=Y2K+Halter',
    inStock: true,
  },
  {
    id: '8',
    name: 'Velvet Mini Dress',
    description: 'Y2K velvet mini dress in deep plum. Scooped neckline with ruched sides and subtle stretch for a perfect fit.',
    price: 135,
    era: '2000s',
    category: 'y2k',
    size: '10',
    condition: 'excellent',
    imageUrl: 'https://placehold.co/400x500/8B4513/FFF?text=Velvet+Mini',
    inStock: true,
  },
  {
    id: '9',
    name: 'Asymmetric Midi Dress',
    description: 'Contemporary asymmetric midi dress in sage green. One-shoulder design with pleated skirt and clean modern lines.',
    price: 285,
    era: '2020s',
    category: 'modern',
    size: '12',
    condition: 'mint',
    imageUrl: 'https://placehold.co/400x500/556B2F/FFF?text=Asymmetric+Midi',
    inStock: true,
  },
  {
    id: '10',
    name: 'Cut-Out Maxi Dress',
    description: 'Modern cut-out maxi dress in black. Strategic side cut-outs with a high neck and flowing skirt.',
    price: 320,
    era: '2020s',
    category: 'modern',
    size: '16',
    condition: 'mint',
    imageUrl: 'https://placehold.co/400x500/1C1C1C/FFF?text=Cut-Out+Maxi',
    inStock: true,
  },
];

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
      catchError(() => of(MOCK_PRODUCTS))
    );
  }

  getById(id: string): Observable<Product | undefined> {
    return this.http.get<Product>(`${this.apiUrl}/${id}${this.localeParam}`).pipe(
      catchError(() => of(MOCK_PRODUCTS.find(p => p.id === id)))
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
}

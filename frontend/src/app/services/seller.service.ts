import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface Seller {
  id: string;
  businessName: string;
  slug: string;
  bio?: string;
  logoUrl?: string;
  contactEmail?: string;
  approvalStatus: string;
  isHouse: boolean;
  connectOnboardingComplete: boolean;
  createdAtUtc: string;
}

export interface SellerApplication {
  businessName: string;
  slug?: string;
  bio?: string;
  contactEmail?: string;
  logoUrl?: string;
}

export interface SellerListing {
  id: string;
  name: string;
  slug: string;
  sku: string;
  price: number;
  era: string;
  category: string;
  size: string;
  condition: string;
  imageUrl: string;
  status: string;
  moderationStatus: string;
  moderationNote?: string;
  sellerId: string;
  createdAtUtc: string;
}

export interface SellerListingCreate {
  name: string;
  description: string;
  price: number;
  era: string;
  category: string;
  size: string;
  condition: string;
  imageUrl: string;
  additionalImageUrls?: string[];
  material?: string;
}

export interface SellerProductCard {
  id: string;
  name: string;
  slug: string;
  price: number;
  salePrice?: number;
  imageUrl: string;
  era: string;
  category: string;
  size: string;
  condition: string;
}

/** Client for the multi-seller marketplace API. All endpoints are gated server-side: while the
 * marketplace is switched off the seller-facing calls return 404. */
@Injectable({ providedIn: 'root' })
export class SellerService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api`;

  // --- Seller-facing ---
  apply(dto: SellerApplication): Observable<Seller> {
    return this.http.post<Seller>(`${this.base}/sellers/apply`, dto);
  }
  mySeller(): Observable<Seller> {
    return this.http.get<Seller>(`${this.base}/sellers/me`);
  }
  createListing(dto: SellerListingCreate): Observable<SellerListing> {
    return this.http.post<SellerListing>(`${this.base}/seller-listings`, dto);
  }
  myListings(): Observable<SellerListing[]> {
    return this.http.get<SellerListing[]>(`${this.base}/seller-listings/mine`);
  }

  // --- Stripe Connect onboarding ---
  connectStart(): Observable<{ url: string }> {
    return this.http.post<{ url: string }>(`${this.base}/sellers/connect/start`, {});
  }
  connectRefresh(): Observable<{ onboardingComplete: boolean }> {
    return this.http.post<{ onboardingComplete: boolean }>(`${this.base}/sellers/connect/refresh`, {});
  }

  // --- Public profile ---
  publicProfile(slug: string): Observable<Seller> {
    return this.http.get<Seller>(`${this.base}/sellers/${slug}`);
  }
  publicProducts(slug: string): Observable<SellerProductCard[]> {
    return this.http.get<SellerProductCard[]>(`${this.base}/sellers/${slug}/products`);
  }

  // --- Admin moderation ---
  listSellers(status?: string): Observable<Seller[]> {
    const q = status ? `?status=${status}` : '';
    return this.http.get<Seller[]>(`${this.base}/sellers/admin/all${q}`);
  }
  approveSeller(id: string): Observable<Seller> {
    return this.http.post<Seller>(`${this.base}/sellers/admin/${id}/approve`, {});
  }
  rejectSeller(id: string, note?: string): Observable<Seller> {
    return this.http.post<Seller>(`${this.base}/sellers/admin/${id}/reject`, { note });
  }
  suspendSeller(id: string, note?: string): Observable<Seller> {
    return this.http.post<Seller>(`${this.base}/sellers/admin/${id}/suspend`, { note });
  }
  moderationQueue(status = 'PendingReview'): Observable<SellerListing[]> {
    return this.http.get<SellerListing[]>(`${this.base}/seller-listings/admin?status=${status}`);
  }
  approveListing(id: string): Observable<SellerListing> {
    return this.http.post<SellerListing>(`${this.base}/seller-listings/admin/${id}/approve`, {});
  }
  rejectListing(id: string, note?: string): Observable<SellerListing> {
    return this.http.post<SellerListing>(`${this.base}/seller-listings/admin/${id}/reject`, { note });
  }
}

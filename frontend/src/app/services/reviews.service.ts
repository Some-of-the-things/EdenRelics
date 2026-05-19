import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

export interface PublicReview {
  id: string;
  authorDisplayName: string;
  transactionRating: number;
  deliveryRating: number;
  productRating: number;
  comment: string;
  postedAtUtc: string;
}

export interface ReviewSummary {
  count: number;
  overall: number;
  transaction: number;
  delivery: number;
  product: number;
}

export interface EligibleOrder {
  orderId: string;
  placedAtUtc: string;
  total: number;
  productNames: string[];
}

export interface MyReview {
  id: string;
  orderId: string;
  transactionRating: number;
  deliveryRating: number;
  productRating: number;
  comment: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  createdAtUtc: string;
  moderationNote?: string | null;
}

export interface SubmitReviewPayload {
  orderId: string;
  transactionRating: number;
  deliveryRating: number;
  productRating: number;
  comment: string;
  authorDisplayName?: string;
}

export interface AdminReview extends MyReview {
  userId: string;
  userEmail: string;
  authorDisplayName: string;
  moderatedAtUtc?: string | null;
  orderTotal: number;
  productNames: string[];
}

@Injectable({ providedIn: 'root' })
export class ReviewsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly url = `${environment.apiUrl}/api/reviews`;

  private authHeaders() {
    return { headers: { Authorization: `Bearer ${this.auth.getToken()}` } };
  }

  getPublic(take = 12): Observable<PublicReview[]> {
    return this.http.get<PublicReview[]>(`${this.url}/public?take=${take}`);
  }

  getSummary(): Observable<ReviewSummary> {
    return this.http.get<ReviewSummary>(`${this.url}/public/summary`);
  }

  getEligible(): Observable<EligibleOrder[]> {
    return this.http.get<EligibleOrder[]>(`${this.url}/eligible`, this.authHeaders());
  }

  getMine(): Observable<MyReview[]> {
    return this.http.get<MyReview[]>(`${this.url}/me`, this.authHeaders());
  }

  submit(payload: SubmitReviewPayload): Observable<MyReview> {
    return this.http.post<MyReview>(this.url, payload, this.authHeaders());
  }

  // Admin
  getAdmin(status?: 'Pending' | 'Approved' | 'Rejected'): Observable<AdminReview[]> {
    const qs = status ? `?status=${status}` : '';
    return this.http.get<AdminReview[]>(`${this.url}/admin${qs}`, this.authHeaders());
  }

  approve(id: string, note?: string): Observable<AdminReview> {
    return this.http.post<AdminReview>(`${this.url}/admin/${id}/approve`, { note: note ?? null }, this.authHeaders());
  }

  reject(id: string, note?: string): Observable<AdminReview> {
    return this.http.post<AdminReview>(`${this.url}/admin/${id}/reject`, { note: note ?? null }, this.authHeaders());
  }
}

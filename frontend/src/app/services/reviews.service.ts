import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/** A published review of a website order ("Verified purchase"). */
export interface PublicReview {
  id: string;
  authorDisplayName: string;
  rating: number;
  comment: string;
  photoUrl: string | null;
  reply: string | null;
  repliedAtUtc: string | null;
  productNames: string[];
  postedAtUtc: string;
}

export interface ReviewSummary {
  count: number;
  overall: number;
}

export interface EligibleOrder {
  orderId: string;
  placedAtUtc: string;
  total: number;
  productNames: string[];
}

export type ReviewStatus = 'Pending' | 'Approved' | 'Rejected';

export interface MyReview {
  id: string;
  orderId: string;
  rating: number;
  comment: string;
  status: ReviewStatus;
  createdAtUtc: string;
  moderationNote?: string | null;
}

export interface ReviewInviteItem {
  productId: string;
  name: string;
  imageUrl: string | null;
  slug: string | null;
}

export interface ReviewInvite {
  orderId: string;
  placedAtUtc: string;
  items: ReviewInviteItem[];
  firstName: string | null;
  lastName: string | null;
  alreadyReviewed: boolean;
}

export type NameStyle = 'first-initial' | 'first' | 'anonymous';

export interface SubmitReviewPayload {
  token: string | null;
  rating: number;
  comment: string;
  firstName: string;
  lastName: string;
  nameStyle: NameStyle;
  photoUrl: string | null;
}

export type HideReason = 'abuse' | 'personal-data' | 'spam' | 'not-about-purchase';

export const HIDE_REASONS: { value: HideReason; label: string }[] = [
  { value: 'abuse', label: 'Abusive or offensive' },
  { value: 'personal-data', label: 'Contains personal data' },
  { value: 'spam', label: 'Spam or advertising' },
  { value: 'not-about-purchase', label: 'Not about the purchase' },
];

export interface AdminReview {
  id: string;
  orderId: string;
  userId: string | null;
  userEmail: string;
  authorDisplayName: string;
  rating: number;
  comment: string;
  photoUrl: string | null;
  status: ReviewStatus;
  holdReason: string | null;
  hiddenReason: HideReason | null;
  reply: string | null;
  repliedAtUtc: string | null;
  createdAtUtc: string;
  moderatedAtUtc: string | null;
  moderationNote: string | null;
  orderTotal: number;
  productNames: string[];
}

@Injectable({ providedIn: 'root' })
export class ReviewsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/api/reviews`;

  getPublic(take = 12): Observable<PublicReview[]> {
    return this.http.get<PublicReview[]>(`${this.url}/public?take=${take}`);
  }

  getForProduct(productId: string): Observable<PublicReview[]> {
    return this.http.get<PublicReview[]>(`${this.url}/public/product/${productId}`);
  }

  getSummary(): Observable<ReviewSummary> {
    return this.http.get<ReviewSummary>(`${this.url}/public/summary`);
  }

  getEligible(): Observable<EligibleOrder[]> {
    return this.http.get<EligibleOrder[]>(`${this.url}/eligible`);
  }

  getMine(): Observable<MyReview[]> {
    return this.http.get<MyReview[]>(`${this.url}/me`);
  }

  getInvite(orderId: string, token: string | null): Observable<ReviewInvite> {
    const qs = token ? `?t=${encodeURIComponent(token)}` : '';
    return this.http.get<ReviewInvite>(`${this.url}/order/${orderId}${qs}`);
  }

  submit(orderId: string, payload: SubmitReviewPayload): Observable<MyReview> {
    return this.http.post<MyReview>(`${this.url}/order/${orderId}`, payload);
  }

  uploadPhoto(orderId: string, token: string | null, file: File): Observable<{ url: string }> {
    const form = new FormData();
    form.append('file', file);
    const qs = token ? `?t=${encodeURIComponent(token)}` : '';
    return this.http.post<{ url: string }>(`${this.url}/order/${orderId}/photo${qs}`, form);
  }

  // Admin
  getAdmin(status?: ReviewStatus): Observable<AdminReview[]> {
    const qs = status ? `?status=${status}` : '';
    return this.http.get<AdminReview[]>(`${this.url}/admin${qs}`);
  }

  publish(id: string): Observable<AdminReview> {
    return this.http.post<AdminReview>(`${this.url}/admin/${id}/publish`, {});
  }

  hide(id: string, reason: HideReason, note: string): Observable<AdminReview> {
    return this.http.post<AdminReview>(`${this.url}/admin/${id}/hide`, { reason, note });
  }

  reply(id: string, reply: string): Observable<AdminReview> {
    return this.http.post<AdminReview>(`${this.url}/admin/${id}/reply`, { reply });
  }

  getSettings(): Observable<{ requestDelayDays: number }> {
    return this.http.get<{ requestDelayDays: number }>(`${this.url}/admin/settings`);
  }

  saveSettings(requestDelayDays: number): Observable<{ requestDelayDays: number }> {
    return this.http.put<{ requestDelayDays: number }>(`${this.url}/admin/settings`, { requestDelayDays });
  }
}

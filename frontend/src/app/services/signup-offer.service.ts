import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, of, shareReplay } from 'rxjs';
import { environment } from '../../environments/environment';

/** What the sign-up boxes say. */
export interface PublicSignupOffer {
  discountOn: boolean;
  headline: string | null;
  terms: string | null;
  earlyAccessLine: string | null;
}

export interface DiscountCheckResult {
  valid: boolean;
  discount: number;
  message: string | null;
  code: string | null;
  description: string | null;
}

export interface SignupOfferSettings {
  enabled: boolean;
  kind: 'percent' | 'fixed';
  amount: number;
  minOrder: number | null;
  validDays: number;
  signupText: string | null;
  excludeReduced: boolean;
  excludeGiftOrders: boolean;
  showEarlyAccessLine: boolean;
}

export interface AdminDiscountCode {
  id: string;
  code: string;
  email: string;
  description: string;
  issuedAtUtc: string;
  expiresAtUtc: string;
  usedAtUtc: string | null;
  orderId: string | null;
  status: 'used' | 'unused' | 'expired';
}

export interface AdminDiscountCodes {
  issued: number;
  used: number;
  unused: number;
  expired: number;
  codes: AdminDiscountCode[];
}

const NO_OFFER: PublicSignupOffer = { discountOn: false, headline: null, terms: null, earlyAccessLine: null };

@Injectable({ providedIn: 'root' })
export class SignupOfferService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/discounts`;
  private offer$?: Observable<PublicSignupOffer>;

  /** The current offer, fetched once per page load. A failure means "no offer", never an error on the page. */
  getOffer(): Observable<PublicSignupOffer> {
    this.offer$ ??= this.http.get<PublicSignupOffer>(`${this.base}/signup-offer`).pipe(
      catchError(() => of(NO_OFFER)),
      shareReplay(1),
    );
    return this.offer$;
  }

  check(code: string, email: string | null, productIds: string[], isGift: boolean): Observable<DiscountCheckResult> {
    return this.http.post<DiscountCheckResult>(`${this.base}/check`, { code, email, productIds, isGift });
  }

  getSettings(): Observable<SignupOfferSettings> {
    return this.http.get<SignupOfferSettings>(`${this.base}/admin/settings`);
  }

  saveSettings(settings: SignupOfferSettings): Observable<SignupOfferSettings> {
    return this.http.put<SignupOfferSettings>(`${this.base}/admin/settings`, settings);
  }

  getCodes(status: string | null): Observable<AdminDiscountCodes> {
    const q = status ? `?status=${encodeURIComponent(status)}` : '';
    return this.http.get<AdminDiscountCodes>(`${this.base}/admin/codes${q}`);
  }
}

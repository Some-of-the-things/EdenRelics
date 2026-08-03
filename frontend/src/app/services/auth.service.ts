import { Injectable, inject, signal, computed, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface UserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  emailVerified: boolean;
}

export interface AddressDto {
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  county: string | null;
  postcode: string | null;
  country: string | null;
}

export interface PaymentInfoDto {
  cardholderName: string | null;
  cardLast4: string | null;
  cardBrand: string | null;
  expiryMonth: number | null;
  expiryYear: number | null;
}

export interface AccountProfileDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  deliveryAddress: AddressDto;
  billingAddress: AddressDto;
  payment: PaymentInfoDto | null;
  mfaEnabled: boolean;
  emailVerified: boolean;
}

export interface AuthResponse {
  token: string;
  user: UserDto;
}

export interface MfaRequiredResponse {
  mfaRequired: true;
  mfaToken: string;
}

export interface MfaSetupResponse {
  secret: string;
  qrUri: string;
}

export type LoginResponse = AuthResponse | MfaRequiredResponse;

/**
 * How long past expiry the server will still renew a token — mirrors the 30-day window in
 * AuthController.Refresh. Past this there is nothing to recover, so the session is cleared.
 */
const REFRESH_GRACE_MS = 30 * 24 * 60 * 60 * 1000;

/**
 * Reads `exp` (seconds since epoch) out of a JWT payload, as milliseconds. Null when the token is
 * absent or unreadable — treated the same as gone, since an unparseable token cannot be renewed.
 * This only reads the claim; it does not verify the signature, which is the server's job.
 */
function tokenExpiry(token: string | null): number | null {
  if (!token) {
    return null;
  }
  try {
    const payload: unknown = JSON.parse(atob(token.split('.')[1]));
    const exp = (payload as { exp?: unknown }).exp;
    return typeof exp === 'number' ? exp * 1000 : null;
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly apiUrl = `${environment.apiUrl}/api/auth`;

  readonly currentUser = signal<UserDto | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly isAdmin = computed(() => this.currentUser()?.role === 'Admin');
  readonly isSeller = computed(() => this.currentUser()?.role === 'Seller');

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.restoreSession();
    }
  }

  /**
   * Restores the stored session, but only if the token can still be turned into a working one.
   *
   * The signed-in state used to be restored from `eden_user` alone, without ever looking at the
   * token. Renewal is purely reactive — the interceptor refreshes on a 401 — so a session that
   * makes no authenticated calls (browsing public pages) is never renewed and never reaped. One
   * was found on 2026-08-03 that had been expired since 8 July: the UI showed signed in for
   * nearly four weeks, and four days later it would have crossed the server's 30-day refresh
   * cliff and started failing outright with no explanation.
   */
  private restoreSession(): void {
    const stored = localStorage.getItem('eden_user');
    if (!stored) {
      return;
    }

    const expiry = tokenExpiry(localStorage.getItem('eden_token'));
    if (expiry === null || Date.now() > expiry + REFRESH_GRACE_MS) {
      // Missing, unreadable, or past the point the server will renew it. Nothing here is
      // recoverable, so clear it rather than present a signed-in UI that cannot call the API.
      this.logout();
      return;
    }

    this.currentUser.set(JSON.parse(stored));

    if (Date.now() > expiry) {
      // Still renewable. Do it now rather than waiting for a request to fail — deferred out of
      // the constructor because the interceptor injects this service, and calling HttpClient
      // mid-construction is a dependency cycle.
      setTimeout(() => this.renewExpiredSession());
    }
  }

  /** Trades an expired-but-renewable token for a fresh one; signs out if the server refuses. */
  private renewExpiredSession(): void {
    const token = localStorage.getItem('eden_token');
    if (!token) {
      return;
    }
    this.http.post<AuthResponse>(`${this.apiUrl}/refresh`, {}, {
      headers: { Authorization: `Bearer ${token}` },
    }).subscribe({
      next: (res) => this.setSession(res),
      error: () => this.logout(),
    });
  }

  register(email: string, password: string, firstName: string, lastName: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/register`, {
      email, password, firstName, lastName,
    }).pipe(tap(res => this.setSession(res)));
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, {
      email, password,
    }).pipe(tap(res => {
      if (!('mfaRequired' in res)) {
        this.setSession(res);
      }
    }));
  }

  mfaVerify(mfaToken: string, code: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/mfa-verify`, { mfaToken, code })
      .pipe(tap(res => this.setSession(res)));
  }

  logout(): void {
    this.currentUser.set(null);
    if (isPlatformBrowser(this.platformId)) {
      localStorage.removeItem('eden_token');
      localStorage.removeItem('eden_user');
    }
  }

  getToken(): string | null {
    if (!isPlatformBrowser(this.platformId)) {
      return null;
    }
    const token = localStorage.getItem('eden_token');
    const expiry = tokenExpiry(token);
    // An expired token is still worth sending — the interceptor will renew it off the 401. One
    // past the renewal window is not: it can only ever fail, so treat it as no token at all.
    if (expiry === null || Date.now() > expiry + REFRESH_GRACE_MS) {
      return null;
    }
    return token;
  }

  forgotPassword(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/forgot-password`, { email });
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/reset-password`, { email, token, newPassword });
  }

  verifyEmail(email: string, token: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/verify-email`, { email, token });
  }

  resendVerification(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/resend-verification`, {}, this.authHeaders());
  }

  externalLogin(provider: string, idToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/external-login`, { provider, idToken })
      .pipe(tap(res => this.setSession(res)));
  }

  private readonly accountUrl = `${environment.apiUrl}/api/account`;

  private authHeaders() {
    const token = this.getToken();
    return { headers: { Authorization: `Bearer ${token}` } };
  }

  getProfile(): Observable<AccountProfileDto> {
    return this.http.get<AccountProfileDto>(`${this.accountUrl}/profile`, this.authHeaders());
  }

  updateProfile(firstName: string, lastName: string): Observable<AccountProfileDto> {
    return this.http.put<AccountProfileDto>(`${this.accountUrl}/profile`, { firstName, lastName }, this.authHeaders()).pipe(
      tap(p => this.currentUser.set({ id: p.id, email: p.email, firstName: p.firstName, lastName: p.lastName, role: this.currentUser()?.role ?? 'Customer', emailVerified: p.emailVerified }))
    );
  }

  updateDeliveryAddress(address: AddressDto): Observable<AccountProfileDto> {
    return this.http.put<AccountProfileDto>(`${this.accountUrl}/delivery-address`, address, this.authHeaders());
  }

  updateBillingAddress(address: AddressDto): Observable<AccountProfileDto> {
    return this.http.put<AccountProfileDto>(`${this.accountUrl}/billing-address`, address, this.authHeaders());
  }

  updatePayment(payment: { cardholderName: string; cardLast4: string; cardBrand: string; expiryMonth: number; expiryYear: number }): Observable<AccountProfileDto> {
    return this.http.put<AccountProfileDto>(`${this.accountUrl}/payment`, payment, this.authHeaders());
  }

  changePassword(currentPassword: string, newPassword: string): Observable<{ message: string; token?: string }> {
    return this.http.post<{ message: string; token?: string }>(`${this.accountUrl}/change-password`, { currentPassword, newPassword }, this.authHeaders())
      .pipe(tap(res => {
        // The server rotates the token version on password change (logging out other
        // sessions); store the fresh token it returns so this session stays signed in.
        if (res.token && isPlatformBrowser(this.platformId)) {
          localStorage.setItem('eden_token', res.token);
        }
      }));
  }

  setupMfa(): Observable<MfaSetupResponse> {
    return this.http.post<MfaSetupResponse>(`${this.accountUrl}/mfa/setup`, {}, this.authHeaders());
  }

  verifyMfaSetup(code: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.accountUrl}/mfa/verify`, { code }, this.authHeaders());
  }

  disableMfa(code: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.accountUrl}/mfa/disable`, { code }, this.authHeaders());
  }

  exportData(): Observable<object> {
    return this.http.get<object>(`${this.accountUrl}/export-data`, this.authHeaders());
  }

  deleteAccount(): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.accountUrl}/delete-account`, this.authHeaders());
  }

  private setSession(res: AuthResponse): void {
    this.currentUser.set(res.user);
    if (isPlatformBrowser(this.platformId)) {
      localStorage.setItem('eden_token', res.token);
      localStorage.setItem('eden_user', JSON.stringify(res.user));
    }
  }
}

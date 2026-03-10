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

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly apiUrl = `${environment.apiUrl}/api/auth`;

  readonly currentUser = signal<UserDto | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly isAdmin = computed(() => this.currentUser()?.role === 'Admin');

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      const stored = localStorage.getItem('eden_user');
      if (stored) {
        this.currentUser.set(JSON.parse(stored));
      }
    }
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
    if (isPlatformBrowser(this.platformId)) {
      return localStorage.getItem('eden_token');
    }
    return null;
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

  changePassword(currentPassword: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.accountUrl}/change-password`, { currentPassword, newPassword }, this.authHeaders());
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

  private setSession(res: AuthResponse): void {
    this.currentUser.set(res.user);
    if (isPlatformBrowser(this.platformId)) {
      localStorage.setItem('eden_token', res.token);
      localStorage.setItem('eden_user', JSON.stringify(res.user));
    }
  }
}

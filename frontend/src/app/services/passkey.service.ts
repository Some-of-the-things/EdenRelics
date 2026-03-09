import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Observable, from, switchMap, tap } from 'rxjs';
import { AuthService, AuthResponse } from './auth.service';

export interface PasskeyInfo {
  id: string;
  nickname: string | null;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class PasskeyService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly apiUrl = 'http://localhost:5260/api/passkey';

  private authHeaders() {
    const token = this.auth.getToken();
    return { headers: { Authorization: `Bearer ${token}` } };
  }

  supportsPasskeys(): boolean {
    if (!isPlatformBrowser(this.platformId)) return false;
    return !!window.PublicKeyCredential;
  }

  registerPasskey(): Observable<{ message: string }> {
    return this.http.post<any>(`${this.apiUrl}/register-options`, {}, this.authHeaders()).pipe(
      switchMap(options => from(this.createCredential(options))),
      switchMap(attestation => this.http.post<{ message: string }>(`${this.apiUrl}/register`, attestation, this.authHeaders()))
    );
  }

  loginWithPasskey(email?: string): Observable<AuthResponse> {
    return this.http.post<{ sessionId: string; options: any }>(`${this.apiUrl}/login-options`, { email: email || null }).pipe(
      switchMap(resp => from(this.getAssertion(resp.options)).pipe(
        switchMap(assertion => this.http.post<AuthResponse>(`${this.apiUrl}/login`, {
          sessionId: resp.sessionId,
          response: assertion
        }))
      )),
      tap(res => {
        this.auth['setSession'](res);
      })
    );
  }

  getCredentials(): Observable<PasskeyInfo[]> {
    return this.http.get<PasskeyInfo[]>(`${this.apiUrl}/credentials`, this.authHeaders());
  }

  deleteCredential(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.apiUrl}/credentials/${id}`, this.authHeaders());
  }

  renameCredential(id: string, nickname: string): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.apiUrl}/credentials/${id}`, { nickname }, this.authHeaders());
  }

  private async createCredential(options: any): Promise<any> {
    const { create } = await import('@github/webauthn-json');
    return await create({ publicKey: options });
  }

  private async getAssertion(options: any): Promise<any> {
    const { get } = await import('@github/webauthn-json');
    return await get({ publicKey: options });
  }
}

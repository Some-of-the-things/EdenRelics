import { Injectable, inject, signal, computed, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

export interface LocaleInfo {
  detected: boolean;
  countryCode: string;
  countryName: string;
  currency: string;
  currencySymbol: string;
  locale: string;
  exchangeRate: number;
}

const DEFAULT_LOCALE: LocaleInfo = {
  detected: false,
  countryCode: 'GB',
  countryName: 'United Kingdom',
  currency: 'GBP',
  currencySymbol: '£',
  locale: 'en-GB',
  exchangeRate: 1,
};

@Injectable({ providedIn: 'root' })
export class LocaleService {
  private readonly http = inject(HttpClient);
  private readonly platformId = inject(PLATFORM_ID);

  private readonly _locale = signal<LocaleInfo>(DEFAULT_LOCALE);
  private readonly _ready = signal(false);

  readonly locale = this._locale.asReadonly();
  readonly ready = this._ready.asReadonly();
  readonly isUK = computed(() => this._locale().countryCode === 'GB');
  readonly showLocalCurrency = computed(() => this._locale().currency !== 'GBP');

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.init();
    }
  }

  private init(): void {
    // Check localStorage for saved preference
    const saved = localStorage.getItem('eden-locale');
    if (saved) {
      try {
        const parsed = JSON.parse(saved) as LocaleInfo;
        this._locale.set(parsed);
        this._ready.set(true);
        return;
      } catch { /* ignore corrupt data */ }
    }

    // Detect from server
    this.http.get<LocaleInfo>(`${environment.apiUrl}/api/locale/detect`).subscribe({
      next: (info) => {
        this._locale.set(info);
        localStorage.setItem('eden-locale', JSON.stringify(info));
        this._ready.set(true);
      },
      error: () => {
        // Fallback: try to infer from browser language
        const lang = navigator.language || 'en-GB';
        const fallback: LocaleInfo = { ...DEFAULT_LOCALE, locale: lang };
        this._locale.set(fallback);
        this._ready.set(true);
      },
    });
  }

  /** Convert GBP amount to local currency */
  convertFromGBP(gbpAmount: number): number {
    return gbpAmount * this._locale().exchangeRate;
  }

  /** Format a GBP amount in the user's local currency */
  formatLocal(gbpAmount: number): string {
    const local = this.convertFromGBP(gbpAmount);
    const info = this._locale();
    try {
      return new Intl.NumberFormat(info.locale, {
        style: 'currency',
        currency: info.currency,
        minimumFractionDigits: info.currency === 'JPY' || info.currency === 'KRW' ? 0 : 2,
        maximumFractionDigits: info.currency === 'JPY' || info.currency === 'KRW' ? 0 : 2,
      }).format(local);
    } catch {
      return `${info.currencySymbol}${local.toFixed(2)}`;
    }
  }

  /** Format a date using the user's locale */
  formatDate(date: string | Date): string {
    const d = typeof date === 'string' ? new Date(date) : date;
    try {
      return new Intl.DateTimeFormat(this._locale().locale, {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
      }).format(d);
    } catch {
      return d.toLocaleDateString();
    }
  }

  /** Format a date with time */
  formatDateTime(date: string | Date): string {
    const d = typeof date === 'string' ? new Date(date) : date;
    try {
      return new Intl.DateTimeFormat(this._locale().locale, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      }).format(d);
    } catch {
      return d.toLocaleString();
    }
  }

  /** Override the detected locale (user preference) */
  setCountry(countryCode: string): void {
    this.http.get<LocaleInfo>(`${environment.apiUrl}/api/locale/detect`).subscribe(); // just to warm cache
    // For now, update the exchange rate via the rates endpoint
    this.http.get<Record<string, number>>(`${environment.apiUrl}/api/locale/rates`).subscribe({
      next: (rates) => {
        const current = this._locale();
        // Find currency for new country — we'll need a lookup
        // For simplicity, just keep current settings but allow shipping to update
        this._locale.update(l => ({ ...l, countryCode }));
        localStorage.setItem('eden-locale', JSON.stringify(this._locale()));
      },
    });
  }
}

import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpHeaders } from '@angular/common/http';

const STORAGE_KEY = 'eden_ea';
const TOKEN_SHAPE = /^[0-9a-f]{32}\.\d{1,12}\.[A-Za-z0-9_-]{20,64}$/;

/**
 * Early access for subscribers. The newsletter's links carry `?ea=<token>`; this keeps the token
 * (in this browser only) and sends it to the API in the `X-Early-Access` header, so early-access
 * pieces show up. The token is taken out of the address bar straight away so it isn't shared by
 * copying the link. Opening a link never signs anyone up for anything.
 */
@Injectable({ providedIn: 'root' })
export class EarlyAccessService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly token = signal<string | null>(null);

  constructor() {
    if (!this.isBrowser) {
      return;
    }
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored && TOKEN_SHAPE.test(stored)) {
        this.token.set(stored);
      }
    } catch {
      // Storage blocked: the link still works for this page view.
    }
    this.captureFromUrl();
  }

  private captureFromUrl(): void {
    const url = new URL(window.location.href);
    const fromLink = url.searchParams.get('ea');
    if (!fromLink) {
      return;
    }
    if (TOKEN_SHAPE.test(fromLink)) {
      this.token.set(fromLink);
      try {
        localStorage.setItem(STORAGE_KEY, fromLink);
      } catch {
        // ignore
      }
    }
    url.searchParams.delete('ea');
    window.history.replaceState(window.history.state, '', url.pathname + url.search + url.hash);
  }

  /** Headers for product and order requests; empty when there's no token. */
  headers(): HttpHeaders {
    const t = this.token();
    return t ? new HttpHeaders({ 'X-Early-Access': t }) : new HttpHeaders();
  }
}

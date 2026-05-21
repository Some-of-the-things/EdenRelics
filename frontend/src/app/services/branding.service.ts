import { Injectable, TransferState, inject, makeStateKey, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser, DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

export interface Branding {
  logoUrl: string | null;
  bgPrimary: string;
  bgSecondary: string;
  bgCard: string;
  bgDark: string;
  textPrimary: string;
  textSecondary: string;
  textMuted: string;
  textInverse: string;
  accent: string;
  accentHover: string;
  fontDisplay: string;
  fontBody: string;
}

const BRANDING_KEY = makeStateKey<Branding>('branding');

@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly http = inject(HttpClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly document = inject(DOCUMENT);
  private readonly transferState = inject(TransferState);

  readonly branding = signal<Branding | null>(null);
  private loaded = false;

  /**
   * Resolves branding before the app renders, in both SSR and browser:
   *   - SSR: HTTP-fetches, stashes in TransferState, mutates the server DOM
   *          (Angular SSR serialises documentElement.style into the response HTML).
   *   - Browser: reads from TransferState when available (no re-fetch), otherwise
   *              falls back to an HTTP fetch. Either way it then applies to the DOM.
   * Failure swallowed — default CSS variables remain in effect.
   */
  async load(): Promise<void> {
    if (this.loaded) {
      return;
    }
    this.loaded = true;

    if (this.transferState.hasKey(BRANDING_KEY)) {
      const cached = this.transferState.get(BRANDING_KEY, null);
      this.transferState.remove(BRANDING_KEY);
      if (cached) {
        this.branding.set(cached);
        this.apply(cached);
        return;
      }
    }

    try {
      const b = await firstValueFrom(this.http.get<Branding>(`${environment.apiUrl}/api/branding`));
      this.branding.set(b);
      this.apply(b);
      if (!isPlatformBrowser(this.platformId)) {
        this.transferState.set(BRANDING_KEY, b);
      }
    } catch {
      // Backend unreachable (common in local SSR dev) — defaults stay in place.
    }
  }

  apply(b: Branding): void {
    const root = this.document.documentElement;
    root.style.setProperty('--bg-primary', b.bgPrimary);
    root.style.setProperty('--bg-secondary', b.bgSecondary);
    root.style.setProperty('--bg-card', b.bgCard);
    root.style.setProperty('--bg-dark', b.bgDark);
    root.style.setProperty('--text-primary', b.textPrimary);
    root.style.setProperty('--text-secondary', b.textSecondary);
    root.style.setProperty('--text-muted', b.textMuted);
    root.style.setProperty('--text-inverse', b.textInverse);
    root.style.setProperty('--accent', b.accent);
    root.style.setProperty('--accent-hover', b.accentHover);

    // Fonts
    if (b.fontDisplay !== 'Playfair Display' || b.fontBody !== 'Work Sans') {
      this.loadGoogleFont(b.fontDisplay);
      this.loadGoogleFont(b.fontBody);
    }
    root.style.setProperty('--font-display', `'${b.fontDisplay}', Georgia, serif`);
    root.style.setProperty('--font-body', `'${b.fontBody}', system-ui, sans-serif`);

    // Favicon — only meaningful in the browser; server-side <link> mutation is harmless but pointless.
    if (b.logoUrl && isPlatformBrowser(this.platformId)) {
      const favicon = this.document.querySelector<HTMLLinkElement>('link[rel="icon"][type="image/png"]');
      if (favicon) {
        favicon.href = b.logoUrl;
      }
    }
  }

  private loadGoogleFont(fontName: string): void {
    if (fontName === 'Playfair Display' || fontName === 'Work Sans') {
      return;
    }
    const id = `gfont-${fontName.replace(/\s+/g, '-').toLowerCase()}`;
    if (this.document.getElementById(id)) {
      return;
    }

    const link = this.document.createElement('link');
    link.id = id;
    link.rel = 'stylesheet';
    link.href = `https://fonts.googleapis.com/css2?family=${encodeURIComponent(fontName)}:wght@300;400;500;600;700&display=swap`;
    this.document.head.appendChild(link);
  }
}

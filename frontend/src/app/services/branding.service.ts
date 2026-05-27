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
  /** Fonts shipped self-hosted in styles.scss — keyed to their full fallback stack. */
  private static readonly SELF_HOSTED: Record<string, string> = {
    'Playfair Display': "'Playfair Display', 'Playfair Display Fallback', Georgia, serif",
    'Work Sans': "'Work Sans', 'Work Sans Fallback', system-ui, sans-serif",
    'EB Garamond': "'EB Garamond', Georgia, 'Times New Roman', serif",
    'Cinzel Decorative': "'Cinzel Decorative', 'Playfair Display', Georgia, serif",
  };

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

    // Fonts — self-hosted fonts use their bundled fallback stack and skip the
    // Google Fonts fetch; anything else is loaded on demand.
    if (!BrandingService.SELF_HOSTED[b.fontDisplay]) {
      this.loadGoogleFont(b.fontDisplay);
    }
    if (!BrandingService.SELF_HOSTED[b.fontBody]) {
      this.loadGoogleFont(b.fontBody);
    }
    root.style.setProperty('--font-display', this.fontStack(b.fontDisplay, 'serif'));
    root.style.setProperty('--font-body', this.fontStack(b.fontBody, 'sans-serif'));

    // Favicon — only meaningful in the browser; server-side <link> mutation is harmless but pointless.
    if (b.logoUrl && isPlatformBrowser(this.platformId)) {
      const favicon = this.document.querySelector<HTMLLinkElement>('link[rel="icon"][type="image/png"]');
      if (favicon) {
        favicon.href = b.logoUrl;
      }
    }
  }

  /** Full CSS font stack for a configured font — self-hosted fonts keep their bundled fallbacks. */
  private fontStack(name: string, generic: 'serif' | 'sans-serif'): string {
    return BrandingService.SELF_HOSTED[name]
      ?? `'${name}', ${generic === 'serif' ? 'Georgia, serif' : 'system-ui, sans-serif'}`;
  }

  private loadGoogleFont(fontName: string): void {
    if (BrandingService.SELF_HOSTED[fontName]) {
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

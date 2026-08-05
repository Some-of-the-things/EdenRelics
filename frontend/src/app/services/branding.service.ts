import { Injectable, TransferState, inject, makeStateKey, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser, DOCUMENT } from '@angular/common';

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

/**
 * The site palette + fonts, fixed at build time after the vintage rebrand. Previously
 * fetched per render via a blocking app-initializer; that async HTTP call during SSR
 * bootstrap let concurrent cache-miss renders interleave and corrupt DI (NG0200 →
 * `undefined.fetch` → intermittent 500s — see worker.ts / the infra-outage runbook).
 * Compiling it in removes the hot-path fetch entirely. The admin branding editor still
 * writes these fields to the DB and previews them in-session, but the PUBLIC site
 * renders this constant — to change the live palette, edit here and redeploy.
 *
 * These ten colours MUST mirror the `:root` palette in styles.scss, because they are
 * written onto documentElement as inline custom properties and an inline style beats
 * `:root` — whatever is here is what everyone actually sees, and every component was
 * designed and contrast-checked against the stylesheet values.
 *
 * They drifted once. The values carried over from the pre-rebrand coral branding row
 * (`--bg-secondary: #B97534` and friends) shipped here on 2026-07-19 and silently
 * overrode the rebrand palette, putting six WCAG AA contrast failures on live pages —
 * the home journal/mailing text, the shop condition badge, the product-page era badge
 * and blog tags — all of which measured clean under the stylesheet palette. Restored
 * to match styles.scss on 2026-08-05; keep the two in step.
 */
const DEFAULT_BRANDING: Branding = {
  logoUrl: null,
  bgPrimary: '#F5F0E6',
  bgSecondary: '#EAE0CC',
  bgCard: '#FBF8F1',
  bgDark: '#1C1510',
  textPrimary: '#2E1A0E',
  textSecondary: '#5C3D1E',
  textMuted: '#6E4A22',
  textInverse: '#F5F0E6',
  accent: '#9B4A1E',
  accentHover: '#7A3A16',
  fontDisplay: 'Playfair Display',
  fontBody: 'Work Sans',
};

@Injectable({ providedIn: 'root' })
export class BrandingService {
  /** Fonts shipped self-hosted in styles.scss — keyed to their full fallback stack. */
  private static readonly SELF_HOSTED: Record<string, string> = {
    'Playfair Display': "'Playfair Display', 'Playfair Display Fallback', Georgia, serif",
    'Work Sans': "'Work Sans', 'Work Sans Fallback', system-ui, sans-serif",
    'EB Garamond': "'EB Garamond', Georgia, 'Times New Roman', serif",
    'Cinzel Decorative': "'Cinzel Decorative', 'Playfair Display', Georgia, serif",
  };

  private readonly platformId = inject(PLATFORM_ID);
  private readonly document = inject(DOCUMENT);
  private readonly transferState = inject(TransferState);

  readonly branding = signal<Branding | null>(null);
  private loaded = false;

  /**
   * Resolves branding before the app renders, in both SSR and browser — now from the
   * compiled-in {@link DEFAULT_BRANDING} constant, with NO HTTP. Removing the network
   * call from bootstrap is deliberate: the old blocking fetch was the async gap that let
   * concurrent SSR renders interleave and corrupt DI (see DEFAULT_BRANDING). The server
   * still primes TransferState and mutates the server DOM (Angular serialises
   * documentElement.style into the HTML); the browser reads TransferState so hydration
   * matches exactly. Synchronous by design — no await, so there is no interleaving window.
   */
  async load(): Promise<void> {
    if (this.loaded) {
      return;
    }
    this.loaded = true;

    const b = this.transferState.hasKey(BRANDING_KEY)
      ? this.transferState.get(BRANDING_KEY, DEFAULT_BRANDING)
      : DEFAULT_BRANDING;
    this.transferState.remove(BRANDING_KEY);

    this.branding.set(b);
    this.apply(b);

    if (!isPlatformBrowser(this.platformId)) {
      // Seed TransferState so the browser hydrates against the identical values.
      this.transferState.set(BRANDING_KEY, b);
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

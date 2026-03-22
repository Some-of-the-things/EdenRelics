import { Injectable, inject, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser, DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
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

@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly http = inject(HttpClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly document = inject(DOCUMENT);

  readonly branding = signal<Branding | null>(null);

  load(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    this.http.get<Branding>(`${environment.apiUrl}/api/branding`).subscribe({
      next: (b) => {
        this.branding.set(b);
        this.apply(b);
      },
    });
  }

  apply(b: Branding): void {
    if (!isPlatformBrowser(this.platformId)) return;

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

    // Favicon
    if (b.logoUrl) {
      const favicon = this.document.querySelector<HTMLLinkElement>('link[rel="icon"][type="image/png"]');
      if (favicon) favicon.href = b.logoUrl;
    }
  }

  private loadGoogleFont(fontName: string): void {
    if (fontName === 'Playfair Display' || fontName === 'Work Sans') return;
    const id = `gfont-${fontName.replace(/\s+/g, '-').toLowerCase()}`;
    if (this.document.getElementById(id)) return;

    const link = this.document.createElement('link');
    link.id = id;
    link.rel = 'stylesheet';
    link.href = `https://fonts.googleapis.com/css2?family=${encodeURIComponent(fontName)}:wght@300;400;500;600;700&display=swap`;
    this.document.head.appendChild(link);
  }
}

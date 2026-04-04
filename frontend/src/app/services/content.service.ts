import { Injectable, inject, signal, PLATFORM_ID, effect } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { LocaleService } from './locale.service';

@Injectable({ providedIn: 'root' })
export class ContentService {
  private readonly http = inject(HttpClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly localeService = inject(LocaleService);
  private readonly content = signal<Record<string, string>>({});
  private loaded = false;
  private loadedLocale = '';

  constructor() {
    // Re-fetch content when locale changes
    if (isPlatformBrowser(this.platformId)) {
      effect(() => {
        const locale = this.localeService.locale();
        const lang = locale.locale.split('-')[0]; // e.g., "fr" from "fr-FR"
        if (this.loadedLocale && this.loadedLocale !== lang) {
          this.loaded = false;
          this.load();
        }
      });
    }
  }

  load(): void {
    if (this.loaded) {
      return;
    }
    this.loaded = true;

    const locale = this.localeService.locale();
    const lang = locale.locale.split('-')[0];
    this.loadedLocale = lang;

    const params = lang && lang !== 'en' ? `?locale=${lang}` : '';
    this.http.get<Record<string, string>>(`${environment.apiUrl}/api/content${params}`).subscribe({
      next: (c) => this.content.set(c),
    });
  }

  get(key: string, fallback = ''): string {
    return this.content()[key] ?? fallback;
  }

  getAll(): Record<string, string> {
    return this.content();
  }

  setAll(content: Record<string, string>): void {
    this.content.set(content);
  }
}

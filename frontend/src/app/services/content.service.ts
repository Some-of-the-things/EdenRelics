import { Injectable, inject, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ContentService {
  private readonly http = inject(HttpClient);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly content = signal<Record<string, string>>({});

  load(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    this.http.get<Record<string, string>>(`${environment.apiUrl}/api/content`).subscribe({
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

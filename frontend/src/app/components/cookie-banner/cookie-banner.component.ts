import { Component, inject, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-cookie-banner',
  imports: [RouterLink],
  templateUrl: './cookie-banner.component.html',
  styleUrl: './cookie-banner.component.scss',
})
export class CookieBannerComponent {
  private readonly platformId = inject(PLATFORM_ID);
  readonly visible = signal(false);

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      const consent = localStorage.getItem('eden_cookie_consent');
      if (!consent) {
        this.visible.set(true);
      }
    }
  }

  acceptAll(): void {
    this.savePreference('all');
  }

  acceptEssential(): void {
    this.savePreference('essential');
  }

  private savePreference(level: string): void {
    if (isPlatformBrowser(this.platformId)) {
      localStorage.setItem('eden_cookie_consent', level);
    }
    this.visible.set(false);
  }
}

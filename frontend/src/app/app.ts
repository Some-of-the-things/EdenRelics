import { Component, inject, afterNextRender } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from './components/header/header.component';
import { FooterComponent } from './components/footer/footer.component';
import { CookieBannerComponent } from './components/cookie-banner/cookie-banner.component';
import { AnalyticsService } from './services/analytics.service';
import { ContentService } from './services/content.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, HeaderComponent, FooterComponent, CookieBannerComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = 'Eden Relics';
  private readonly content = inject(ContentService);
  private readonly analytics = inject(AnalyticsService);

  constructor() {
    // Branding is resolved during app initialisation (see appConfig) — no call here.
    this.content.load();
    afterNextRender(() => {
      this.analytics.init();
    });
  }
}

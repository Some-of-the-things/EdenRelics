import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from './components/header/header.component';
import { FooterComponent } from './components/footer/footer.component';
import { AnalyticsService } from './services/analytics.service';
import { BrandingService } from './services/branding.service';
import { ContentService } from './services/content.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, HeaderComponent, FooterComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = 'Eden Relics';
  private readonly branding = inject(BrandingService);
  private readonly content = inject(ContentService);

  constructor() {
    inject(AnalyticsService).init();
    this.branding.load();
    this.content.load();
  }
}

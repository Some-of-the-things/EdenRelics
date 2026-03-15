import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ProductListComponent } from '../../components/product-list/product-list.component';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-home',
  imports: [ProductListComponent, FormsModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly http = inject(HttpClient);
  readonly cms = inject(ContentService);

  mailingEmail = '';
  readonly mailingSubscribed = signal(false);

  subscribeToMailingList(): void {
    if (!this.mailingEmail.trim()) return;
    this.http.post(`${environment.apiUrl}/api/mailing-list/subscribe`, {
      email: this.mailingEmail,
      source: 'Footer',
    }).subscribe({
      next: () => this.mailingSubscribed.set(true),
    });
  }

  ngOnInit(): void {
    this.seo.updateTags({
      url: '/',
    });
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@type': 'Organization',
      name: 'Eden Relics',
      url: 'https://www.edenrelics.co.uk',
      description: 'Curated vintage clothing from decades past.',
    });
  }
}

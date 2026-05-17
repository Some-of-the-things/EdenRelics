import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
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
  changeDetection: ChangeDetectionStrategy.OnPush,
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
      source: 'Homepage',
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
      '@graph': [
        {
          '@type': 'Organization',
          '@id': 'https://edenrelics.co.uk/#organization',
          name: 'Eden Relics',
          legalName: 'EDEN RELICS LTD',
          url: 'https://edenrelics.co.uk',
          logo: 'https://edenrelics.co.uk/logo.png',
          description: 'Curated vintage clothing from decades past.',
          email: 'edenrelics@dcp-net.com',
          telephone: '+44 7454 705183',
          address: {
            '@type': 'PostalAddress',
            streetAddress: '30 Vane Close',
            addressLocality: 'Norwich',
            postalCode: 'NR7 0US',
            addressCountry: 'GB',
          },
        },
        {
          '@type': 'WebSite',
          '@id': 'https://edenrelics.co.uk/#website',
          url: 'https://edenrelics.co.uk',
          name: 'Eden Relics',
          publisher: { '@id': 'https://edenrelics.co.uk/#organization' },
        },
        {
          '@type': 'Store',
          '@id': 'https://edenrelics.co.uk/#store',
          name: 'Eden Relics',
          url: 'https://edenrelics.co.uk',
          image: 'https://edenrelics.co.uk/og-image.png',
          description: 'Carefully sourced and lovingly preserved vintage clothing from the 1970s to today.',
          telephone: '+44 7454 705183',
          email: 'edenrelics@dcp-net.com',
          priceRange: '£££',
          address: {
            '@type': 'PostalAddress',
            streetAddress: '30 Vane Close',
            addressLocality: 'Norwich',
            postalCode: 'NR7 0US',
            addressCountry: 'GB',
          },
        },
      ],
    });
  }
}

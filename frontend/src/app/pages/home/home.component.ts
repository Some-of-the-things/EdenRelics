import { ChangeDetectionStrategy, Component, effect, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ProductListComponent } from '../../components/product-list/product-list.component';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
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
  private readonly productStore = inject(ProductStore);
  readonly cms = inject(ContentService);

  mailingEmail = '';
  readonly mailingSubscribed = signal(false);

  constructor() {
    effect(() => {
      const products = this.productStore.products();
      this.emitJsonLd(products);
    });
  }

  subscribeToMailingList(): void {
    if (!this.mailingEmail.trim()) {
      return;
    }
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
  }

  private emitJsonLd(products: readonly Product[]): void {
    const graph: object[] = [
      {
        '@type': 'Organization',
        '@id': 'https://edenrelics.co.uk/#organization',
        name: 'Eden Relics',
        legalName: 'EDEN RELICS LTD',
        url: 'https://edenrelics.co.uk',
        logo: 'https://edenrelics.co.uk/logo.png',
        description: 'Vintage women’s clothing from the 1960s, 70s, 80s and 90s. Thoughtfully sourced, carefully assessed — slow fashion worth wearing again.',
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
        description: 'Vintage women’s clothing from the 1960s, 70s, 80s and 90s. Thoughtfully sourced, carefully assessed — slow fashion worth wearing again.',
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
    ];

    if (products.length > 0) {
      graph.push({
        '@type': 'ItemList',
        '@id': 'https://edenrelics.co.uk/#products',
        name: 'Vintage Clothing',
        numberOfItems: products.length,
        itemListElement: products.slice(0, 30).map((p, idx) => ({
          '@type': 'ListItem',
          position: idx + 1,
          url: `https://edenrelics.co.uk/product/${p.slug || p.id}`,
          name: p.name,
          image: p.imageUrl,
        })),
      });
    }

    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@graph': graph,
    });
  }
}

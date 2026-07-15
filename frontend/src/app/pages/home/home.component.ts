import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { HomeReviewsComponent } from '../../components/home-reviews/home-reviews.component';
import { SeoService } from '../../services/seo.service';
import { ContentService } from '../../services/content.service';
import { ReviewsService } from '../../services/reviews.service';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { collectionFeaturedSlugs, findCollectionBySlug, orderedCollectionProducts } from '../collections/collections.data';
import { MarketplaceService } from '../../services/marketplace.service';
import { imageSrcAt, imageSrcset } from '../../utils/image-variant-loader';
import { environment } from '../../../environments/environment';

interface BlogPostSummary {
  id: string;
  title: string;
  slug: string;
  excerpt: string | null;
  featuredImageUrl: string | null;
  author: string | null;
  publishedAtUtc: string | null;
}

@Component({
  selector: 'app-home',
  imports: [HomeReviewsComponent, FormsModule, RouterLink, CurrencyPipe],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly http = inject(HttpClient);
  private readonly productStore = inject(ProductStore);
  private readonly reviewsService = inject(ReviewsService);
  readonly cms = inject(ContentService);
  readonly marketplace = inject(MarketplaceService);

  private reviewSummary: { count: number; overall: number } | null = null;

  mailingEmail = '';
  readonly mailingSubscribed = signal(false);
  readonly latestBlogPost = signal<BlogPostSummary | null>(null);

  readonly srcset = imageSrcset;
  readonly srcAt = imageSrcAt;

  /** The 5 featured pieces from The Wildflower Edit, in curated order. */
  readonly featured = computed<Product[]>(() => {
    const c = findCollectionBySlug('wildflower-edit');
    if (!c) {
      return [];
    }
    return orderedCollectionProducts(this.productStore.liveOrSoldProducts(), collectionFeaturedSlugs(c));
  });

  /**
   * The featured pieces from the gated "Our Top Picks" edit. The template only shows the
   * section when the marketplace is live (multi-user), so this stays dormant until launch.
   */
  readonly topPicks = computed<Product[]>(() => {
    const c = findCollectionBySlug('top-picks');
    if (!c) {
      return [];
    }
    return orderedCollectionProducts(this.productStore.liveOrSoldProducts(), collectionFeaturedSlugs(c));
  });

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
    this.marketplace.load();
    this.seo.updateTags({
      url: '/',
      hreflang: true,
    });
    this.emitJsonLd();
    this.http.get<BlogPostSummary[]>(`${environment.apiUrl}/api/blog`).subscribe({
      next: (posts) => {
        if (posts.length > 0) {
          this.latestBlogPost.set(posts[0]);
        }
      },
      error: () => {},
    });
    this.reviewsService.getSummary().subscribe({
      next: (s) => {
        if (s.count > 0) {
          this.reviewSummary = { count: s.count, overall: s.overall };
          this.emitJsonLd();
        }
      },
      error: () => {},
    });
  }

  private emitJsonLd(): void {
    const graph: object[] = [
      {
        '@type': 'Organization',
        '@id': 'https://edenrelics.co.uk/#organization',
        name: 'Eden Relics',
        legalName: 'EDEN RELICS LTD',
        url: 'https://edenrelics.co.uk',
        logo: 'https://edenrelics.co.uk/logo.png',
        description: 'Vintage women’s clothing from the 1950s, 60s, 70s, 80s and 90s. Thoughtfully sourced, carefully assessed — slow fashion worth wearing again.',
        email: 'edenrelics@dcp-net.com',
        telephone: '+44 7454 905173',
        address: {
          '@type': 'PostalAddress',
          streetAddress: '30 Vane Close',
          addressLocality: 'Norwich',
          postalCode: 'NR7 0US',
          addressCountry: 'GB',
        },
        ...(this.reviewSummary
          ? {
              aggregateRating: {
                '@type': 'AggregateRating',
                ratingValue: this.reviewSummary.overall.toFixed(1),
                reviewCount: this.reviewSummary.count,
                bestRating: '5',
                worstRating: '1',
              },
            }
          : {}),
      },
      {
        '@type': 'WebSite',
        '@id': 'https://edenrelics.co.uk/#website',
        url: 'https://edenrelics.co.uk',
        name: 'Eden Relics',
        publisher: { '@id': 'https://edenrelics.co.uk/#organization' },
        potentialAction: {
          '@type': 'SearchAction',
          target: {
            '@type': 'EntryPoint',
            urlTemplate: 'https://edenrelics.co.uk/shop?q={search_term_string}',
          },
          'query-input': 'required name=search_term_string',
        },
      },
      {
        '@type': 'Store',
        '@id': 'https://edenrelics.co.uk/#store',
        name: 'Eden Relics',
        url: 'https://edenrelics.co.uk',
        image: 'https://edenrelics.co.uk/og-image.png',
        description: 'Vintage women’s clothing from the 1950s, 60s, 70s, 80s and 90s. Thoughtfully sourced, carefully assessed — slow fashion worth wearing again.',
        telephone: '+44 7454 905173',
        email: 'edenrelics@dcp-net.com',
        priceRange: '£££',
        address: {
          '@type': 'PostalAddress',
          streetAddress: '30 Vane Close',
          addressLocality: 'Norwich',
          postalCode: 'NR7 0US',
          addressCountry: 'GB',
        },
        // Local presence (Norwich) + the national area we actually ship to.
        // Reinforces the "vintage clothing Norwich" / "Norwich vintage shops"
        // local queries the site already appears for, without implying a
        // walk-in storefront.
        areaServed: [
          { '@type': 'City', name: 'Norwich' },
          { '@type': 'AdministrativeArea', name: 'Norfolk' },
          { '@type': 'Country', name: 'United Kingdom' },
        ],
      },
    ];

    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@graph': graph,
    });
  }
}

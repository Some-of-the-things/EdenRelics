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
import { collectionFeaturedSlugs, findCollectionBySlug, orderedCollectionProducts, orderedProductsById } from '../collections/collections.data';
import { TopPicksService } from '../../services/top-picks.service';
import { HomeSection, HomeSectionsService } from '../../services/home-sections.service';
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
  readonly topPicks = inject(TopPicksService);
  private readonly homeSections = inject(HomeSectionsService);

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
   * The featured pieces from the curated "Our Top Picks" edit, resolved by product ID from the
   * DB-curated list. Empty while Top Picks is switched off, so its strip stays dormant until the
   * operator flips its switch (independent of the marketplace).
   */
  readonly topPickProducts = computed<Product[]>(() =>
    this.topPicks.enabled()
      ? orderedProductsById(this.productStore.liveOrSoldProducts(), this.topPicks.featuredProductIds())
      : [],
  );

  /**
   * The product strips in the order arranged on the admin Home Sections tab, each with its pieces.
   * A strip with no pieces to show is left out rather than rendered as an empty heading.
   */
  readonly strips = computed(() =>
    this.homeSections
      .sections()
      .filter((section) => section.visible || section.kind === 'top-picks')
      .map((section, index) => ({ section, index, products: this.productsFor(section) }))
      .filter((strip) => strip.products.length > 0),
  );

  private productsFor(section: HomeSection): Product[] {
    switch (section.kind) {
      case 'top-picks':
        return this.topPickProducts();
      case 'latest-collection':
        return this.featured();
      case 'custom':
        // Live only: a sold hand-pick just drops off. (liveOrSoldProducts would show admins, who
        // receive the whole catalogue, sold pieces customers never see.)
        return orderedProductsById(this.productStore.liveProducts(), section.productIds);
    }
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
    this.topPicks.load();
    this.homeSections.load();
    this.seo.updateTags({
      url: '/',
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

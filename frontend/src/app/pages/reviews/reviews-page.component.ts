import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SeoService } from '../../services/seo.service';
import { ReviewsService, toShopReview } from '../../services/reviews.service';
import { ReviewCardComponent } from '../../components/reviews/review-card.component';
import { ReviewStarsComponent } from '../../components/reviews/review-stars.component';
import { GOOGLE_PROFILE_URL, SHOP_REVIEWS, ShopReview, googleRating } from '../../components/reviews/reviews.data';

/**
 * Every customer review, in three clearly separate groups: Google reviews, verified purchases from
 * this site (newest first, whatever their rating: UK rules don't allow hiding negative ones), and
 * reviews from our other shops. Deliberately no Review / AggregateRating structured data (see
 * reviews.data.ts).
 */
@Component({
  selector: 'app-reviews-page',
  imports: [ReviewCardComponent, ReviewStarsComponent, RouterLink],
  templateUrl: './reviews-page.component.html',
  styleUrl: './reviews-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewsPageComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly reviewsService = inject(ReviewsService);

  readonly rating = googleRating();
  readonly profileUrl = GOOGLE_PROFILE_URL;

  readonly google = SHOP_REVIEWS.filter((r) => r.source === 'google');
  readonly otherShops = SHOP_REVIEWS.filter((r) => r.source === 'buyer');
  readonly verified = signal<ShopReview[]>([]);

  ngOnInit(): void {
    this.seo.updateTags({
      title: 'Customer Reviews',
      description:
        'What customers say about buying vintage from Eden Relics: reviews of our dresses, packaging and delivery.',
      url: '/reviews',
    });
    this.seo.setJsonLd({
      '@context': 'https://schema.org',
      '@type': 'WebPage',
      name: 'Customer Reviews',
      url: 'https://edenrelics.co.uk/reviews',
      isPartOf: { '@id': 'https://edenrelics.co.uk/#website' },
    });

    this.reviewsService.getPublic(100).subscribe({
      next: (rows) => this.verified.set(rows.map(toShopReview)),
      error: () => {},
    });
  }
}

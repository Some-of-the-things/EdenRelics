import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { SeoService } from '../../services/seo.service';
import { ReviewsService } from '../../services/reviews.service';
import { ReviewCardComponent } from '../../components/reviews/review-card.component';
import { ReviewStarsComponent } from '../../components/reviews/review-stars.component';
import { GOOGLE_PROFILE_URL, ShopReview, allReviews, googleRating } from '../../components/reviews/reviews.data';

/**
 * Every customer review, Google first. Reviews left on this site (approved in the admin) are
 * added after the listed ones, whatever their rating: UK rules don't allow hiding negative ones.
 * Deliberately no Review / AggregateRating structured data (see reviews.data.ts).
 */
@Component({
  selector: 'app-reviews-page',
  imports: [ReviewCardComponent, ReviewStarsComponent],
  templateUrl: './reviews-page.component.html',
  styleUrl: './reviews-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewsPageComponent implements OnInit {
  private readonly seo = inject(SeoService);
  private readonly reviewsService = inject(ReviewsService);

  readonly rating = googleRating();
  readonly profileUrl = GOOGLE_PROFILE_URL;

  private readonly listed = allReviews();
  private readonly onSite = signal<ShopReview[]>([]);
  readonly reviews = computed(() => [...this.listed, ...this.onSite()]);

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
      next: (rows) =>
        this.onSite.set(
          rows.map((r) => ({
            name: r.authorDisplayName,
            source: 'buyer' as const,
            rating: Math.round((r.transactionRating + r.deliveryRating + r.productRating) / 3),
            text: r.comment,
            showOnHome: false,
            showOnProduct: false,
          })),
        ),
      error: () => {},
    });
  }
}

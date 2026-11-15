import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReviewStarsComponent } from './review-stars.component';
import { ShopReview } from './reviews.data';

/** One review card: stars, the review in italics, the name, and its tag (plus a Google link). */
@Component({
  selector: 'app-review-card',
  imports: [ReviewStarsComponent],
  template: `
    @let r = review();
    <figure class="review-card" [class.review-card--large]="large()">
      <app-review-stars [rating]="r.rating" [size]="large() ? '18px' : '15px'" />
      <blockquote class="review-card__text">
        <p>{{ r.text }}</p>
      </blockquote>
      <figcaption class="review-card__meta">
        <span class="review-card__name">{{ r.name }}</span>
        <span class="review-card__tag">{{ r.source === 'google' ? 'Google' : 'Buyer review' }}</span>
        @if (r.source === 'google' && r.url) {
          <a class="review-card__link" [href]="r.url" target="_blank" rel="noopener noreferrer"
             [attr.aria-label]="'Read ' + r.name + '’s review on Google (opens in a new tab)'">Read on Google</a>
        }
      </figcaption>
    </figure>
  `,
  styleUrl: './review-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewCardComponent {
  readonly review = input.required<ShopReview>();
  readonly large = input(false);
}

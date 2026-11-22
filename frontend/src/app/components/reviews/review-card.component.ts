import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ReviewStarsComponent } from './review-stars.component';
import { ShopReview } from './reviews.data';

const TAGS: Record<ShopReview['source'], string> = {
  google: 'Google',
  buyer: 'From our other shops',
  verified: 'Verified purchase',
};

/**
 * One review card: stars, the review in italics, the name, and where it's from (plus a Google
 * link). Verified purchases also show the piece, the buyer's photo and any reply from the shop.
 */
@Component({
  selector: 'app-review-card',
  imports: [ReviewStarsComponent],
  template: `
    @let r = review();
    <figure class="review-card" [class.review-card--large]="large()" [class.review-card--verified]="r.source === 'verified'">
      <app-review-stars [rating]="r.rating" [size]="large() ? '18px' : '15px'" />
      @if (r.pieces?.length) {
        <p class="review-card__piece">{{ r.pieces!.join(', ') }}</p>
      }
      <blockquote class="review-card__text">
        <p>{{ r.text }}</p>
      </blockquote>
      @if (r.photoUrl) {
        <img class="review-card__photo" [src]="r.photoUrl" [alt]="'Photo from ' + r.name" loading="lazy" />
      }
      <figcaption class="review-card__meta">
        <span class="review-card__name">{{ r.name }}</span>
        <span class="review-card__tag" [class.review-card__tag--verified]="r.source === 'verified'">{{ tag() }}</span>
        @if (r.source === 'google' && r.url) {
          <a class="review-card__link" [href]="r.url" target="_blank" rel="noopener noreferrer"
             [attr.aria-label]="'Read ' + r.name + '’s review on Google (opens in a new tab)'">Read on Google</a>
        }
      </figcaption>
      @if (r.reply) {
        <div class="review-card__reply">
          <span class="review-card__reply-label">Reply from Eden Relics</span>
          <p>{{ r.reply }}</p>
        </div>
      }
    </figure>
  `,
  styleUrl: './review-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewCardComponent {
  readonly review = input.required<ShopReview>();
  readonly large = input(false);
  readonly tag = computed(() => TAGS[this.review().source]);
}

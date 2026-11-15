import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReviewStarsComponent } from './review-stars.component';
import { googleRating, productPageReview } from './reviews.data';

/**
 * The one-line rating used outside the reviews section.
 * - Product pages: `linked` + `quoteKey` → "★★★★★ 5.0 on Google · Read reviews" plus one short
 *   review chosen by the product.
 * - Basket / checkout: no inputs → stars and "5.0 on Google" only, no link, so nobody is drawn
 *   away mid-checkout.
 */
@Component({
  selector: 'app-review-trust-line',
  imports: [RouterLink, ReviewStarsComponent],
  template: `
    @if (rating) {
      <div class="trust">
        <p class="trust__line">
          <app-review-stars [rating]="+rating" size="14px" />
          <span>{{ rating }} on Google</span>
          @if (linked()) {
            <span aria-hidden="true">·</span>
            <a routerLink="/reviews" class="trust__link">Read reviews</a>
          }
        </p>
        @if (quote(); as q) {
          <p class="trust__quote">
            <span class="trust__quote-text">“{{ q.text }}”</span>
            <span class="trust__quote-name">{{ q.name }}</span>
          </p>
        }
      </div>
    }
  `,
  styles: `
    :host { display: block; }
    .trust { margin-top: 1rem; color: var(--text-primary, #2E1A0E); }
    .trust__line {
      display: flex; flex-wrap: wrap; align-items: center; gap: 0.45rem;
      margin: 0;
      font-family: 'EB Garamond', Georgia, serif;
      font-size: 1.05rem;
    }
    .trust__link {
      color: var(--accent, #9B4A1E);
      text-decoration: underline;
      text-underline-offset: 2px;
    }
    .trust__link:hover, .trust__link:focus-visible { color: var(--accent-hover, #7A3A16); }
    .trust__quote {
      margin: 0.4rem 0 0;
      display: flex; flex-wrap: wrap; align-items: baseline; gap: 0.25rem 0.6rem;
    }
    .trust__quote-text {
      font-family: 'Playfair Display', 'Playfair Display Fallback', Georgia, serif;
      font-style: italic;
      font-size: 0.98rem;
      line-height: 1.5;
    }
    .trust__quote-name {
      font-family: 'EB Garamond', Georgia, serif;
      font-size: 0.95rem;
      color: var(--text-secondary, #5C3D1E);
    }
    .trust__quote-name::before { content: '— '; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewTrustLineComponent {
  readonly linked = input(false);
  /** When set, also show one short review picked by this key (a product ID). */
  readonly quoteKey = input<string | null>(null);

  readonly rating = googleRating();
  readonly quote = computed(() => {
    const key = this.quoteKey();
    return key ? productPageReview(key) : undefined;
  });
}

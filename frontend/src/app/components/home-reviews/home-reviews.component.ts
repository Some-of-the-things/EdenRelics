import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  PLATFORM_ID,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ReviewCardComponent } from '../reviews/review-card.component';
import { ReviewStarsComponent } from '../reviews/review-stars.component';
import { GOOGLE_PROFILE_URL, SHOP_REVIEWS, googleRating } from '../reviews/reviews.data';

/**
 * The homepage reviews section: the Google reviews as large cards, the short buyer reviews
 * below, and a link through to /reviews. On a phone the cards become one swipeable row
 * (Google first) with dots underneath.
 */
@Component({
  selector: 'app-home-reviews',
  imports: [RouterLink, ReviewCardComponent, ReviewStarsComponent],
  templateUrl: './home-reviews.component.html',
  styleUrl: './home-reviews.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeReviewsComponent {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly track = viewChild<ElementRef<HTMLElement>>('track');

  readonly google = SHOP_REVIEWS.filter((r) => r.showOnHome && r.source === 'google');
  readonly buyer = SHOP_REVIEWS.filter((r) => r.showOnHome && r.source !== 'google');
  readonly dots = Array.from({ length: this.google.length + this.buyer.length }, (_, i) => i);
  readonly rating = googleRating();
  readonly profileUrl = GOOGLE_PROFILE_URL;

  /** Which card the phone carousel is showing, for the dots. */
  readonly active = signal(0);

  private cards(): HTMLElement[] {
    const el = this.track()?.nativeElement;
    return el ? Array.from(el.querySelectorAll<HTMLElement>('.home-reviews__card')) : [];
  }

  onScroll(): void {
    const el = this.track()?.nativeElement;
    if (!this.isBrowser || !el) {
      return;
    }
    const cards = this.cards();
    const left = el.scrollLeft;
    let nearest = 0;
    let best = Number.POSITIVE_INFINITY;
    cards.forEach((card, i) => {
      const distance = Math.abs(card.offsetLeft - el.offsetLeft - left);
      if (distance < best) {
        best = distance;
        nearest = i;
      }
    });
    // At the far right the last card can't snap to the left edge; treat the end as the last card.
    if (left + el.clientWidth >= el.scrollWidth - 2) {
      nearest = cards.length - 1;
    }
    this.active.set(nearest);
  }

  goTo(index: number): void {
    const el = this.track()?.nativeElement;
    const card = this.cards()[index];
    if (!this.isBrowser || !el || !card) {
      return;
    }
    el.scrollTo({ left: card.offsetLeft - el.offsetLeft, behavior: 'smooth' });
  }
}

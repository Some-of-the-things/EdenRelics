import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** A row of five inline-SVG stars, read out as one image ("5 out of 5 stars"). */
@Component({
  selector: 'app-review-stars',
  template: `
    <span class="stars" role="img" [attr.aria-label]="label()" [style.--star-size]="size()">
      @for (n of positions; track n) {
        <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false" [class.stars__empty]="n > rating()">
          <path d="M12 2.6l2.9 6.1 6.6.8-4.9 4.6 1.3 6.6L12 17.4l-5.9 3.3 1.3-6.6-4.9-4.6 6.6-.8z" />
        </svg>
      }
    </span>
  `,
  styles: `
    :host { display: inline-flex; }
    .stars { display: inline-flex; gap: 2px; line-height: 0; }
    svg { width: var(--star-size, 16px); height: var(--star-size, 16px); fill: var(--accent, #9B4A1E); }
    svg.stars__empty { fill: none; stroke: var(--accent, #9B4A1E); stroke-width: 1.5; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewStarsComponent {
  readonly rating = input(5);
  readonly size = input('16px');
  readonly positions = [1, 2, 3, 4, 5];
  readonly label = computed(() => `${this.rating()} out of 5 stars`);
}

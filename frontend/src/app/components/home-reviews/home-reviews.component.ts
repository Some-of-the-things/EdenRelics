import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { PublicReview, ReviewSummary, ReviewsService } from '../../services/reviews.service';

@Component({
  selector: 'app-home-reviews',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './home-reviews.component.html',
  styleUrl: './home-reviews.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HomeReviewsComponent implements OnInit {
  private readonly reviews = inject(ReviewsService);

  readonly summary = signal<ReviewSummary | null>(null);
  readonly items = signal<PublicReview[]>([]);
  readonly stars = [1, 2, 3, 4, 5];

  ngOnInit(): void {
    this.reviews.getSummary().subscribe({
      next: (s) => this.summary.set(s),
      error: () => {},
    });
    this.reviews.getPublic(12).subscribe({
      next: (r) => this.items.set(r),
      error: () => {},
    });
  }

  overall(r: PublicReview): number {
    return (r.transactionRating + r.deliveryRating + r.productRating) / 3;
  }
}

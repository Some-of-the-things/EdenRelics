import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ReviewsService, EligibleOrder, MyReview } from '../../services/reviews.service';
import { SeoService } from '../../services/seo.service';

@Component({
  selector: 'app-review-page',
  imports: [FormsModule, RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './review-page.component.html',
  styleUrl: './review-page.component.scss',
})
export class ReviewPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reviews = inject(ReviewsService);
  private readonly seo = inject(SeoService);

  readonly orderId = signal<string>('');
  readonly order = signal<EligibleOrder | null>(null);
  readonly loading = signal(true);
  readonly notEligible = signal(false);
  readonly transactionRating = signal(0);
  readonly deliveryRating = signal(0);
  readonly productRating = signal(0);
  readonly comment = signal('');
  readonly displayName = signal('');
  readonly submitting = signal(false);
  readonly submitted = signal<MyReview | null>(null);
  readonly error = signal<string | null>(null);

  readonly stars = [1, 2, 3, 4, 5];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('orderId') ?? '';
    this.orderId.set(id);
    this.seo.updateTags({ title: 'Leave a review', url: `/review/${id}`, noIndex: true });

    this.reviews.getEligible().subscribe({
      next: (orders) => {
        const match = orders.find((o) => o.orderId === id);
        if (match) {
          this.order.set(match);
        } else {
          this.notEligible.set(true);
        }
        this.loading.set(false);
      },
      error: () => {
        this.notEligible.set(true);
        this.loading.set(false);
      },
    });
  }

  setRating(field: 'transaction' | 'delivery' | 'product', value: number): void {
    if (field === 'transaction') this.transactionRating.set(value);
    else if (field === 'delivery') this.deliveryRating.set(value);
    else this.productRating.set(value);
  }

  // Arrow-key navigation for the star radiogroups (WAI-ARIA radio pattern): move
  // selection and focus relative to the focused star.
  onStarKey(field: 'transaction' | 'delivery' | 'product', current: number, event: KeyboardEvent): void {
    let next: number;
    if (event.key === 'ArrowRight' || event.key === 'ArrowUp') next = Math.min(5, current + 1);
    else if (event.key === 'ArrowLeft' || event.key === 'ArrowDown') next = Math.max(1, current - 1);
    else if (event.key === 'Home') next = 1;
    else if (event.key === 'End') next = 5;
    else return;

    event.preventDefault();
    this.setRating(field, next);
    const group = (event.currentTarget as HTMLElement).parentElement;
    const target = group?.querySelectorAll('button')[next - 1] as HTMLElement | undefined;
    target?.focus();
  }

  submit(): void {
    this.error.set(null);
    if (!this.transactionRating() || !this.deliveryRating() || !this.productRating()) {
      this.error.set('Please rate all three aspects.');
      return;
    }
    if (this.comment().trim().length < 10) {
      this.error.set('Please write at least 10 characters in your review.');
      return;
    }

    this.submitting.set(true);
    this.reviews
      .submit({
        orderId: this.orderId(),
        transactionRating: this.transactionRating(),
        deliveryRating: this.deliveryRating(),
        productRating: this.productRating(),
        comment: this.comment().trim(),
        authorDisplayName: this.displayName().trim() || undefined,
      })
      .subscribe({
        next: (r) => {
          this.submitted.set(r);
          this.submitting.set(false);
        },
        error: (e) => {
          this.error.set(e?.error?.error ?? 'Could not submit your review. Please try again.');
          this.submitting.set(false);
        },
      });
  }
}

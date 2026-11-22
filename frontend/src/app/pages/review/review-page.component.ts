import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { NameStyle, ReviewInvite, ReviewsService } from '../../services/reviews.service';
import { SeoService } from '../../services/seo.service';
import { AuthService } from '../../services/auth.service';

/**
 * The review form for one order. Reached from the emailed link (`/review/:orderId?t=…`, no login
 * needed) or from the account page when signed in. One star rating, the review, an optional photo
 * and how the name shows; the piece is attached automatically.
 */
@Component({
  selector: 'app-review-page',
  imports: [RouterLink, DatePipe],
  templateUrl: './review-page.component.html',
  styleUrl: './review-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly reviews = inject(ReviewsService);
  private readonly seo = inject(SeoService);
  readonly auth = inject(AuthService);

  readonly orderId = signal('');
  private token: string | null = null;

  readonly invite = signal<ReviewInvite | null>(null);
  readonly loading = signal(true);
  readonly unavailable = signal(false);

  readonly rating = signal(0);
  readonly comment = signal('');
  readonly firstName = signal('');
  readonly lastName = signal('');
  readonly nameStyle = signal<NameStyle>('first-initial');
  readonly photoUrl = signal<string | null>(null);
  readonly photoUploading = signal(false);

  readonly submitting = signal(false);
  readonly result = signal<'published' | 'held' | null>(null);
  readonly error = signal<string | null>(null);

  readonly stars = [1, 2, 3, 4, 5];
  readonly ratingLabels = ['', 'Poor', 'Not great', 'Good', 'Very good', 'Excellent'];

  /** The radio labels show the name as it will appear, once there's a name to show. */
  readonly nameExamples = computed(() => {
    const first = this.firstName().trim();
    const last = this.lastName().trim();
    if (!first) {
      return { firstInitial: 'First name and surname initial', first: 'First name only' };
    }
    return {
      firstInitial: last ? `${first} ${last[0].toUpperCase()}.` : first,
      first,
    };
  });

  readonly commentLength = computed(() => this.comment().trim().length);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('orderId') ?? '';
    this.token = this.route.snapshot.queryParamMap.get('t');
    this.orderId.set(id);
    this.seo.updateTags({ title: 'Leave a review', url: `/review/${id}`, noIndex: true });

    this.reviews.getInvite(id, this.token).subscribe({
      next: (invite) => {
        this.invite.set(invite);
        this.firstName.set(invite.firstName ?? '');
        this.lastName.set(invite.lastName ?? '');
        this.loading.set(false);
      },
      error: () => {
        this.unavailable.set(true);
        this.loading.set(false);
      },
    });
  }

  // Arrow-key navigation for the star radiogroup (WAI-ARIA radio pattern).
  onStarKey(current: number, event: KeyboardEvent): void {
    let next: number;
    if (event.key === 'ArrowRight' || event.key === 'ArrowUp') {
      next = Math.min(5, current + 1);
    } else if (event.key === 'ArrowLeft' || event.key === 'ArrowDown') {
      next = Math.max(1, current - 1);
    } else if (event.key === 'Home') {
      next = 1;
    } else if (event.key === 'End') {
      next = 5;
    } else {
      return;
    }
    event.preventDefault();
    this.rating.set(next);
    const group = (event.currentTarget as HTMLElement).parentElement;
    (group?.querySelectorAll('button')[next - 1] as HTMLElement | undefined)?.focus();
  }

  choosePhoto(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    if (file.size > 15 * 1024 * 1024) {
      this.error.set('Please choose a photo under 15 MB.');
      return;
    }
    this.error.set(null);
    this.photoUploading.set(true);
    this.reviews.uploadPhoto(this.orderId(), this.token, file).subscribe({
      next: (res) => {
        this.photoUrl.set(res.url);
        this.photoUploading.set(false);
      },
      error: (e) => {
        this.photoUploading.set(false);
        this.error.set(e?.error?.error ?? 'The photo could not be uploaded. Please try another.');
      },
    });
  }

  submit(): void {
    this.error.set(null);
    if (!this.rating()) {
      this.error.set('Please choose a star rating.');
      return;
    }
    if (this.commentLength() < 10) {
      this.error.set('Please write at least 10 characters.');
      return;
    }
    this.submitting.set(true);
    this.reviews
      .submit(this.orderId(), {
        token: this.token,
        rating: this.rating(),
        comment: this.comment().trim(),
        firstName: this.firstName().trim(),
        lastName: this.lastName().trim(),
        nameStyle: this.nameStyle(),
        photoUrl: this.photoUrl(),
      })
      .subscribe({
        next: (r) => {
          this.submitting.set(false);
          this.result.set(r.status === 'Approved' ? 'published' : 'held');
        },
        error: (e) => {
          this.submitting.set(false);
          this.error.set(e?.error?.error ?? 'Your review could not be sent. Please try again.');
        },
      });
  }
}

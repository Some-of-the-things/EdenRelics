import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Observable } from 'rxjs';
import { AdminReview, HIDE_REASONS, HideReason, ReviewStatus, ReviewsService } from '../../services/reviews.service';

type Filter = ReviewStatus | 'all';

/**
 * Reviews of website orders. They publish on their own; this is where Teodora replies, checks the
 * ones the filter held, and hides one for a recorded reason (abuse, personal data, spam, or not
 * about the purchase). Also the "days after dispatch" setting for the review email.
 */
@Component({
  selector: 'app-admin-reviews',
  imports: [DatePipe, CurrencyPipe],
  templateUrl: './admin-reviews.component.html',
  styleUrl: './admin-reviews.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminReviewsComponent implements OnInit {
  private readonly api = inject(ReviewsService);

  readonly reasons = HIDE_REASONS;
  readonly filter = signal<Filter>('all');
  readonly reviews = signal<AdminReview[]>([]);
  readonly loading = signal(false);
  readonly working = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly replyingId = signal<string | null>(null);
  readonly replyText = signal('');
  readonly hidingId = signal<string | null>(null);
  readonly hideReason = signal<HideReason | ''>('');
  readonly hideNote = signal('');

  readonly delayDays = signal<number | null>(null);
  readonly delaySaved = signal(false);

  ngOnInit(): void {
    this.load();
    this.api.getSettings().subscribe({ next: (s) => this.delayDays.set(s.requestDelayDays), error: () => {} });
  }

  load(): void {
    this.loading.set(true);
    const f = this.filter();
    this.api.getAdmin(f === 'all' ? undefined : f).subscribe({
      next: (rows) => {
        this.reviews.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  setFilter(f: Filter): void {
    this.filter.set(f);
    this.load();
  }

  statusLabel(r: AdminReview): string {
    if (r.status === 'Approved') {
      return 'Published';
    }
    return r.status === 'Pending' ? 'Held for a check' : 'Hidden';
  }

  reasonLabel(value: string | null): string {
    return this.reasons.find((r) => r.value === value)?.label ?? value ?? '';
  }

  stars(rating: number): { on: string; off: string } {
    return { on: '★'.repeat(rating), off: '★'.repeat(Math.max(0, 5 - rating)) };
  }

  startReply(r: AdminReview): void {
    this.hidingId.set(null);
    this.replyingId.set(r.id);
    this.replyText.set(r.reply ?? '');
  }

  saveReply(r: AdminReview): void {
    this.mutate(r.id, this.api.reply(r.id, this.replyText()), () => this.replyingId.set(null));
  }

  removeReply(r: AdminReview): void {
    if (!confirm('Remove your reply from this review?')) {
      return;
    }
    this.mutate(r.id, this.api.reply(r.id, ''), () => this.replyingId.set(null));
  }

  startHide(r: AdminReview): void {
    this.replyingId.set(null);
    this.hidingId.set(r.id);
    this.hideReason.set('');
    this.hideNote.set('');
  }

  confirmHide(r: AdminReview): void {
    const reason = this.hideReason();
    if (!reason) {
      this.error.set('Choose the reason for hiding it.');
      return;
    }
    this.mutate(r.id, this.api.hide(r.id, reason, this.hideNote().trim()), () => this.hidingId.set(null));
  }

  publish(r: AdminReview): void {
    this.mutate(r.id, this.api.publish(r.id), () => {});
  }

  saveDelay(value: string): void {
    const days = Number(value);
    if (!Number.isInteger(days) || days < 1 || days > 60) {
      this.error.set('Choose between 1 and 60 days.');
      return;
    }
    this.error.set(null);
    this.api.saveSettings(days).subscribe({
      next: (s) => {
        this.delayDays.set(s.requestDelayDays);
        this.delaySaved.set(true);
        setTimeout(() => this.delaySaved.set(false), 2500);
      },
      error: (e) => this.error.set(e?.error?.error ?? 'Could not save the setting.'),
    });
  }

  private mutate(id: string, obs: Observable<AdminReview>, done: () => void): void {
    this.working.set(id);
    this.error.set(null);
    obs.subscribe({
      next: (updated) => {
        this.working.set(null);
        done();
        const f = this.filter();
        this.reviews.update((rows) =>
          rows.flatMap((row) => (row.id !== id ? [row] : f === 'all' || f === updated.status ? [updated] : [])),
        );
      },
      error: (e) => {
        this.working.set(null);
        this.error.set(e?.error?.error ?? 'That did not work. Please try again.');
      },
    });
  }
}

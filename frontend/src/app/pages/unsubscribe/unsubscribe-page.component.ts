import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { SeoService } from '../../services/seo.service';

type State = 'working' | 'done' | 'invalid' | 'resubscribed' | 'error';

/**
 * The page behind the unsubscribe link in every newsletter footer. Opening the link unsubscribes
 * straight away (one click); a button undoes it.
 */
@Component({
  selector: 'app-unsubscribe-page',
  imports: [RouterLink],
  template: `
    <div class="unsub">
      @switch (state()) {
        @case ('working') {
          <p>One moment…</p>
        }
        @case ('done') {
          <h1 class="unsub__title">You've been unsubscribed</h1>
          <p>You won't get any more newsletters from Eden Relics. Emails about orders you place still come as usual.</p>
          <p>Changed your mind?</p>
          <button type="button" class="unsub__btn" (click)="resubscribe()" [disabled]="busy()">Keep me subscribed</button>
        }
        @case ('resubscribed') {
          <h1 class="unsub__title">You're back on the list</h1>
          <p>Thanks for staying. You'll hear about new pieces as they arrive.</p>
          <a routerLink="/shop" class="unsub__link">Browse the shop</a>
        }
        @case ('invalid') {
          <h1 class="unsub__title">This link isn't working</h1>
          <p>The unsubscribe link may be incomplete. Try it again from the email, or <a routerLink="/contact" class="unsub__link">contact us</a> and we'll take you off the list.</p>
        }
        @default {
          <h1 class="unsub__title">Something went wrong</h1>
          <p>Please try the link again in a minute, or <a routerLink="/contact" class="unsub__link">contact us</a> and we'll take you off the list.</p>
        }
      }
    </div>
  `,
  styles: `
    .unsub {
      max-width: 560px;
      margin: 4rem auto;
      padding: 0 16px 4rem;
      color: var(--text-primary);
      line-height: 1.6;
    }
    .unsub__title {
      font-family: var(--font-display);
      font-size: 1.8rem;
      font-weight: 500;
      margin: 0 0 1rem;
    }
    .unsub__btn {
      background: var(--text-primary);
      color: var(--text-inverse);
      border: none;
      padding: 0.8rem 1.6rem;
      font: inherit;
      cursor: pointer;
    }
    .unsub__btn:disabled {
      opacity: 0.6;
    }
    .unsub__link {
      color: var(--accent);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UnsubscribePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly http = inject(HttpClient);
  private readonly seo = inject(SeoService);
  private readonly url = `${environment.apiUrl}/api/mailing-list`;

  readonly state = signal<State>('working');
  readonly busy = signal(false);
  private token = '';

  ngOnInit(): void {
    this.seo.updateTags({ title: 'Unsubscribe', url: '/unsubscribe', noIndex: true });
    this.token = this.route.snapshot.queryParamMap.get('t') ?? '';
    if (!this.token) {
      this.state.set('invalid');
      return;
    }
    this.http.post(`${this.url}/unsubscribe-token`, { token: this.token }).subscribe({
      next: () => this.state.set('done'),
      error: (e) => this.state.set(e?.status === 404 ? 'invalid' : 'error'),
    });
  }

  resubscribe(): void {
    this.busy.set(true);
    this.http.post(`${this.url}/resubscribe-token`, { token: this.token }).subscribe({
      next: () => this.state.set('resubscribed'),
      error: () => {
        this.busy.set(false);
        this.state.set('error');
      },
    });
  }
}

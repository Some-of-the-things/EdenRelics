import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  AdminDiscountCodes,
  SignupOfferService,
  SignupOfferSettings,
} from '../../services/signup-offer.service';

type CodeFilter = 'all' | 'unused' | 'used' | 'expired';

/**
 * Task 4: the newsletter sign-up discount. Switch it on or off, set the discount, minimum order,
 * how long codes last, what they leave out and the wording on the site; and see the codes sent.
 * Changing the offer never changes codes already sent.
 */
@Component({
  selector: 'app-admin-signup-offer',
  imports: [DatePipe, FormsModule],
  templateUrl: './admin-signup-offer.component.html',
  styleUrl: './admin-newsletter.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminSignupOfferComponent implements OnInit {
  private readonly api = inject(SignupOfferService);

  readonly settings = signal<SignupOfferSettings | null>(null);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly codes = signal<AdminDiscountCodes | null>(null);
  readonly filter = signal<CodeFilter>('all');

  /** What the sign-up box will say, as the form stands. */
  readonly headlinePreview = computed(() => {
    const s = this.settings();
    if (!s) {
      return '';
    }
    if (!s.enabled) {
      return 'New Arrivals, Straight to Your Inbox (no offer)';
    }
    if (s.signupText?.trim()) {
      return s.signupText.trim();
    }
    const amount = s.kind === 'fixed' ? `£${this.trim(s.amount)} off` : `${this.trim(s.amount)}% off`;
    const over = s.minOrder ? ` over £${this.trim(s.minOrder)}` : '';
    return `Join and get ${amount} your first order${over}`;
  });

  ngOnInit(): void {
    this.api.getSettings().subscribe({
      next: (s) => this.settings.set(s),
      error: () => this.error.set('Could not load the offer settings.'),
    });
    this.loadCodes();
  }

  update<K extends keyof SignupOfferSettings>(key: K, value: SignupOfferSettings[K]): void {
    const s = this.settings();
    if (s) {
      this.settings.set({ ...s, [key]: value });
      this.message.set(null);
    }
  }

  numberOrNull(value: string): number | null {
    const n = Number(value);
    return value.trim() === '' || Number.isNaN(n) ? null : n;
  }

  save(): void {
    const s = this.settings();
    if (!s) {
      return;
    }
    if (s.enabled && !confirm(`Offer "${this.headlinePreview()}" to every new subscriber?`)) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.api.saveSettings({ ...s, minOrder: s.minOrder || null, signupText: s.signupText?.trim() || null }).subscribe({
      next: (saved) => {
        this.settings.set(saved);
        this.saving.set(false);
        this.message.set(saved.enabled ? 'Saved. New subscribers now get a code.' : 'Saved. The offer is off: no codes are sent and the site makes no offer.');
      },
      error: (e) => {
        this.saving.set(false);
        this.error.set(e?.error?.error ?? 'Could not save the offer.');
      },
    });
  }

  setFilter(f: CodeFilter): void {
    this.filter.set(f);
    this.loadCodes();
  }

  loadCodes(): void {
    const f = this.filter();
    this.api.getCodes(f === 'all' ? null : f).subscribe({
      next: (c) => this.codes.set(c),
      error: () => {},
    });
  }

  private trim(n: number): string {
    return Number.isInteger(n) ? `${n}` : n.toFixed(2);
  }
}

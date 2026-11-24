import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { FocusTrapDirective } from '../../directives/focus-trap.directive';
import { ShopEngagementService } from '../../services/shop-engagement.service';
import { NewsletterService } from '../../services/newsletter.service';
import { SignupOfferService } from '../../services/signup-offer.service';

/**
 * Newsletter-signup discount pop-up. Shown when {@link ShopEngagementService} decides the visitor
 * has actively browsed the shop past the threshold, and only while the admin has the sign-up
 * offer switched on. The wording comes from that offer. Each subscriber's code is unique and
 * goes to their inbox, so it isn't shown here.
 */
@Component({
  selector: 'app-discount-popup',
  imports: [FormsModule, FocusTrapDirective],
  templateUrl: './discount-popup.component.html',
  styleUrl: './discount-popup.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DiscountPopupComponent {
  private readonly engagement = inject(ShopEngagementService);
  private readonly newsletter = inject(NewsletterService);
  private readonly offers = inject(SignupOfferService);

  readonly offer = toSignal(this.offers.getOffer(), { initialValue: null });

  /** Trigger from the engagement tracker (latches true once), and only with an offer to make. */
  readonly show = computed(() => this.engagement.showPopup() && !!this.offer()?.discountOn);
  /** Local close state, so the success view can linger. */
  readonly closed = signal(false);

  email = '';
  readonly submitting = signal(false);
  readonly subscribed = signal(false);
  readonly error = signal(false);

  submit(): void {
    const email = this.email.trim();
    if (!email || this.submitting()) {
      return;
    }
    this.submitting.set(true);
    this.error.set(false);
    this.newsletter.subscribe(email, 'Discount Popup').subscribe({
      next: () => {
        this.submitting.set(false);
        this.subscribed.set(true);
        this.engagement.resolve('subscribed');
      },
      error: () => {
        this.submitting.set(false);
        this.error.set(true);
      },
    });
  }

  /** Close the dialog. If they never subscribed, record a dismissal so it won't nag again. */
  dismiss(): void {
    this.closed.set(true);
    if (!this.subscribed()) {
      this.engagement.resolve('dismissed');
    }
  }
}

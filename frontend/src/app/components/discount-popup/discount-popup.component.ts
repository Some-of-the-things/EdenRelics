import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FocusTrapDirective } from '../../directives/focus-trap.directive';
import { ShopEngagementService } from '../../services/shop-engagement.service';
import { NewsletterService } from '../../services/newsletter.service';

/**
 * Must match the Stripe promotion code (Stripe Dashboard → Coupons) so the code
 * shown/emailed here is actually redeemable at checkout.
 */
const DISCOUNT_CODE = 'WELCOME15';

/**
 * Newsletter-signup discount pop-up. Rendered once (per app root) and shown when
 * {@link ShopEngagementService} decides the visitor has actively browsed the shop
 * past the threshold. Reuses the app's focus-trap dialog pattern.
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

  /** Trigger from the engagement tracker (latches true once). */
  readonly show = this.engagement.showPopup;
  /** Local close state, so the success view can linger to show the code. */
  readonly closed = signal(false);
  readonly code = DISCOUNT_CODE;

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

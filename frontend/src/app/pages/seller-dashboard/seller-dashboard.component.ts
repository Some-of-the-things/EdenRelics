import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SellerService, Seller, SellerListing, SellerListingCreate } from '../../services/seller.service';

const EMPTY_LISTING: SellerListingCreate = {
  name: '', description: '', price: 0, era: '', category: '', size: '', condition: '', imageUrl: '',
};

@Component({
  selector: 'app-seller-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  styleUrl: './seller-dashboard.component.scss',
  template: `
    <section class="seller">
      <h1 class="seller__title">Seller dashboard</h1>

      @if (loading()) {
        <p class="seller__muted">Loading…</p>
      } @else if (!seller()) {
        <div class="seller__card">
          <h2 class="seller__heading">Apply to sell on Eden Relics</h2>
          <p class="seller__lead">Tell us about your vintage business. Applications are reviewed before you can list.</p>
          <form class="form" (ngSubmit)="apply()">
            <label class="field">
              <span class="field__label">Business name</span>
              <input class="field__input" name="businessName" [(ngModel)]="businessName" required maxlength="200" />
            </label>
            <label class="field">
              <span class="field__label">About your shop</span>
              <textarea class="field__input" name="bio" [(ngModel)]="bio" rows="4" maxlength="4000"></textarea>
            </label>
            <label class="field">
              <span class="field__label">Contact email</span>
              <input class="field__input" name="contactEmail" type="email" [(ngModel)]="contactEmail" />
            </label>
            <button class="btn btn--primary" type="submit" [disabled]="!businessName.trim()">Submit application</button>
          </form>
        </div>
      } @else {
        <div class="seller__card">
          <p class="seller__status-line">
            <strong>{{ seller()!.businessName }}</strong>
            <span class="badge badge--{{ seller()!.approvalStatus.toLowerCase() }}">{{ seller()!.approvalStatus }}</span>
          </p>

          @if (seller()!.approvalStatus === 'Applied') {
            <p class="seller__lead">Your application is with our team for review. You’ll be able to add listings once approved.</p>
          } @else if (seller()!.approvalStatus === 'Rejected') {
            <p class="seller__lead">Your application wasn’t approved this time. Please contact us if you’d like to discuss it.</p>
          } @else if (seller()!.approvalStatus === 'Suspended') {
            <p class="seller__lead">Your seller account is currently suspended. Please contact us.</p>
          } @else {
            @if (!seller()!.connectOnboardingComplete) {
              <div class="notice">
                <span><strong>Set up payments</strong> to let your listings go live — payouts are handled by Stripe,
                  and held until each buyer’s 14-day return window closes.</span>
                <button class="btn btn--primary" (click)="setupPayments()">Set up payments with Stripe</button>
              </div>
            } @else {
              <p class="notice--ok">✓ Payments set up — your approved listings can go live.</p>
            }

            <a class="seller__profile-link" [routerLink]="['/sellers', seller()!.slug]">View your public profile →</a>

            <div class="seller__row">
              <h2 class="seller__heading">Your listings</h2>
              <button class="btn btn--ghost" (click)="showListingForm.set(!showListingForm())">
                {{ showListingForm() ? 'Cancel' : 'Add a listing' }}
              </button>
            </div>

            @if (showListingForm()) {
              <form class="form" (ngSubmit)="createListing()">
                <label class="field"><span class="field__label">Name</span>
                  <input class="field__input" name="ln" [(ngModel)]="newListing.name" required /></label>
                <label class="field"><span class="field__label">Description</span>
                  <textarea class="field__input" name="ld" [(ngModel)]="newListing.description" rows="3"></textarea></label>
                <label class="field"><span class="field__label">Price (£)</span>
                  <input class="field__input" name="lp" type="number" [(ngModel)]="newListing.price" min="0" step="0.01" /></label>
                <div class="field__row">
                  <label class="field"><span class="field__label">Era</span>
                    <input class="field__input" name="le" [(ngModel)]="newListing.era" placeholder="1970s" /></label>
                  <label class="field"><span class="field__label">Category</span>
                    <input class="field__input" name="lc" [(ngModel)]="newListing.category" placeholder="70s" /></label>
                  <label class="field"><span class="field__label">Size</span>
                    <input class="field__input" name="ls" [(ngModel)]="newListing.size" /></label>
                  <label class="field"><span class="field__label">Condition</span>
                    <input class="field__input" name="lco" [(ngModel)]="newListing.condition" /></label>
                </div>
                <label class="field"><span class="field__label">Image URL</span>
                  <input class="field__input" name="li" [(ngModel)]="newListing.imageUrl" required /></label>
                <button class="btn btn--primary" type="submit"
                  [disabled]="!newListing.name.trim() || !newListing.imageUrl.trim()">Submit for review</button>
              </form>
            }

            @if (listings().length === 0) {
              <p class="seller__muted">No listings yet. Add your first piece — it goes live once our team approves it.</p>
            } @else {
              <table class="listings">
                <thead><tr><th>Item</th><th>Price</th><th>Status</th><th>Moderation</th></tr></thead>
                <tbody>
                  @for (l of listings(); track l.id) {
                    <tr>
                      <td>{{ l.name }}</td>
                      <td>£{{ l.price }}</td>
                      <td>{{ l.status }}</td>
                      <td>
                        <span class="badge badge--{{ l.moderationStatus.toLowerCase() }}">{{ l.moderationStatus }}</span>
                        @if (l.moderationNote) { <div class="listings__note">{{ l.moderationNote }}</div> }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          }
        </div>
      }

      @if (error()) { <p class="seller__error">{{ error() }}</p> }
    </section>
  `,
})
export class SellerDashboardComponent implements OnInit {
  private readonly sellers = inject(SellerService);

  readonly loading = signal(true);
  readonly seller = signal<Seller | null>(null);
  readonly listings = signal<SellerListing[]>([]);
  readonly showListingForm = signal(false);
  readonly error = signal('');

  businessName = '';
  bio = '';
  contactEmail = '';
  newListing: SellerListingCreate = { ...EMPTY_LISTING };

  ngOnInit(): void {
    this.sellers.mySeller().subscribe({
      next: (s) => {
        this.seller.set(s);
        this.loading.set(false);
        if (s.approvalStatus === 'Approved') {
          this.loadListings();
          this.maybeRefreshConnect();
        }
      },
      error: () => {
        this.seller.set(null);
        this.loading.set(false);
      },
    });
  }

  private loadListings(): void {
    this.sellers.myListings().subscribe((l) => this.listings.set(l));
  }

  apply(): void {
    if (!this.businessName.trim()) {
      return;
    }
    this.error.set('');
    this.sellers
      .apply({
        businessName: this.businessName,
        bio: this.bio || undefined,
        contactEmail: this.contactEmail || undefined,
      })
      .subscribe({
        next: (s) => this.seller.set(s),
        error: () => this.error.set('Sorry — we couldn’t submit your application. Please try again.'),
      });
  }

  setupPayments(): void {
    this.sellers.connectStart().subscribe({
      next: (res) => {
        if (res.url && typeof window !== 'undefined') {
          window.location.href = res.url;
        }
      },
      error: () => this.error.set('Could not start payment setup. Please try again.'),
    });
  }

  private maybeRefreshConnect(): void {
    if (typeof window === 'undefined' || !new URLSearchParams(window.location.search).has('connect')) {
      return;
    }
    this.sellers.connectRefresh().subscribe((r) => {
      const s = this.seller();
      if (s) {
        this.seller.set({ ...s, connectOnboardingComplete: r.onboardingComplete });
      }
    });
  }

  createListing(): void {
    if (!this.newListing.name.trim() || !this.newListing.imageUrl.trim()) {
      return;
    }
    this.error.set('');
    this.sellers.createListing({ ...this.newListing }).subscribe({
      next: () => {
        this.showListingForm.set(false);
        this.newListing = { ...EMPTY_LISTING };
        this.loadListings();
      },
      error: () => this.error.set('Sorry — we couldn’t create that listing. Please try again.'),
    });
  }
}

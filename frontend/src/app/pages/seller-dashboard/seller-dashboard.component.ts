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
  template: `
    <section class="seller-dashboard" style="max-width:900px;margin:2rem auto;padding:0 1rem;">
      <h1>Seller dashboard</h1>

      @if (loading()) {
        <p>Loading…</p>
      } @else if (!seller()) {
        <!-- Not yet a seller: application form -->
        <h2>Apply to sell on Eden Relics</h2>
        <p>Tell us about your vintage business. Applications are reviewed before you can list.</p>
        <form (ngSubmit)="apply()" #f="ngForm" class="stack">
          <label>Business name*
            <input name="businessName" [(ngModel)]="businessName" required maxlength="200" />
          </label>
          <label>About your shop
            <textarea name="bio" [(ngModel)]="bio" rows="4" maxlength="4000"></textarea>
          </label>
          <label>Contact email
            <input name="contactEmail" type="email" [(ngModel)]="contactEmail" />
          </label>
          <button type="submit" [disabled]="!businessName.trim()">Submit application</button>
        </form>
      } @else {
        <!-- Existing seller: status + listings -->
        <p><strong>{{ seller()!.businessName }}</strong>
          — status: <span class="status status-{{ seller()!.approvalStatus.toLowerCase() }}">{{ seller()!.approvalStatus }}</span>
        </p>

        @if (seller()!.approvalStatus === 'Applied') {
          <p>Your application is with our team for review. You’ll be able to add listings once approved.</p>
        } @else if (seller()!.approvalStatus === 'Rejected') {
          <p>Your application wasn’t approved this time. Please contact us if you’d like to discuss it.</p>
        } @else if (seller()!.approvalStatus === 'Suspended') {
          <p>Your seller account is currently suspended. Please contact us.</p>
        } @else {
          <!-- Approved -->
          @if (!seller()!.connectOnboardingComplete) {
            <div style="background:#f5eccf;padding:0.75rem 1rem;border-radius:8px;margin-bottom:1rem;">
              <strong>Set up payments</strong> to let your listings go live — payouts are handled by Stripe,
              and held until each buyer’s 14-day return window closes.
              <button (click)="setupPayments()" style="margin-left:0.5rem;">Set up payments with Stripe</button>
            </div>
          } @else {
            <p style="color:#3a7d3a;">✓ Payments set up — your approved listings can go live.</p>
          }
          <div style="display:flex;justify-content:space-between;align-items:center;">
            <h2>Your listings</h2>
            <button (click)="showListingForm.set(!showListingForm())">
              {{ showListingForm() ? 'Cancel' : 'Add a listing' }}
            </button>
          </div>
          <p><a [routerLink]="['/sellers', seller()!.slug]">View your public profile →</a></p>

          @if (showListingForm()) {
            <form (ngSubmit)="createListing()" class="stack" style="border:1px solid #ddd;padding:1rem;border-radius:8px;">
              <label>Name* <input name="ln" [(ngModel)]="newListing.name" required /></label>
              <label>Description <textarea name="ld" [(ngModel)]="newListing.description" rows="3"></textarea></label>
              <label>Price (£)* <input name="lp" type="number" [(ngModel)]="newListing.price" min="0" step="0.01" /></label>
              <div style="display:flex;gap:0.5rem;flex-wrap:wrap;">
                <label>Era <input name="le" [(ngModel)]="newListing.era" placeholder="1970s" /></label>
                <label>Category <input name="lc" [(ngModel)]="newListing.category" placeholder="70s" /></label>
                <label>Size <input name="ls" [(ngModel)]="newListing.size" /></label>
                <label>Condition <input name="lco" [(ngModel)]="newListing.condition" /></label>
              </div>
              <label>Image URL* <input name="li" [(ngModel)]="newListing.imageUrl" required /></label>
              <button type="submit" [disabled]="!newListing.name.trim() || !newListing.imageUrl.trim()">
                Submit for review
              </button>
            </form>
          }

          @if (listings().length === 0) {
            <p>No listings yet. Add your first piece — it goes live once our team approves it.</p>
          } @else {
            <table style="width:100%;border-collapse:collapse;">
              <thead><tr><th align="left">Item</th><th>Price</th><th>Status</th><th>Moderation</th></tr></thead>
              <tbody>
                @for (l of listings(); track l.id) {
                  <tr style="border-top:1px solid #eee;">
                    <td>{{ l.name }}</td>
                    <td align="center">£{{ l.price }}</td>
                    <td align="center">{{ l.status }}</td>
                    <td align="center">
                      <span class="status status-{{ l.moderationStatus.toLowerCase() }}">{{ l.moderationStatus }}</span>
                      @if (l.moderationNote) { <div style="font-size:0.8em;color:#a33;">{{ l.moderationNote }}</div> }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        }
      }

      @if (error()) { <p style="color:#a33;">{{ error() }}</p> }
    </section>
  `,
  styles: [`
    .stack { display:flex; flex-direction:column; gap:0.75rem; max-width:520px; }
    .stack label { display:flex; flex-direction:column; gap:0.25rem; font-size:0.9rem; }
    .status { padding:0.1rem 0.5rem; border-radius:999px; font-size:0.8rem; background:#eee; }
    .status-approved { background:#dce7dc; }
    .status-pendingreview, .status-applied { background:#f5eccf; }
    .status-rejected, .status-suspended { background:#f2d9d9; }
  `],
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
        // 404 — this user hasn't applied yet; show the application form.
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

  /** After returning from Stripe onboarding (?connect=...), refresh the seller's payment status. */
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

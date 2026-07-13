import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SellerService, Seller, SellerListing } from '../../services/seller.service';

@Component({
  selector: 'app-admin-sellers',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section style="max-width:1000px;margin:2rem auto;padding:0 1rem;">
      <h1>Seller moderation</h1>

      <h2>Applications awaiting review</h2>
      @if (pendingSellers().length === 0) {
        <p>No applications awaiting review.</p>
      } @else {
        <table style="width:100%;border-collapse:collapse;">
          <thead><tr><th align="left">Business</th><th align="left">Slug</th><th align="left">Contact</th><th></th></tr></thead>
          <tbody>
            @for (s of pendingSellers(); track s.id) {
              <tr style="border-top:1px solid #eee;">
                <td>{{ s.businessName }}</td>
                <td>{{ s.slug }}</td>
                <td>{{ s.contactEmail || '—' }}</td>
                <td align="right">
                  <button (click)="approveSeller(s)">Approve</button>
                  <button (click)="rejectSeller(s)">Reject</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      }

      <h2 style="margin-top:2rem;">Listings awaiting review</h2>
      @if (pendingListings().length === 0) {
        <p>No listings awaiting review.</p>
      } @else {
        <table style="width:100%;border-collapse:collapse;">
          <thead><tr><th align="left">Item</th><th>Price</th><th align="left">Era</th><th></th></tr></thead>
          <tbody>
            @for (l of pendingListings(); track l.id) {
              <tr style="border-top:1px solid #eee;">
                <td style="display:flex;align-items:center;gap:0.5rem;">
                  <img [src]="l.imageUrl" alt="" width="40" height="50" style="object-fit:cover;border-radius:4px;" />
                  {{ l.name }}
                </td>
                <td align="center">£{{ l.price }}</td>
                <td>{{ l.era }}</td>
                <td align="right">
                  <button (click)="approveListing(l)">Approve</button>
                  <button (click)="rejectListing(l)">Reject</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      }

      @if (message()) { <p style="color:#484;">{{ message() }}</p> }
    </section>
  `,
})
export class AdminSellersComponent implements OnInit {
  private readonly sellers = inject(SellerService);

  readonly pendingSellers = signal<Seller[]>([]);
  readonly pendingListings = signal<SellerListing[]>([]);
  readonly message = signal('');

  ngOnInit(): void {
    this.reload();
  }

  private reload(): void {
    this.sellers.listSellers('Applied').subscribe((s) => this.pendingSellers.set(s));
    this.sellers.moderationQueue('PendingReview').subscribe((l) => this.pendingListings.set(l));
  }

  approveSeller(s: Seller): void {
    this.sellers.approveSeller(s.id).subscribe(() => {
      this.message.set(`Approved ${s.businessName}.`);
      this.reload();
    });
  }

  rejectSeller(s: Seller): void {
    const note = prompt(`Reject ${s.businessName}? Optional reason:`) ?? undefined;
    this.sellers.rejectSeller(s.id, note).subscribe(() => {
      this.message.set(`Rejected ${s.businessName}.`);
      this.reload();
    });
  }

  approveListing(l: SellerListing): void {
    this.sellers.approveListing(l.id).subscribe(() => {
      this.message.set(`Published “${l.name}”.`);
      this.reload();
    });
  }

  rejectListing(l: SellerListing): void {
    const note = prompt(`Reject “${l.name}”? Optional reason (shown to the seller):`) ?? undefined;
    this.sellers.rejectListing(l.id, note).subscribe(() => {
      this.message.set(`Rejected “${l.name}”.`);
      this.reload();
    });
  }
}

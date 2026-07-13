import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SellerService, Seller, SellerListing } from '../../services/seller.service';

@Component({
  selector: 'app-admin-sellers',
  standalone: true,
  imports: [CommonModule],
  styleUrl: './admin-sellers.component.scss',
  template: `
    <section class="mod">
      <h1 class="mod__title">Seller moderation</h1>

      <h2 class="mod__heading">Applications awaiting review</h2>
      @if (pendingSellers().length === 0) {
        <p class="mod__empty">No applications awaiting review.</p>
      } @else {
        <table class="table">
          <thead><tr><th>Business</th><th>Slug</th><th>Contact</th><th></th></tr></thead>
          <tbody>
            @for (s of pendingSellers(); track s.id) {
              <tr>
                <td>{{ s.businessName }}</td>
                <td>{{ s.slug }}</td>
                <td>{{ s.contactEmail || '—' }}</td>
                <td class="table__actions">
                  <button class="btn btn--primary" (click)="approveSeller(s)">Approve</button>
                  <button class="btn btn--ghost" (click)="rejectSeller(s)">Reject</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      }

      <h2 class="mod__heading">Listings awaiting review</h2>
      @if (pendingListings().length === 0) {
        <p class="mod__empty">No listings awaiting review.</p>
      } @else {
        <table class="table">
          <thead><tr><th>Item</th><th>Price</th><th>Era</th><th></th></tr></thead>
          <tbody>
            @for (l of pendingListings(); track l.id) {
              <tr>
                <td>
                  <span class="table__item">
                    <img class="table__thumb" [src]="l.imageUrl" alt="" width="40" height="50" />
                    {{ l.name }}
                  </span>
                </td>
                <td>£{{ l.price }}</td>
                <td>{{ l.era }}</td>
                <td class="table__actions">
                  <button class="btn btn--primary" (click)="approveListing(l)">Approve</button>
                  <button class="btn btn--ghost" (click)="rejectListing(l)">Reject</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      }

      @if (message()) { <p class="mod__message">{{ message() }}</p> }
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

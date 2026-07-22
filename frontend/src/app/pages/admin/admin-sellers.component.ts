import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SellerService, Seller, SellerListing } from '../../services/seller.service';

@Component({
  selector: 'app-admin-sellers',
  standalone: true,
  imports: [CommonModule],
  styleUrl: './admin-sellers.component.scss',
  templateUrl: './admin-sellers.component.html',
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

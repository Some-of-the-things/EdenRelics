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
  templateUrl: './seller-dashboard.component.html',
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

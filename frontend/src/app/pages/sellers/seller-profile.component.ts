import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Title, Meta } from '@angular/platform-browser';
import { SellerService, Seller, SellerProductCard } from '../../services/seller.service';

@Component({
  selector: 'app-seller-profile',
  standalone: true,
  imports: [CommonModule, RouterLink],
  styleUrl: './seller-profile.component.scss',
  templateUrl: './seller-profile.component.html',
})
export class SellerProfileComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly sellers = inject(SellerService);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);

  readonly seller = signal<Seller | null>(null);
  readonly products = signal<SellerProductCard[]>([]);
  readonly notFound = signal(false);

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('slug') ?? '';
    this.sellers.publicProfile(slug).subscribe({
      next: (s) => {
        this.seller.set(s);
        this.title.setTitle(`${s.businessName} — Vintage Seller on Eden Relics`);
        this.meta.updateTag({
          name: 'description',
          content: s.bio
            ? s.bio.slice(0, 155)
            : `Shop curated vintage from ${s.businessName} on Eden Relics.`,
        });
      },
      error: () => this.notFound.set(true),
    });
    this.sellers.publicProducts(slug).subscribe({
      next: (p) => this.products.set(p),
      error: () => this.products.set([]),
    });
  }
}

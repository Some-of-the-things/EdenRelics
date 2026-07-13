import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Title, Meta } from '@angular/platform-browser';
import { SellerService, Seller, SellerProductCard } from '../../services/seller.service';

@Component({
  selector: 'app-seller-profile',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    @if (notFound()) {
      <section style="max-width:800px;margin:3rem auto;padding:0 1rem;text-align:center;">
        <h1>Seller not found</h1>
        <p>This seller profile isn’t available.</p>
        <a routerLink="/shop">Browse the shop →</a>
      </section>
    } @else if (seller(); as s) {
      <section style="max-width:1000px;margin:2rem auto;padding:0 1rem;">
        <header style="display:flex;align-items:center;gap:1rem;">
          @if (s.logoUrl) {
            <img [src]="s.logoUrl" [alt]="s.businessName" width="72" height="72" style="border-radius:50%;object-fit:cover;" />
          }
          <div>
            <h1 style="margin:0;">{{ s.businessName }}</h1>
            <p style="margin:0.25rem 0 0;color:#666;">Curated vintage on Eden Relics</p>
          </div>
        </header>

        @if (s.bio) {
          <p style="margin-top:1rem;max-width:60ch;">{{ s.bio }}</p>
        }

        <h2 style="margin-top:2rem;">Available pieces</h2>
        @if (products().length === 0) {
          <p>No pieces available right now — check back soon.</p>
        } @else {
          <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:1rem;">
            @for (p of products(); track p.id) {
              <a [routerLink]="['/product', p.slug]" style="text-decoration:none;color:inherit;">
                <img [src]="p.imageUrl" [alt]="p.name" style="width:100%;aspect-ratio:4/5;object-fit:cover;border-radius:8px;" />
                <div style="margin-top:0.4rem;font-size:0.9rem;">{{ p.name }}</div>
                <div style="font-weight:600;">
                  @if (p.salePrice) { <span>£{{ p.salePrice }}</span> <s style="color:#999;font-weight:400;">£{{ p.price }}</s> }
                  @else { £{{ p.price }} }
                </div>
              </a>
            }
          </div>
        }
      </section>
    } @else {
      <section style="max-width:800px;margin:3rem auto;padding:0 1rem;"><p>Loading…</p></section>
    }
  `,
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

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
  template: `
    @if (notFound()) {
      <section class="notfound">
        <h1 class="notfound__title">Seller not found</h1>
        <p>This seller profile isn’t available.</p>
        <a routerLink="/shop">Browse the shop →</a>
      </section>
    } @else if (seller(); as s) {
      <section class="profile">
        <header class="profile__header">
          @if (s.logoUrl) {
            <img class="profile__logo" [src]="s.logoUrl" [alt]="s.businessName" width="80" height="80" />
          }
          <div>
            <h1 class="profile__name">{{ s.businessName }}</h1>
            <p class="profile__tagline">Curated vintage on Eden Relics</p>
          </div>
        </header>

        @if (s.bio) {
          <p class="profile__bio">{{ s.bio }}</p>
        }

        <h2 class="profile__heading">Available pieces</h2>
        @if (products().length === 0) {
          <p class="profile__empty">No pieces available right now — check back soon.</p>
        } @else {
          <div class="grid">
            @for (p of products(); track p.id) {
              <a class="card" [routerLink]="['/product', p.slug]">
                <img class="card__img" [src]="p.imageUrl" [alt]="p.name" />
                <div class="card__name">{{ p.name }}</div>
                <div class="card__price">
                  @if (p.salePrice) {
                    <span class="card__sale">£{{ p.salePrice }}</span><span class="card__was">£{{ p.price }}</span>
                  } @else { £{{ p.price }} }
                </div>
              </a>
            }
          </div>
        }
      </section>
    } @else {
      <section class="profile"><p class="profile__loading">Loading…</p></section>
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

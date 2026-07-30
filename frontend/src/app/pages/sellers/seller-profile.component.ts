import { Component, inject, signal, OnInit, RESPONSE_INIT } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Title, Meta } from '@angular/platform-browser';
import { HttpErrorResponse } from '@angular/common/http';
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
  // Present only during server render; null in the browser.
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

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
      error: (err: unknown) => this.markNotFound(err),
    });
    this.sellers.publicProducts(slug).subscribe({
      next: (p) => this.products.set(p),
      error: () => this.products.set([]),
    });
  }

  /**
   * An unknown seller slug used to render the "not found" view with a 200, which
   * makes it a soft 404: Google indexes it as a real page, so every bad or stale
   * slug becomes a thin duplicate. Mirrors the category-hub behaviour — set a
   * genuine 404 on the server response and mark the page noindex either way, so
   * a transient API failure is never indexed either.
   */
  private markNotFound(err: unknown): void {
    this.notFound.set(true);
    this.title.setTitle('Seller not found — Eden Relics');
    this.meta.updateTag({ name: 'robots', content: 'noindex, nofollow' });

    // Only a genuine "no such seller" becomes a 404 — answering an API outage
    // with one would invite Google to drop live seller pages. Other failures
    // keep the 200 and rely on the noindex above: a 5xx here would make
    // worker.ts treat the whole render as failed and serve the CSR shell, which
    // returns 200 regardless, so the status would not reach the crawler anyway.
    if (this.responseInit && err instanceof HttpErrorResponse && err.status === 404) {
      this.responseInit.status = 404;
    }
  }
}

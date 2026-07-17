import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { productStatusLabel, resolveProductStatus } from '../../utils/product-status';
import { TopPicksService, TopPickItem } from '../../services/top-picks.service';

/**
 * Admin curation for the "Our Top Picks" edit. Curate the ordered list of pieces (and which show on
 * the homepage strip) here; the list persists to the DB independently of the on/off switch, so you
 * can build it before going live. Picks are stored by product ID (so they stay unambiguous across
 * sellers once the marketplace is live); the public surfaces render only currently live pieces.
 * Embedded as the "Top Picks" tab of the admin page.
 */
@Component({
  selector: 'app-admin-top-picks',
  imports: [FormsModule, CurrencyPipe],
  templateUrl: './admin-top-picks.component.html',
  styleUrl: './admin-top-picks.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminTopPicksComponent implements OnInit {
  private readonly store = inject(ProductStore);
  private readonly topPicks = inject(TopPicksService);

  /** Whether the public Top Picks surfaces are currently switched on (TopPicks:Enabled). */
  readonly enabled = signal(false);
  /** The working (unsaved) curated list, in display order. */
  readonly picks = signal<TopPickItem[]>([]);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly error = signal<string | null>(null);

  /** Free-text filter for the "add a product" picker. */
  readonly search = signal('');

  /** Full catalogue keyed by product ID (admin clients receive every status from the API). */
  private readonly byId = computed<Map<string, Product>>(
    () => new Map(this.store.products().map((p) => [p.id, p])),
  );

  private readonly pickedIds = computed<Set<string>>(() => new Set(this.picks().map((p) => p.productId)));

  /** The picked rows, each resolved to its product (may be undefined if the product no longer exists). */
  readonly rows = computed(() =>
    this.picks().map((pick) => ({ ...pick, product: this.byId().get(pick.productId) })),
  );

  /** Live products matching the search, excluding ones already picked. Capped for a tidy list. */
  readonly candidates = computed<Product[]>(() => {
    const q = this.search().trim().toLowerCase();
    const picked = this.pickedIds();
    return this.store
      .products()
      .filter((p) => !picked.has(p.id) && resolveProductStatus(p) === 'live')
      .filter((p) =>
        q === '' ||
        p.name.toLowerCase().includes(q) ||
        (p.sku ?? '').toLowerCase().includes(q),
      )
      .slice(0, 24);
  });

  ngOnInit(): void {
    this.topPicks.getAdmin().subscribe({
      next: (res) => {
        this.enabled.set(res.enabled);
        this.picks.set(res.items);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(this.explain(err.status));
      },
    });
  }

  statusLabel(product: Product): string {
    return productStatusLabel(resolveProductStatus(product));
  }

  isLive(product: Product | undefined): boolean {
    return !!product && resolveProductStatus(product) === 'live';
  }

  add(productId: string): void {
    if (this.pickedIds().has(productId)) {
      return;
    }
    this.picks.update((list) => [...list, { productId, featured: false }]);
    this.saved.set(false);
  }

  remove(productId: string): void {
    this.picks.update((list) => list.filter((p) => p.productId !== productId));
    this.saved.set(false);
  }

  toggleFeatured(productId: string): void {
    this.picks.update((list) =>
      list.map((p) => (p.productId === productId ? { ...p, featured: !p.featured } : p)),
    );
    this.saved.set(false);
  }

  move(index: number, delta: number): void {
    const target = index + delta;
    this.picks.update((list) => {
      if (target < 0 || target >= list.length) {
        return list;
      }
      const next = [...list];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
    this.saved.set(false);
  }

  save(): void {
    if (this.saving()) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.topPicks.save(this.picks()).subscribe({
      next: (res) => {
        this.saving.set(false);
        this.saved.set(true);
        this.enabled.set(res.enabled);
        this.picks.set(res.items);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(this.explain(err.status));
      },
    });
  }

  private explain(status: number): string {
    if (status === 401 || status === 403) {
      return 'Please sign in with an admin account to curate Top Picks.';
    }
    return 'Something went wrong saving. Please try again in a moment.';
  }
}

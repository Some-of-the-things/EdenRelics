import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Observable, of } from 'rxjs';
import { CategoriesPayload, CategoryService, HUB_NAMES, HubKey, SubCategory } from '../../services/category.service';
import { isMember, keywordMatches } from '../../utils/category-membership';

/**
 * The "Sub-categories" ticks on the product edit form: one list per hub. Ticks start from where
 * the piece sits now (its name's match words plus any hand-made changes). Nothing is sent until
 * the product is saved, when the admin page calls {@link save}; the backend then stores only the
 * ticks that differ from the match words.
 */
@Component({
  selector: 'app-admin-product-categories',
  templateUrl: './admin-product-categories.component.html',
  styleUrl: './admin-product-categories.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminProductCategoriesComponent implements OnInit {
  private readonly categories = inject(CategoryService);

  /** The product being edited; null for a new piece (ticks are available once it's saved). */
  readonly productId = input<string | null>(null);
  /** The product's saved name, which the match words are checked against. */
  readonly productName = input('');

  readonly hubKeys: HubKey[] = ['designers', 'style', 'dresses'];
  readonly hubNames = HUB_NAMES;

  private readonly payload = signal<CategoriesPayload | null>(null);
  readonly error = signal<string | null>(null);
  /** Ticked sub-category ids; null until loaded. */
  readonly ticked = signal<ReadonlySet<string> | null>(null);
  private initialTicks = new Set<string>();

  readonly hubs = computed(() => this.payload()?.hubs ?? []);

  ngOnInit(): void {
    if (!this.productId()) {
      return;
    }
    this.categories.getAdmin().subscribe({
      next: (payload) => {
        this.payload.set(payload);
        const product = { id: this.productId()!, name: this.productName() };
        this.initialTicks = new Set(
          payload.hubs.flatMap((h) => h.subCategories).filter((s) => isMember(product, s)).map((s) => s.id),
        );
        this.ticked.set(new Set(this.initialTicks));
      },
      error: () => this.error.set('Could not load the sub-categories.'),
    });
  }

  subCategoriesOf(key: HubKey): SubCategory[] {
    return this.hubs().find((h) => h.key === key)?.subCategories ?? [];
  }

  isTicked(id: string): boolean {
    return this.ticked()?.has(id) ?? false;
  }

  /** Shown next to a tick so it's clear which ones come from the name. */
  matchesName(sub: SubCategory): boolean {
    return keywordMatches(this.productName(), sub.includeKeywords, sub.excludeKeywords);
  }

  toggle(id: string): void {
    this.ticked.update((set) => {
      const next = new Set(set ?? []);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  /** Saves the ticks if they changed. Called by the admin page when the product is saved. */
  save(): Observable<void> {
    const id = this.productId();
    const ticked = this.ticked();
    if (!id || !ticked) {
      return of(undefined);
    }
    const unchanged = ticked.size === this.initialTicks.size && [...ticked].every((t) => this.initialTicks.has(t));
    if (unchanged) {
      return of(undefined);
    }
    return this.categories.setProductCategories(id, [...ticked]);
  }
}

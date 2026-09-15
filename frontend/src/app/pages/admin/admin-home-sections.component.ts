import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { resolveProductStatus } from '../../utils/product-status';
import { HomeSection, HomeSectionsService } from '../../services/home-sections.service';

/** A section being edited, with a stable key so the list can be reordered without losing focus. */
interface EditableSection extends HomeSection {
  key: number;
}

const MAX_SECTIONS = 12;
const MAX_PRODUCTS = 12;

/**
 * Arrange the product strips on the homepage: their order, wording, buttons and visibility, plus
 * any number of extra hand-picked sections. Top Picks and the latest collection are built in: they
 * can be renamed, moved and (the collection) hidden, but not removed, because their pieces are
 * curated elsewhere. Embedded as the "Home Sections" tab of the admin page.
 */
@Component({
  selector: 'app-admin-home-sections',
  imports: [FormsModule],
  templateUrl: './admin-home-sections.component.html',
  // Shares the Top Picks tab's styles for the rows, thumbnails and product picker.
  styleUrls: ['./admin-top-picks.component.scss', './admin-home-sections.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminHomeSectionsComponent implements OnInit {
  private readonly store = inject(ProductStore);
  private readonly homeSections = inject(HomeSectionsService);

  readonly maxProducts = MAX_PRODUCTS;
  readonly sections = signal<EditableSection[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly error = signal<string | null>(null);

  /** The custom section whose product picker is open, by key. */
  readonly pickingFor = signal<number | null>(null);
  readonly search = signal('');

  private nextKey = 0;

  private readonly byId = computed<Map<string, Product>>(
    () => new Map(this.store.products().map((p) => [p.id, p])),
  );

  readonly canAddSection = computed(() => this.sections().length < MAX_SECTIONS);

  /** Live products matching the search, excluding ones already in the section being edited. */
  readonly candidates = computed<Product[]>(() => {
    const key = this.pickingFor();
    const section = this.sections().find((s) => s.key === key);
    if (!section) {
      return [];
    }
    const picked = new Set(section.productIds);
    const q = this.search().trim().toLowerCase();
    return this.store
      .products()
      .filter((p) => !picked.has(p.id) && resolveProductStatus(p) === 'live')
      .filter((p) => q === '' || p.name.toLowerCase().includes(q) || (p.sku ?? '').toLowerCase().includes(q))
      .slice(0, 24);
  });

  ngOnInit(): void {
    this.homeSections.getAdmin().subscribe({
      next: (sections) => {
        this.sections.set(sections.map((s) => this.editable(s)));
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(this.explain(err));
      },
    });
  }

  product(id: string): Product | undefined {
    return this.byId().get(id);
  }

  isLive(product: Product | undefined): boolean {
    return !!product && resolveProductStatus(product) === 'live';
  }

  kindLabel(section: HomeSection): string {
    switch (section.kind) {
      case 'top-picks':
        return 'Top Picks: pieces come from the Top Picks tab';
      case 'latest-collection':
        return 'Latest collection: pieces come from the collection';
      case 'custom':
        return 'Your own section: pick the pieces below';
    }
  }

  update(key: number, changes: Partial<HomeSection>): void {
    this.sections.update((list) => list.map((s) => (s.key === key ? { ...s, ...changes } : s)));
    this.saved.set(false);
  }

  move(index: number, delta: number): void {
    const target = index + delta;
    this.sections.update((list) => {
      if (target < 0 || target >= list.length) {
        return list;
      }
      const next = [...list];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
    this.saved.set(false);
  }

  addSection(): void {
    if (!this.canAddSection()) {
      return;
    }
    const section = this.editable({
      kind: 'custom',
      eyebrow: '',
      title: 'New section',
      description: '',
      buttonText: '',
      buttonLink: '',
      visible: true,
      productIds: [],
    });
    this.sections.update((list) => [...list, section]);
    this.openPicker(section.key);
    this.saved.set(false);
  }

  removeSection(key: number): void {
    this.sections.update((list) => list.filter((s) => s.key !== key || s.kind !== 'custom'));
    if (this.pickingFor() === key) {
      this.pickingFor.set(null);
    }
    this.saved.set(false);
  }

  openPicker(key: number): void {
    this.pickingFor.set(this.pickingFor() === key ? null : key);
    this.search.set('');
  }

  addProduct(key: number, productId: string): void {
    this.sections.update((list) =>
      list.map((s) =>
        s.key === key && !s.productIds.includes(productId) && s.productIds.length < MAX_PRODUCTS
          ? { ...s, productIds: [...s.productIds, productId] }
          : s,
      ),
    );
    this.saved.set(false);
  }

  removeProduct(key: number, productId: string): void {
    this.sections.update((list) =>
      list.map((s) => (s.key === key ? { ...s, productIds: s.productIds.filter((id) => id !== productId) } : s)),
    );
    this.saved.set(false);
  }

  moveProduct(key: number, index: number, delta: number): void {
    this.sections.update((list) =>
      list.map((s) => {
        const target = index + delta;
        if (s.key !== key || target < 0 || target >= s.productIds.length) {
          return s;
        }
        const ids = [...s.productIds];
        [ids[index], ids[target]] = [ids[target], ids[index]];
        return { ...s, productIds: ids };
      }),
    );
    this.saved.set(false);
  }

  save(): void {
    if (this.saving()) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const payload: HomeSection[] = this.sections().map(({ key: _key, ...section }) => section);
    this.homeSections.save(payload).subscribe({
      next: (sections) => {
        this.saving.set(false);
        this.saved.set(true);
        this.pickingFor.set(null);
        this.sections.set(sections.map((s) => this.editable(s)));
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(this.explain(err));
      },
    });
  }

  private editable(section: HomeSection): EditableSection {
    return { ...section, key: this.nextKey++ };
  }

  private explain(err: { status: number; error?: { error?: string } }): string {
    if (err.status === 401 || err.status === 403) {
      return 'Please sign in with an admin account to edit the homepage.';
    }
    if (err.status === 400 && err.error?.error) {
      return err.error.error;
    }
    return 'Something went wrong saving. Please try again in a moment.';
  }
}

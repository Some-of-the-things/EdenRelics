import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProductStore } from '../../store/product.store';
import { Product } from '../../models/product.model';
import { productStatusLabel, resolveProductStatus } from '../../utils/product-status';
import {
  CategoriesPayload,
  CategoryService,
  HUB_NAMES,
  HubKey,
  SaveSubCategory,
  SubCategory,
  subCategoryPath,
} from '../../services/category.service';
import { MembershipReason, isMember, membershipReason } from '../../utils/category-membership';

interface SubCategoryForm {
  name: string;
  slug: string;
  tagline: string;
  intro: string;
  body: string;
  metaTitle: string;
  metaDescription: string;
  isVisible: boolean;
  includeWords: string;
  excludeWords: string;
}

/** Turns a name into a URL slug, the same way the backend does when the slug is left blank. */
export function slugify(name: string): string {
  return name
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '')
    .slice(0, 80)
    .replace(/-$/, '');
}

function splitWords(text: string): string[] {
  return text
    .split(',')
    .map((w) => w.trim().toLowerCase())
    .filter((w) => w.length > 0);
}

/**
 * The admin Categories tab: create, edit, reorder, hide and delete the sub-categories under
 * /designers, /style and /dresses, and choose which pieces sit in each. Pieces join automatically
 * when their name contains a match word; adding or removing a piece here overrides that.
 */
@Component({
  selector: 'app-admin-categories',
  imports: [FormsModule, RouterLink],
  templateUrl: './admin-categories.component.html',
  styleUrl: './admin-categories.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminCategoriesComponent implements OnInit {
  private readonly categories = inject(CategoryService);
  private readonly store = inject(ProductStore);

  readonly hubKeys: HubKey[] = ['designers', 'style', 'dresses'];
  readonly hubNames = HUB_NAMES;
  readonly subCategoryPath = subCategoryPath;

  readonly payload = signal<CategoriesPayload | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly activeHub = signal<HubKey>('designers');

  /** The sub-category being edited: its id, 'new', or null when the form is closed. */
  readonly editing = signal<string | 'new' | null>(null);
  form: SubCategoryForm = this.blankForm();
  /** Whether the slug should keep following the name (new pages, until the slug is typed in). */
  slugFollowsName = true;
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  /** The sub-category whose pieces are open, and the pieces panel state. */
  readonly piecesFor = signal<string | null>(null);
  readonly pieceSearch = signal('');
  readonly pieceFilter = signal<'in' | 'all'>('in');
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly piecesBusy = signal(false);

  readonly subs = computed<SubCategory[]>(
    () => this.payload()?.hubs.find((h) => h.key === this.activeHub())?.subCategories ?? [],
  );

  private readonly allSubs = computed<SubCategory[]>(
    () => this.payload()?.hubs.flatMap((h) => h.subCategories) ?? [],
  );

  readonly editingSub = computed<SubCategory | undefined>(() => {
    const id = this.editing();
    return id && id !== 'new' ? this.allSubs().find((s) => s.id === id) : undefined;
  });

  readonly piecesSub = computed<SubCategory | undefined>(() => {
    const id = this.piecesFor();
    return id ? this.allSubs().find((s) => s.id === id) : undefined;
  });

  /** Live pieces in each sub-category, for the counts in the list. */
  readonly liveCounts = computed<Map<string, number>>(() => {
    const live = this.store.products().filter((p) => resolveProductStatus(p) === 'live');
    return new Map(this.allSubs().map((s) => [s.id, live.filter((p) => isMember(p, s)).length]));
  });

  /** Pieces listed in the pieces panel, with why each is in or out. */
  readonly pieceRows = computed(() => {
    const sub = this.piecesSub();
    if (!sub) {
      return [];
    }
    const q = this.pieceSearch().trim().toLowerCase();
    const onlyIn = this.pieceFilter() === 'in';
    return this.store
      .products()
      .filter((p) => resolveProductStatus(p) !== 'write-off')
      .map((p) => ({ product: p, reason: membershipReason(p, sub), member: isMember(p, sub) }))
      .filter((row) => !onlyIn || row.member || row.reason === 'removed')
      .filter(
        (row) =>
          q === '' ||
          row.product.name.toLowerCase().includes(q) ||
          (row.product.sku ?? '').toLowerCase().includes(q),
      )
      .slice(0, 200);
  });

  ngOnInit(): void {
    this.reload();
  }

  private reload(): void {
    this.categories.getAdmin().subscribe({
      next: (payload) => {
        this.payload.set(payload);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Could not load the categories. Refresh the page to try again.');
      },
    });
  }

  /** Keeps the public pages this browser shows in step after a change. */
  private afterChange(payloadChanged = true): void {
    if (payloadChanged) {
      this.reload();
    }
    this.categories.refresh();
  }

  selectHub(key: HubKey): void {
    this.activeHub.set(key);
    this.closeForm();
    this.piecesFor.set(null);
  }

  liveCount(sub: SubCategory): number {
    return this.liveCounts().get(sub.id) ?? 0;
  }

  statusLabel(product: Product): string {
    return productStatusLabel(resolveProductStatus(product));
  }

  reasonLabel(reason: MembershipReason): string {
    switch (reason) {
      case 'keyword':
        return 'Name matches';
      case 'added':
        return 'Added by hand';
      case 'removed':
        return 'Removed by hand';
      default:
        return '';
    }
  }

  // ---- Add / edit ----

  startNew(): void {
    this.form = this.blankForm();
    this.slugFollowsName = true;
    this.formError.set(null);
    this.notice.set(null);
    this.piecesFor.set(null);
    this.editing.set('new');
  }

  startEdit(sub: SubCategory): void {
    this.form = {
      name: sub.name,
      slug: sub.slug,
      tagline: sub.tagline,
      intro: sub.intro,
      body: sub.body,
      metaTitle: sub.metaTitle,
      metaDescription: sub.metaDescription,
      isVisible: sub.isVisible,
      includeWords: sub.includeKeywords.join(', '),
      excludeWords: sub.excludeKeywords.join(', '),
    };
    this.slugFollowsName = false;
    this.formError.set(null);
    this.notice.set(null);
    this.piecesFor.set(null);
    this.editing.set(sub.id);
  }

  closeForm(): void {
    this.editing.set(null);
    this.formError.set(null);
  }

  onNameChange(name: string): void {
    this.form.name = name;
    if (this.slugFollowsName) {
      this.form.slug = slugify(name);
    }
  }

  onSlugChange(slug: string): void {
    this.form.slug = slug;
    this.slugFollowsName = false;
  }

  /** True when an existing page's slug is being changed — its old URL will redirect. */
  slugChanging(): boolean {
    const sub = this.editingSub();
    return !!sub && slugify(this.form.slug) !== sub.slug && this.form.slug.trim() !== '';
  }

  save(): void {
    const editing = this.editing();
    if (!editing || this.saving()) {
      return;
    }
    const body: SaveSubCategory = {
      hubKey: this.activeHub(),
      name: this.form.name.trim(),
      slug: this.form.slug.trim(),
      tagline: this.form.tagline,
      intro: this.form.intro,
      body: this.form.body,
      metaTitle: this.form.metaTitle,
      metaDescription: this.form.metaDescription,
      isVisible: this.form.isVisible,
      includeKeywords: splitWords(this.form.includeWords),
      excludeKeywords: splitWords(this.form.excludeWords),
    };
    this.saving.set(true);
    this.formError.set(null);
    const request$ = editing === 'new' ? this.categories.create(body) : this.categories.update(editing, body);
    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.editing.set(null);
        this.notice.set(
          editing === 'new'
            ? `"${saved.name}" is saved at ${subCategoryPath(saved)}.`
            : `"${saved.name}" is saved.`,
        );
        this.afterChange();
      },
      error: (err) => {
        this.saving.set(false);
        this.formError.set(err?.error?.error ?? 'Could not save. Please try again.');
      },
    });
  }

  remove(sub: SubCategory): void {
    const hubPath = `/${sub.hubKey}`;
    if (
      !confirm(
        `Delete "${sub.name}"?\n\nIts page (${subCategoryPath(sub)}) will redirect to ${hubPath}. ` +
          'The pieces in it are not affected. This cannot be undone.',
      )
    ) {
      return;
    }
    this.categories.delete(sub.id).subscribe({
      next: () => {
        this.notice.set(`"${sub.name}" was deleted. Its old address now redirects to ${hubPath}.`);
        if (this.editing() === sub.id) {
          this.closeForm();
        }
        if (this.piecesFor() === sub.id) {
          this.piecesFor.set(null);
        }
        this.afterChange();
      },
      error: () => this.notice.set('Could not delete it. Please try again.'),
    });
  }

  toggleVisible(sub: SubCategory): void {
    const body: SaveSubCategory = {
      hubKey: sub.hubKey,
      name: sub.name,
      slug: sub.slug,
      tagline: sub.tagline,
      intro: sub.intro,
      body: sub.body,
      metaTitle: sub.metaTitle,
      metaDescription: sub.metaDescription,
      isVisible: !sub.isVisible,
      includeKeywords: sub.includeKeywords,
      excludeKeywords: sub.excludeKeywords,
    };
    this.categories.update(sub.id, body).subscribe({
      next: () => this.afterChange(),
      error: () => this.notice.set('Could not change it. Please try again.'),
    });
  }

  move(index: number, delta: number): void {
    const list = [...this.subs()];
    const target = index + delta;
    if (target < 0 || target >= list.length) {
      return;
    }
    [list[index], list[target]] = [list[target], list[index]];
    const hub = this.activeHub();
    // Show the new order straight away; the reload afterwards confirms what was saved.
    this.payload.update((p) =>
      p ? { ...p, hubs: p.hubs.map((h) => (h.key === hub ? { ...h, subCategories: list } : h)) } : p,
    );
    this.categories.reorder(hub, list.map((s) => s.id)).subscribe({
      next: () => this.afterChange(),
      error: () => {
        this.notice.set('Could not save the new order. Please try again.');
        this.reload();
      },
    });
  }

  // ---- Pieces ----

  openPieces(sub: SubCategory): void {
    this.closeForm();
    this.notice.set(null);
    this.pieceSearch.set('');
    this.pieceFilter.set('in');
    this.selected.set(new Set());
    this.piecesFor.set(this.piecesFor() === sub.id ? null : sub.id);
  }

  isSelected(id: string): boolean {
    return this.selected().has(id);
  }

  toggleSelected(id: string): void {
    this.selected.update((set) => {
      const next = new Set(set);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  selectAllShown(): void {
    this.selected.set(new Set(this.pieceRows().map((r) => r.product.id)));
  }

  clearSelection(): void {
    this.selected.set(new Set());
  }

  addSelected(): void {
    this.applyPieces([...this.selected()], []);
  }

  removeSelected(): void {
    this.applyPieces([], [...this.selected()]);
  }

  addOne(productId: string): void {
    this.applyPieces([productId], []);
  }

  removeOne(productId: string): void {
    this.applyPieces([], [productId]);
  }

  private applyPieces(add: string[], remove: string[]): void {
    const sub = this.piecesSub();
    if (!sub || (add.length === 0 && remove.length === 0) || this.piecesBusy()) {
      return;
    }
    this.piecesBusy.set(true);
    this.categories.setProducts(sub.id, add, remove).subscribe({
      next: (updated) => {
        this.piecesBusy.set(false);
        this.selected.set(new Set());
        this.payload.update((p) =>
          p
            ? {
                ...p,
                hubs: p.hubs.map((h) => ({
                  ...h,
                  subCategories: h.subCategories.map((s) => (s.id === updated.id ? updated : s)),
                })),
              }
            : p,
        );
        this.afterChange(false);
      },
      error: () => {
        this.piecesBusy.set(false);
        this.notice.set('Could not update the pieces. Please try again.');
      },
    });
  }

  taglineLabel(): string {
    return this.activeHub() === 'designers'
      ? 'Origin and dates (the line under the heading, e.g. "Wales, UK — founded 1953")'
      : 'Short line under the heading';
  }

  private blankForm(): SubCategoryForm {
    return {
      name: '',
      slug: '',
      tagline: '',
      intro: '',
      body: '',
      metaTitle: '',
      metaDescription: '',
      isVisible: true,
      includeWords: '',
      excludeWords: '',
    };
  }
}

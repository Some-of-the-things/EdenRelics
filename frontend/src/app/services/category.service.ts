import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Product } from '../models/product.model';
import { DESIGNERS } from '../pages/designers/designers.data';
import { CATEGORY_HUBS } from '../pages/category/category.data';
import { isMember } from '../utils/category-membership';

/** The URL prefix of a category hub. A fourth hub would also need a frontend route. */
export type HubKey = 'designers' | 'style' | 'dresses';

export interface SubCategory {
  id: string;
  hubKey: HubKey;
  slug: string;
  name: string;
  /** Short line under the heading (the origin / era note on designer pages). */
  tagline: string;
  /** Lead paragraph. */
  intro: string;
  /** Extra paragraphs separated by blank lines; empty means "use the written sections in code". */
  body: string;
  metaTitle: string;
  metaDescription: string;
  displayOrder: number;
  isVisible: boolean;
  includeKeywords: string[];
  excludeKeywords: string[];
  /** The original slug of a migrated page, linking it to the written sections held in code. */
  contentKey: string | null;
  addedProductIds: string[];
  removedProductIds: string[];
}

export interface CategoryHubGroup {
  key: HubKey;
  name: string;
  subCategories: SubCategory[];
}

export interface CategoryRedirect {
  hubKey: HubKey;
  fromSlug: string;
  toPath: string;
}

export interface CategoriesPayload {
  hubs: CategoryHubGroup[];
  redirects: CategoryRedirect[];
  hidden: { hubKey: HubKey; slug: string }[];
}

export interface SaveSubCategory {
  hubKey: HubKey;
  name: string;
  slug: string;
  tagline: string;
  intro: string;
  body: string;
  metaTitle: string;
  metaDescription: string;
  isVisible: boolean;
  includeKeywords: string[];
  excludeKeywords: string[];
}

/** Where an unknown sub-category URL should go instead of a 404. */
export type MissingSubCategory =
  | { kind: 'moved'; to: string } // renamed or deleted: permanent (301)
  | { kind: 'hidden'; to: string } // temporarily hidden: back to the hub (302)
  | null;

export const HUB_NAMES: Record<HubKey, string> = {
  designers: 'Vintage Designers & Labels',
  style: 'Shop by Style',
  dresses: 'Dress Types',
};

/**
 * The launch sub-categories, built from the data files they used to be hard-coded in. Shown until
 * the API answers and kept if it can't be reached, so no category page ever disappears because of
 * a failed request. The database was seeded from exactly this list.
 */
function builtInPayload(): CategoriesPayload {
  const base = { body: '', isVisible: true, addedProductIds: [], removedProductIds: [] };
  const designers: SubCategory[] = DESIGNERS.map((d, i) => ({
    ...base,
    id: `builtin-designers-${d.slug}`,
    hubKey: 'designers',
    slug: d.slug,
    name: d.name,
    tagline: d.origin,
    intro: d.intro,
    metaTitle: d.metaTitle,
    metaDescription: d.metaDescription,
    displayOrder: i,
    includeKeywords: d.productMatchers,
    excludeKeywords: [],
    contentKey: d.slug,
  }));
  const fromHubs = (kind: 'style' | 'garment', hubKey: HubKey): SubCategory[] =>
    CATEGORY_HUBS.filter((h) => h.kind === kind).map((h, i) => ({
      ...base,
      id: `builtin-${hubKey}-${h.slug}`,
      hubKey,
      slug: h.slug,
      name: h.name,
      tagline: h.tagline,
      intro: h.intro,
      metaTitle: h.metaTitle,
      metaDescription: h.metaDescription,
      displayOrder: i,
      includeKeywords: h.include,
      excludeKeywords: h.exclude ?? [],
      contentKey: h.slug,
    }));
  return {
    hubs: [
      { key: 'designers', name: HUB_NAMES.designers, subCategories: designers },
      { key: 'style', name: HUB_NAMES.style, subCategories: fromHubs('style', 'style') },
      { key: 'dresses', name: HUB_NAMES.dresses, subCategories: fromHubs('garment', 'dresses') },
    ],
    redirects: [],
    hidden: [],
  };
}

export const BUILT_IN_CATEGORIES: CategoriesPayload = builtInPayload();

export function subCategoryPath(sub: { hubKey: string; slug: string }): string {
  return `/${sub.hubKey}/${sub.slug}`;
}

/**
 * Category hubs and their sub-categories, managed from the admin Categories tab. Pages, counts and
 * product-page links all read from here, and membership follows the shared keyword-plus-ticks rule
 * in utils/category-membership.ts.
 */
@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/api/categories`;

  private readonly _payload = signal<CategoriesPayload>(BUILT_IN_CATEGORIES);
  private readonly _loaded = signal(false);
  private requested = false;

  /** True once the API has answered (or failed, leaving the built-in list in place). */
  readonly loaded = this._loaded.asReadonly();
  readonly hubs = computed(() => this._payload().hubs);

  /** Fetches the live list once per app instance (per request on the server). */
  load(): void {
    if (this.requested) {
      return;
    }
    this.requested = true;
    this.http.get<CategoriesPayload>(this.url).subscribe({
      next: (payload) => {
        this._payload.set(payload);
        this._loaded.set(true);
      },
      error: () => this._loaded.set(true),
    });
  }

  subCategoriesOf(hubKey: HubKey): SubCategory[] {
    return this.hubs().find((h) => h.key === hubKey)?.subCategories ?? [];
  }

  find(hubKey: HubKey, slug: string): SubCategory | undefined {
    return this.subCategoriesOf(hubKey).find((s) => s.slug === slug);
  }

  /** For a slug with no visible page: where it should go (renamed/deleted → 301, hidden → 302). */
  resolveMissing(hubKey: HubKey, slug: string): MissingSubCategory {
    const payload = this._payload();
    const redirect = payload.redirects.find((r) => r.hubKey === hubKey && r.fromSlug === slug);
    if (redirect) {
      return { kind: 'moved', to: redirect.toPath };
    }
    if (payload.hidden.some((h) => h.hubKey === hubKey && h.slug === slug)) {
      return { kind: 'hidden', to: `/${hubKey}` };
    }
    return null;
  }

  productsIn(sub: SubCategory, products: readonly Product[]): Product[] {
    return products.filter((p) => isMember(p, sub));
  }

  /** Every visible sub-category a piece belongs to, optionally limited to one hub. */
  subCategoriesFor(product: { id: string; name: string }, hubKey?: HubKey): SubCategory[] {
    return this.hubs()
      .filter((h) => !hubKey || h.key === hubKey)
      .flatMap((h) => h.subCategories)
      .filter((s) => isMember(product, s));
  }

  // ---- Admin ----

  getAdmin(): Observable<CategoriesPayload> {
    return this.http.get<CategoriesPayload>(`${this.url}/admin`);
  }

  create(body: SaveSubCategory): Observable<SubCategory> {
    return this.http.post<SubCategory>(`${this.url}/admin/sub-categories`, body);
  }

  update(id: string, body: SaveSubCategory): Observable<SubCategory> {
    return this.http.put<SubCategory>(`${this.url}/admin/sub-categories/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/admin/sub-categories/${id}`);
  }

  reorder(hubKey: HubKey, ids: string[]): Observable<void> {
    return this.http.put<void>(`${this.url}/admin/hubs/${hubKey}/order`, { ids });
  }

  setProducts(id: string, add: string[], remove: string[]): Observable<SubCategory> {
    return this.http.put<SubCategory>(`${this.url}/admin/sub-categories/${id}/products`, { add, remove });
  }

  setProductCategories(productId: string, subCategoryIds: string[]): Observable<void> {
    return this.http.put<void>(`${this.url}/admin/products/${productId}`, { subCategoryIds });
  }

  /** After an admin save, refresh the public list this browser session shows. */
  refresh(): void {
    this.requested = false;
    this.load();
  }
}

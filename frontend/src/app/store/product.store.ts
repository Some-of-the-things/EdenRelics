import { computed, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap } from 'rxjs';
import { Product } from '../models/product.model';
import { ProductService } from '../services/product.service';
import { resolveProductStatus } from '../utils/product-status';

interface ProductState {
  products: Product[];
  selectedCategory: Product['category'] | 'all';
  selectedSize: Product['size'] | 'all';
  searchQuery: string;
  currentPage: number;
  pageSize: number;
  isLoading: boolean;
  error: string;
}

const initialState: ProductState = {
  products: [],
  selectedCategory: 'all',
  selectedSize: 'all',
  searchQuery: '',
  currentPage: 1,
  pageSize: 12,
  isLoading: false,
  error: '',
};

export const ProductStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed((store) => ({
    /**
     * Customer-visible products: status === 'live' only. Use this for any
     * public-facing list / count. Admin clients receive the full catalogue
     * from the API, so without this filter sold and draft items would leak
     * into the home page list when an admin is logged in. The raw
     * `products()` signal is still available for admin-context consumers
     * (admin panel, product detail page for direct/favourite access).
     */
    liveProducts: computed(() =>
      store.products().filter((p) => resolveProductStatus(p) === 'live'),
    ),
    filteredProducts: computed(() => {
      let products = store.products().filter((p) => resolveProductStatus(p) === 'live');
      const category = store.selectedCategory();
      const size = store.selectedSize();
      const query = store.searchQuery().toLowerCase();

      if (category !== 'all') {
        products = products.filter((p) => p.category === category);
      }
      if (size !== 'all') {
        products = products.filter((p) => p.size === size);
      }
      if (query) {
        products = products.filter(
          (p) =>
            p.name.toLowerCase().includes(query) ||
            p.era.toLowerCase().includes(query) ||
            p.description.toLowerCase().includes(query)
        );
      }
      return products;
    }),
    categories: computed(() => {
      const cats: Product['category'][] = [
        '50s',
        '60s',
        '70s',
        '80s',
        '90s',
        'y2k',
      ];
      return cats;
    }),
    sizes: computed(() => {
      const sizes: Product['size'][] = [
        '6',
        '6/8',
        '8',
        '8/10',
        '10',
        '10/12',
        '12',
        '12/14',
        '14',
        '16',
      ];
      return sizes;
    }),
  })),
  withComputed((store) => ({
    totalPages: computed(() =>
      Math.max(1, Math.ceil(store.filteredProducts().length / store.pageSize())),
    ),
    pagedProducts: computed(() => {
      const filtered = store.filteredProducts();
      const size = store.pageSize();
      const total = Math.max(1, Math.ceil(filtered.length / size));
      const page = Math.min(Math.max(1, store.currentPage()), total);
      const start = (page - 1) * size;
      return filtered.slice(start, start + size);
    }),
  })),
  withMethods((store, productService = inject(ProductService)) => ({
    loadProducts: rxMethod<void>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(() =>
          productService.getAll().pipe(
            tap((products) => patchState(store, { products, isLoading: false }))
          )
        )
      )
    ),
    setCategory(category: Product['category'] | 'all'): void {
      patchState(store, { selectedCategory: category, currentPage: 1 });
    },
    setSize(size: Product['size'] | 'all'): void {
      patchState(store, { selectedSize: size, currentPage: 1 });
    },
    setSearchQuery(query: string): void {
      patchState(store, { searchQuery: query, currentPage: 1 });
    },
    setPage(page: number): void {
      patchState(store, { currentPage: Math.max(1, Math.floor(page)) });
    },
    addProduct(product: Omit<Product, 'id'>): void {
      patchState(store, { error: '' });
      productService.add(product).subscribe({
        next: (created) => {
          patchState(store, { products: [...store.products(), created] });
        },
        error: (err) => {
          const msg = err.status === 403
            ? 'You do not have permission to add products.'
            : err.error?.message ?? 'Failed to add product.';
          patchState(store, { error: msg });
        },
      });
    },
    updateProduct(id: string, changes: Partial<Omit<Product, 'id'>>): void {
      patchState(store, { error: '' });
      productService.update(id, changes).subscribe({
        next: (updated) => {
          patchState(store, {
            products: store.products().map((p) => (p.id === id ? updated : p)),
          });
        },
        error: (err) => {
          const msg = err.status === 403
            ? 'You do not have permission to edit products.'
            : err.error?.message ?? 'Failed to update product.';
          patchState(store, { error: msg });
        },
      });
    },
    removeProduct(id: string): void {
      patchState(store, { error: '' });
      productService.remove(id).subscribe({
        next: () => {
          patchState(store, {
            products: store.products().filter((p) => p.id !== id),
          });
        },
        error: (err) => {
          const msg = err.status === 403
            ? 'You do not have permission to delete products.'
            : err.error?.message ?? 'Failed to delete product.';
          patchState(store, { error: msg });
        },
      });
    },
  })),
  withHooks({
    onInit(store) {
      store.loadProducts();
    },
  })
);

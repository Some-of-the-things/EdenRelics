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

interface ProductState {
  products: Product[];
  selectedCategory: Product['category'] | 'all';
  searchQuery: string;
  isLoading: boolean;
  error: string;
}

const initialState: ProductState = {
  products: [],
  selectedCategory: 'all',
  searchQuery: '',
  isLoading: false,
  error: '',
};

export const ProductStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed((store) => ({
    filteredProducts: computed(() => {
      let products = store.products();
      const category = store.selectedCategory();
      const query = store.searchQuery().toLowerCase();

      if (category !== 'all') {
        products = products.filter((p) => p.category === category);
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
        'modern',
      ];
      return cats;
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
      patchState(store, { selectedCategory: category });
    },
    setSearchQuery(query: string): void {
      patchState(store, { searchQuery: query });
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

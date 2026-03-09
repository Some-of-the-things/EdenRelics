import { computed, inject } from '@angular/core';
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
}

const initialState: ProductState = {
  products: [],
  selectedCategory: 'all',
  searchQuery: '',
  isLoading: false,
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
      productService.add(product).subscribe((created) => {
        patchState(store, { products: [...store.products(), created] });
      });
    },
    updateProduct(id: string, changes: Partial<Omit<Product, 'id'>>): void {
      productService.update(id, changes).subscribe((updated) => {
        patchState(store, {
          products: store.products().map((p) => (p.id === id ? updated : p)),
        });
      });
    },
    removeProduct(id: string): void {
      productService.remove(id).subscribe(() => {
        patchState(store, {
          products: store.products().filter((p) => p.id !== id),
        });
      });
    },
  })),
  withHooks({
    onInit(store) {
      store.loadProducts();
    },
  })
);

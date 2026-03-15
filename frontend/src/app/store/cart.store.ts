import { computed } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withMethods,
  withState,
} from '@ngrx/signals';
import { CartItem, Product } from '../models/product.model';

interface CartState {
  items: CartItem[];
}

const initialState: CartState = {
  items: [],
};

export const CartStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed((store) => ({
    totalItems: computed(() => store.items().length),
    totalPrice: computed(() =>
      store.items().reduce((sum, item) => sum + item.product.price, 0)
    ),
    isEmpty: computed(() => store.items().length === 0),
  })),
  withMethods((store) => ({
    addToCart(product: Product): void {
      const items = store.items();
      const existing = items.find((i) => i.product.id === product.id);

      if (!existing) {
        patchState(store, { items: [...items, { product, quantity: 1 }] });
      }
    },
    removeFromCart(productId: string): void {
      patchState(store, {
        items: store.items().filter((i) => i.product.id !== productId),
      });
    },
    clearCart(): void {
      patchState(store, { items: [] });
    },
  }))
);

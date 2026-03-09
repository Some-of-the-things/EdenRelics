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
    totalItems: computed(() =>
      store.items().reduce((sum, item) => sum + item.quantity, 0)
    ),
    totalPrice: computed(() =>
      store.items().reduce(
        (sum, item) => sum + item.product.price * item.quantity,
        0
      )
    ),
    isEmpty: computed(() => store.items().length === 0),
  })),
  withMethods((store) => ({
    addToCart(product: Product): void {
      const items = store.items();
      const existing = items.find((i) => i.product.id === product.id);

      if (existing) {
        patchState(store, {
          items: items.map((i) =>
            i.product.id === product.id
              ? { ...i, quantity: i.quantity + 1 }
              : i
          ),
        });
      } else {
        patchState(store, { items: [...items, { product, quantity: 1 }] });
      }
    },
    removeFromCart(productId: string): void {
      patchState(store, {
        items: store.items().filter((i) => i.product.id !== productId),
      });
    },
    updateQuantity(productId: string, quantity: number): void {
      if (quantity <= 0) {
        this.removeFromCart(productId);
        return;
      }
      patchState(store, {
        items: store.items().map((i) =>
          i.product.id === productId ? { ...i, quantity } : i
        ),
      });
    },
    clearCart(): void {
      patchState(store, { items: [] });
    },
  }))
);

import { TestBed } from '@angular/core/testing';
import { CartStore } from './cart.store';
import { Product } from '../models/product.model';

describe('CartStore', () => {
  let store: InstanceType<typeof CartStore>;

  const mockProduct: Product = {
    id: '1',
    name: 'Test Dress',
    description: 'A test dress',
    price: 100,
    era: '1960s',
    category: '70s',
    size: 'S',
    condition: 'excellent',
    imageUrl: 'test.jpg',
    inStock: true,
  };

  const mockProduct2: Product = {
    ...mockProduct,
    id: '2',
    name: 'Test Jacket',
    price: 200,
    category: '90s',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({});
    store = TestBed.inject(CartStore);
  });

  it('should start with an empty cart', () => {
    expect(store.items().length).toBe(0);
    expect(store.isEmpty()).toBe(true);
    expect(store.totalItems()).toBe(0);
    expect(store.totalPrice()).toBe(0);
  });

  it('should add a product to the cart', () => {
    store.addToCart(mockProduct);
    expect(store.items().length).toBe(1);
    expect(store.items()[0].quantity).toBe(1);
    expect(store.isEmpty()).toBe(false);
  });

  it('should increment quantity when adding the same product', () => {
    store.addToCart(mockProduct);
    store.addToCart(mockProduct);
    expect(store.items().length).toBe(1);
    expect(store.items()[0].quantity).toBe(2);
  });

  it('should calculate total items correctly', () => {
    store.addToCart(mockProduct);
    store.addToCart(mockProduct);
    store.addToCart(mockProduct2);
    expect(store.totalItems()).toBe(3);
  });

  it('should calculate total price correctly', () => {
    store.addToCart(mockProduct); // 100
    store.addToCart(mockProduct); // 100
    store.addToCart(mockProduct2); // 200
    expect(store.totalPrice()).toBe(400);
  });

  it('should remove a product from the cart', () => {
    store.addToCart(mockProduct);
    store.addToCart(mockProduct2);
    store.removeFromCart('1');
    expect(store.items().length).toBe(1);
    expect(store.items()[0].product.id).toBe('2');
  });

  it('should update quantity', () => {
    store.addToCart(mockProduct);
    store.updateQuantity('1', 5);
    expect(store.items()[0].quantity).toBe(5);
  });

  it('should remove item when quantity is set to 0', () => {
    store.addToCart(mockProduct);
    store.updateQuantity('1', 0);
    expect(store.items().length).toBe(0);
  });

  it('should clear the cart', () => {
    store.addToCart(mockProduct);
    store.addToCart(mockProduct2);
    store.clearCart();
    expect(store.items().length).toBe(0);
    expect(store.isEmpty()).toBe(true);
  });
});

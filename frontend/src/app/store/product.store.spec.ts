import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ProductStore } from './product.store';
import { Product } from '../models/product.model';
import { environment } from '../../environments/environment';

const MOCK_PRODUCTS: Product[] = [
  {
    id: '1',
    name: 'Bohemian Maxi Dress',
    description: 'Flowing 1970s bohemian maxi dress',
    price: 195,
    era: '1970s',
    category: '70s',
    size: '10',
    condition: 'good',
    imageUrl: 'test.jpg',
    inStock: true,
  },
  {
    id: '2',
    name: 'Silk Slip Dress',
    description: 'Minimalist 90s silk slip dress',
    price: 225,
    era: '1990s',
    category: '90s',
    size: '12',
    condition: 'excellent',
    imageUrl: 'test2.jpg',
    inStock: true,
  },
  {
    id: '3',
    name: 'Power Blazer',
    description: 'Bold 80s oversized blazer',
    price: 180,
    era: '1980s',
    category: '80s',
    size: '14',
    condition: 'good',
    imageUrl: 'test3.jpg',
    inStock: true,
  },
];

describe('ProductStore', () => {
  let store: InstanceType<typeof ProductStore>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // Suppress LocaleService's /api/locale/detect call (browser-only init)
        { provide: PLATFORM_ID, useValue: 'server' },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    store = TestBed.inject(ProductStore);

    // Flush the initial loadProducts call
    httpMock.expectOne(`${environment.apiUrl}/api/products`).flush(MOCK_PRODUCTS);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should load products on init', () => {
    expect(store.products().length).toBe(3);
  });

  it('should show all products when no filter is set', () => {
    expect(store.filteredProducts().length).toBe(store.products().length);
  });

  it('should filter products by category', () => {
    store.setCategory('70s');
    const filtered = store.filteredProducts();
    expect(filtered.length).toBeGreaterThan(0);
    expect(filtered.every((p) => p.category === '70s')).toBe(true);
  });

  it('should filter products by search query', () => {
    store.setSearchQuery('bohemian');
    const filtered = store.filteredProducts();
    expect(filtered.length).toBe(1);
    expect(filtered[0].name).toBe('Bohemian Maxi Dress');
  });

  it('should filter by both category and search query', () => {
    store.setCategory('90s');
    store.setSearchQuery('slip');
    const filtered = store.filteredProducts();
    expect(filtered.length).toBe(1);
    expect(filtered[0].name).toContain('Slip');
  });

  it('should return empty when no match', () => {
    store.setSearchQuery('nonexistent-item-xyz');
    expect(store.filteredProducts().length).toBe(0);
  });

  it('should reset to all when category set back to all', () => {
    store.setCategory('70s');
    expect(store.filteredProducts().length).toBeLessThan(store.products().length);
    store.setCategory('all');
    expect(store.filteredProducts().length).toBe(store.products().length);
  });

  it('should provide categories list', () => {
    const cats = store.categories();
    expect(cats).toContain('50s');
    expect(cats).toContain('60s');
    expect(cats).toContain('70s');
    expect(cats).toContain('80s');
    expect(cats).toContain('90s');
    expect(cats).toContain('y2k');
    expect(cats).toContain('modern');
    expect(cats.length).toBe(7);
  });
});

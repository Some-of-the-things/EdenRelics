import { TestBed } from '@angular/core/testing';
import { ProductStore } from './product.store';

describe('ProductStore', () => {
  let store: InstanceType<typeof ProductStore>;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    store = TestBed.inject(ProductStore);
  });

  it('should load products on init', () => {
    expect(store.products().length).toBeGreaterThan(0);
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
    expect(cats).toContain('70s');
    expect(cats).toContain('80s');
    expect(cats).toContain('90s');
    expect(cats).toContain('y2k');
    expect(cats).toContain('modern');
    expect(cats.length).toBe(5);
  });
});

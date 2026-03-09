import { TestBed } from '@angular/core/testing';
import { ProductService } from './product.service';

describe('ProductService', () => {
  let service: ProductService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ProductService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should return all products', () => {
    const products = service.getAll();
    expect(products.length).toBeGreaterThan(0);
  });

  it('should return a product by id', () => {
    const product = service.getById('1');
    expect(product).toBeTruthy();
    expect(product!.id).toBe('1');
  });

  it('should return undefined for unknown id', () => {
    const product = service.getById('999');
    expect(product).toBeUndefined();
  });

  it('should return products by category', () => {
    const dresses = service.getByCategory('dresses');
    expect(dresses.length).toBeGreaterThan(0);
    expect(dresses.every((p) => p.category === 'dresses')).toBe(true);
  });
});

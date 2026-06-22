import { Product } from '../models/product.model';
import { filterAdminProducts, productStatusLabel, resolveProductStatus } from './product-status';

function makeProduct(overrides: Partial<Product>): Product {
  return {
    id: overrides.id ?? 'id-1',
    name: overrides.name ?? 'Test Product',
    description: 'desc',
    price: 100,
    era: '1990s',
    category: '90s',
    size: '10',
    condition: 'good',
    imageUrl: 'img.jpg',
    inStock: true,
    ...overrides,
  } as Product;
}

describe('resolveProductStatus', () => {
  it('returns the explicit status when set', () => {
    expect(resolveProductStatus(makeProduct({ status: 'stock' }))).toBe('stock');
    expect(resolveProductStatus(makeProduct({ status: 'live' }))).toBe('live');
    expect(resolveProductStatus(makeProduct({ status: 'sold' }))).toBe('sold');
  });

  it('falls back to inStock=true -> live when status is undefined', () => {
    expect(resolveProductStatus(makeProduct({ status: undefined, inStock: true }))).toBe('live');
  });

  it('falls back to inStock=false -> sold when status is undefined', () => {
    expect(resolveProductStatus(makeProduct({ status: undefined, inStock: false }))).toBe('sold');
  });
});

describe('productStatusLabel', () => {
  it('maps each status to its label', () => {
    expect(productStatusLabel('stock')).toBe('Stock');
    expect(productStatusLabel('live')).toBe('Live');
    expect(productStatusLabel('sold')).toBe('Sold');
  });
});

describe('filterAdminProducts', () => {
  const products: Product[] = [
    makeProduct({ id: '1', name: 'Bohemian Maxi Dress', sku: 'ER-00001', status: 'live', era: '1970s' }),
    makeProduct({ id: '2', name: 'Silk Slip Dress', sku: 'ER-00002', status: 'stock', era: '1990s' }),
    makeProduct({ id: '3', name: 'Power Blazer', sku: 'ER-00003', status: 'sold', era: '1980s' }),
    makeProduct({ id: '4', name: 'Mystery Item', sku: 'CUSTOM-9', status: 'live', era: '1970s' }),
  ];

  it('returns everything with status=all and empty search', () => {
    expect(filterAdminProducts(products, '', 'all').length).toBe(4);
  });

  it('filters by status only', () => {
    expect(filterAdminProducts(products, '', 'live').map((p) => p.id)).toEqual(['1', '4']);
    expect(filterAdminProducts(products, '', 'stock').map((p) => p.id)).toEqual(['2']);
    expect(filterAdminProducts(products, '', 'sold').map((p) => p.id)).toEqual(['3']);
  });

  it('filters by name (case-insensitive)', () => {
    expect(filterAdminProducts(products, 'bohemian', 'all').map((p) => p.id)).toEqual(['1']);
    expect(filterAdminProducts(products, 'BOHEMIAN', 'all').map((p) => p.id)).toEqual(['1']);
  });

  it('filters by SKU (full or partial)', () => {
    expect(filterAdminProducts(products, 'ER-00002', 'all').map((p) => p.id)).toEqual(['2']);
    expect(filterAdminProducts(products, 'er-0000', 'all').length).toBe(3);
    expect(filterAdminProducts(products, 'custom', 'all').map((p) => p.id)).toEqual(['4']);
  });

  it('filters by era', () => {
    expect(filterAdminProducts(products, '1970', 'all').map((p) => p.id)).toEqual(['1', '4']);
  });

  it('filters by supplier (case-insensitive, partial)', () => {
    const withSuppliers = [
      makeProduct({ id: 's1', name: 'A', supplier: 'Norwich Vintage Co' }),
      makeProduct({ id: 's2', name: 'B', supplier: 'Camden Market Stall' }),
      makeProduct({ id: 's3', name: 'C' }),
    ];
    expect(filterAdminProducts(withSuppliers, 'norwich', 'all').map((p) => p.id)).toEqual(['s1']);
    expect(filterAdminProducts(withSuppliers, 'MARKET', 'all').map((p) => p.id)).toEqual(['s2']);
  });

  it('combines status and search', () => {
    expect(filterAdminProducts(products, 'dress', 'live').map((p) => p.id)).toEqual(['1']);
    expect(filterAdminProducts(products, 'dress', 'stock').map((p) => p.id)).toEqual(['2']);
  });

  it('returns empty when nothing matches', () => {
    expect(filterAdminProducts(products, 'no-such-thing', 'all').length).toBe(0);
  });

  it('ignores legacy products without explicit status', () => {
    const legacy: Product[] = [
      makeProduct({ id: 'l1', name: 'Legacy Live', status: undefined, inStock: true }),
      makeProduct({ id: 'l2', name: 'Legacy Sold', status: undefined, inStock: false }),
    ];
    expect(filterAdminProducts(legacy, '', 'live').map((p) => p.id)).toEqual(['l1']);
    expect(filterAdminProducts(legacy, '', 'sold').map((p) => p.id)).toEqual(['l2']);
    expect(filterAdminProducts(legacy, '', 'stock').length).toBe(0);
  });
});

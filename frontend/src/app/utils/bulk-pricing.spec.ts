import { Product } from '../models/product.model';
import { bulkSaleTotals, discountedPrice, normaliseDiscountPercent } from './bulk-pricing';

function makeProduct(price: number, id = 'id-1'): Product {
  return {
    id,
    name: `Product ${id}`,
    description: 'desc',
    price,
    era: '1990s',
    category: '90s',
    size: '10',
    condition: 'good',
    imageUrl: 'img.jpg',
    inStock: true,
  } as Product;
}

describe('normaliseDiscountPercent', () => {
  it('accepts percentages inside 1-90', () => {
    expect(normaliseDiscountPercent(1)).toBe(1);
    expect(normaliseDiscountPercent(20)).toBe(20);
    expect(normaliseDiscountPercent(90)).toBe(90);
  });

  it('rejects zero, negatives and anything over 90', () => {
    expect(normaliseDiscountPercent(0)).toBeNull();
    expect(normaliseDiscountPercent(-5)).toBeNull();
    expect(normaliseDiscountPercent(91)).toBeNull();
  });

  it('rejects empty and non-numeric input from the number field', () => {
    expect(normaliseDiscountPercent(null)).toBeNull();
    expect(normaliseDiscountPercent(undefined)).toBeNull();
    expect(normaliseDiscountPercent(Number.NaN)).toBeNull();
  });
});

describe('discountedPrice', () => {
  it('rounds to the nearest penny, not to a round pound', () => {
    expect(discountedPrice(145, 20)).toBe(116);
    expect(discountedPrice(137, 15)).toBe(116.45);
    expect(discountedPrice(68, 30)).toBe(47.6);
  });

  it('never produces floating-point noise', () => {
    expect(discountedPrice(29.99, 10)).toBe(26.99);
    expect(discountedPrice(19.99, 33)).toBe(13.39);
  });

  it('always works off the full price, so applying twice is idempotent', () => {
    const once = discountedPrice(100, 30);
    expect(discountedPrice(100, 30)).toBe(once);
    expect(once).toBe(70);
  });
});

describe('bulkSaleTotals', () => {
  it('totals the full and discounted prices across the selection', () => {
    const products = [makeProduct(145, 'a'), makeProduct(137, 'b'), makeProduct(68, 'c')];
    expect(bulkSaleTotals(products, 20)).toEqual({ before: 350, after: 280, saving: 70 });
  });

  it('returns zeroes for an empty selection', () => {
    expect(bulkSaleTotals([], 20)).toEqual({ before: 0, after: 0, saving: 0 });
  });
});

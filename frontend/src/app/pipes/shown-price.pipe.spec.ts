import { shownPrice } from './shown-price.pipe';

describe('shownPrice', () => {
  it('shows a sold piece at its original price, even if it was reduced', () => {
    expect(shownPrice({ price: 80, salePrice: 60, status: 'sold', inStock: false })).toBe(80);
    expect(shownPrice({ price: 80, salePrice: 60, inStock: false })).toBe(80);
  });

  it('shows a live piece at its sale price when it has one', () => {
    expect(shownPrice({ price: 80, salePrice: 60, status: 'live', inStock: true })).toBe(60);
    expect(shownPrice({ price: 80, salePrice: null, status: 'live', inStock: true })).toBe(80);
  });
});

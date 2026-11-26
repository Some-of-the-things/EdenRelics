import { Pipe, PipeTransform } from '@angular/core';
import { Product } from '../models/product.model';
import { resolveProductStatus } from '../utils/product-status';

/**
 * The price a product card shows: a sold piece shows what it was listed at, not the reduced price
 * it went for; anything else shows its sale price when it has one.
 */
export function shownPrice(product: Pick<Product, 'price' | 'salePrice' | 'status' | 'inStock'>): number {
  if (resolveProductStatus(product) === 'sold') {
    return product.price;
  }
  return product.salePrice ?? product.price;
}

@Pipe({ name: 'shownPrice' })
export class ShownPricePipe implements PipeTransform {
  transform(product: Pick<Product, 'price' | 'salePrice' | 'status' | 'inStock'>): number {
    return shownPrice(product);
  }
}

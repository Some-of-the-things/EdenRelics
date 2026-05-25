import { Product, ProductStatus } from '../models/product.model';

export function resolveProductStatus(product: Pick<Product, 'status' | 'inStock'>): ProductStatus {
  return product.status ?? (product.inStock ? 'live' : 'sold');
}

export function productStatusLabel(status: ProductStatus): string {
  switch (status) {
    case 'stock': return 'Stock';
    case 'live': return 'Live';
    case 'sold': return 'Sold';
  }
}

export function filterAdminProducts(
  products: Product[],
  search: string,
  statusFilter: 'all' | ProductStatus,
): Product[] {
  const term = search.trim().toLowerCase();
  return products.filter((p) => {
    const status = resolveProductStatus(p);
    if (statusFilter !== 'all' && status !== statusFilter) {
      return false;
    }
    if (!term) {
      return true;
    }
    const haystacks = [p.name, p.sku, p.era, p.size]
      .filter((v): v is string => !!v)
      .map((v) => v.toLowerCase());
    return haystacks.some((h) => h.includes(term));
  });
}

export type ProductSort =
  | 'newest'
  | 'oldest'
  | 'name'
  | 'era'
  | 'size'
  | 'price-low'
  | 'price-high'
  | 'cost-low'
  | 'cost-high'
  | 'views'
  | 'stock-date-old'
  | 'stock-date-new';

/** Extract leading digits, e.g. "1970s" → 1970, "10/12" → 10. Returns Infinity for unparseable so they sort last. */
function leadingNumber(value: string | undefined | null): number {
  if (!value) { return Infinity; }
  const match = /^\d+/.exec(value);
  return match ? Number(match[0]) : Infinity;
}

export function sortAdminProducts(products: Product[], sort: ProductSort): Product[] {
  const copy = [...products];
  switch (sort) {
    case 'newest':
      return copy.sort((a, b) => (b.createdAtUtc ?? '').localeCompare(a.createdAtUtc ?? ''));
    case 'oldest':
      return copy.sort((a, b) => (a.createdAtUtc ?? '').localeCompare(b.createdAtUtc ?? ''));
    case 'name':
      return copy.sort((a, b) => a.name.localeCompare(b.name));
    case 'era':
      return copy.sort((a, b) => leadingNumber(a.era) - leadingNumber(b.era));
    case 'size':
      return copy.sort((a, b) => leadingNumber(a.size) - leadingNumber(b.size));
    case 'price-low':
      return copy.sort((a, b) => a.price - b.price);
    case 'price-high':
      return copy.sort((a, b) => b.price - a.price);
    case 'cost-low':
      return copy.sort((a, b) => (a.costPrice ?? Infinity) - (b.costPrice ?? Infinity));
    case 'cost-high':
      return copy.sort((a, b) => (b.costPrice ?? -Infinity) - (a.costPrice ?? -Infinity));
    case 'views':
      return copy.sort((a, b) => (b.viewCount ?? 0) - (a.viewCount ?? 0));
    case 'stock-date-old':
      return copy.sort((a, b) => (a.stockPurchaseDate ?? '￿').localeCompare(b.stockPurchaseDate ?? '￿'));
    case 'stock-date-new':
      return copy.sort((a, b) => (b.stockPurchaseDate ?? '').localeCompare(a.stockPurchaseDate ?? ''));
  }
}

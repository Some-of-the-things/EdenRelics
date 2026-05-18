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

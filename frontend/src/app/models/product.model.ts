export type ProductStatus = 'stock' | 'live' | 'sold';

/**
 * Every size we sell, smallest first — the single source of truth.
 *
 * Slash form for the in-between sizes, matching how the labels read and how
 * every existing product is stored. The order here is the order the shop's size
 * filter renders in, so keep it ascending.
 *
 * This is a list rather than a bare union because the union alone only stops
 * WRONG values, not MISSING ones: the shop filter kept its own hand-written copy
 * and silently omitted 14/16, so a dress listed at that size in admin could
 * never be found through the filter. Derive from this, don't restate it.
 */
export const PRODUCT_SIZES = [
  '6', '6/8', '8', '8/10', '10', '10/12', '12', '12/14', '14',
  '14/16', '16', '16/18', '18', '18/20',
] as const;

export type ProductSize = (typeof PRODUCT_SIZES)[number];

export interface Product {
  id: string;
  name: string;
  slug?: string;
  sku?: string;
  description: string;
  price: number;
  salePrice?: number | null;
  showReduction?: boolean;
  discountPercent?: number;
  costPrice?: number;
  stockPurchaseDate?: string | null;
  supplier?: string;
  era: string;
  category: '50s' | '60s' | '70s' | '80s' | '90s' | 'y2k';
  size: ProductSize;
  condition: 'mint' | 'excellent' | 'very good' | 'good' | 'fair';
  material?: string | null;
  imageUrl: string;
  additionalImageUrls?: string[];
  videoUrls?: string[];
  inStock: boolean;
  status?: ProductStatus;
  viewCount?: number;
  createdAtUtc?: string;
}

export interface CartItem {
  product: Product;
  quantity: number;
}

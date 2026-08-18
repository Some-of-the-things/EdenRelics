import { Product } from '../models/product.model';

/** The percentage range the bulk sale tool accepts — mirrors the backend's guard. */
export const MIN_BULK_DISCOUNT_PERCENT = 1;
export const MAX_BULK_DISCOUNT_PERCENT = 90;

/**
 * A discount percentage that's safe to send, or null if it isn't usable. Keeps the admin UI
 * and the API agreeing on what counts as a valid bulk discount.
 */
export function normaliseDiscountPercent(value: number | null | undefined): number | null {
  const percent = Number(value);
  if (
    !Number.isFinite(percent) ||
    percent < MIN_BULK_DISCOUNT_PERCENT ||
    percent > MAX_BULK_DISCOUNT_PERCENT
  ) {
    return null;
  }
  return percent;
}

/**
 * The sale price for a percentage off, rounded to the nearest penny. Mirrors the backend's
 * arithmetic so the preview shown before applying matches what actually gets saved. Always
 * derived from the full price, never from an existing sale price, so re-running a sale on the
 * same products doesn't compound the discount.
 */
export function discountedPrice(price: number, discountPercent: number): number {
  return Math.round(((price * (100 - discountPercent)) / 100) * 100) / 100;
}

export interface BulkSaleTotals {
  before: number;
  after: number;
  saving: number;
}

/** Full-price vs discounted totals across a selection, for the "£870 → £696" preview. */
export function bulkSaleTotals(
  products: readonly Product[],
  discountPercent: number,
): BulkSaleTotals {
  const before = products.reduce((sum, p) => sum + p.price, 0);
  const after = products.reduce((sum, p) => sum + discountedPrice(p.price, discountPercent), 0);
  return {
    before: round2(before),
    after: round2(after),
    saving: round2(before - after),
  };
}

function round2(value: number): number {
  return Math.round(value * 100) / 100;
}

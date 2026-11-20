/**
 * The one rule for which pieces sit in a sub-category, shared by every page, count and admin
 * screen. Mirrors CategoryService.KeywordMatches on the backend.
 *
 * Keywords first: a piece's NAME (lower-cased) must contain an include word and no exclude word.
 * A hand-made exception always wins: added-by-hand pieces are in, removed-by-hand pieces are out.
 */
export interface MembershipRule {
  includeKeywords: readonly string[];
  excludeKeywords: readonly string[];
  addedProductIds: readonly string[];
  removedProductIds: readonly string[];
}

export function keywordMatches(
  productName: string,
  include: readonly string[],
  exclude: readonly string[],
): boolean {
  const name = productName.toLowerCase();
  if (exclude.some((x) => x.length > 0 && name.includes(x.toLowerCase()))) {
    return false;
  }
  return include.some((k) => k.length > 0 && name.includes(k.toLowerCase()));
}

export function isMember(product: { id: string; name: string }, rule: MembershipRule): boolean {
  if (rule.removedProductIds.includes(product.id)) {
    return false;
  }
  if (rule.addedProductIds.includes(product.id)) {
    return true;
  }
  return keywordMatches(product.name, rule.includeKeywords, rule.excludeKeywords);
}

/** How a piece came to be in (or out of) a sub-category — shown on the admin screens. */
export type MembershipReason = 'keyword' | 'added' | 'removed' | 'none';

export function membershipReason(product: { id: string; name: string }, rule: MembershipRule): MembershipReason {
  if (rule.removedProductIds.includes(product.id)) {
    return 'removed';
  }
  if (rule.addedProductIds.includes(product.id)) {
    return 'added';
  }
  return keywordMatches(product.name, rule.includeKeywords, rule.excludeKeywords) ? 'keyword' : 'none';
}

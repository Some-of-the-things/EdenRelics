import { Product } from '../../models/product.model';

/** One curated piece: its stable SKU plus the clean slug it should live at. */
export interface CollectionItem {
  sku: string;
  slug: string;
}

export interface CollectionProfile {
  slug: string;
  /** Display title, shown in Eden Relics branding at the top of the page. */
  name: string;
  metaTitle: string;
  metaDescription: string;
  /** Lead paragraph describing the edit. */
  intro: string;
  /**
   * The pieces in this collection, in display order. Membership is explicit (by
   * SKU) — not name-matched like designers — so a curated edit can't accidentally
   * pull in other products that share a designer or keyword. Each item also
   * carries the clean slug it should live at (used by the approve/publish flow to
   * fix the GUID-slugged pieces at go-live).
   */
  items: CollectionItem[];
  /** SKUs to feature on the homepage strip, in display order (subset of items). */
  featuredSkus: string[];
}

export const COLLECTIONS: CollectionProfile[] = [
  {
    slug: 'wildflower-edit',
    name: 'The Wildflower Edit',
    metaTitle: 'The Wildflower Edit — Vintage Floral Dresses | Eden Relics',
    metaDescription:
      'Thirteen hand-picked vintage dresses united by romantic florals, graceful silhouettes and exceptional craftsmanship — Laura Ashley, Angela Gore, Van Allan and more.',
    intro:
      'The Wildflower Edit is a celebration of vintage dresses that feel timeless rather than trend-led. Thirteen carefully chosen originals united by romantic florals, graceful silhouettes and exceptional craftsmanship. Each dress has its own story, but together they speak the same language: soft structure, thoughtful detail and the quiet confidence of clothes made to be loved for decades, not seasons.',
    items: [
      { sku: 'ER-00121', slug: '1980s-90s-laura-ashley-floral-dress-pastel-lightweight-cotton-v-waist' },
      { sku: 'ER-00120', slug: '1980s-laura-ashley-red-floral-dress-wool-cotton-self-covered-buttons' },
      { sku: 'ER-00119', slug: '1970s-angela-gore-maxi-dress-paisley-lace-teal-velvet-waist' },
      { sku: 'ER-00118', slug: '1982-epcot-center-dress-raspberry-lace-collar-cuffs' },
      { sku: 'ER-00117', slug: '1970s-handmade-folk-floral-maxi-dress-navy-with-contrast-bib-lace-trim' },
      { sku: 'ER-00116', slug: '1970s-van-allan-maxi-dress-lilac-flutter-sleeves-lace-applique' },
      { sku: 'ER-00115', slug: '1970s-two-tone-maxi-dress-white-chocolate-with-velvet-trim-paisley-border-print' },
      { sku: 'ER-00114', slug: '1970s-handmade-floral-maxi-dress-white-with-bell-sleeves-lace-bow-detail' },
      { sku: 'ER-00103', slug: '1970s-empire-waist-maxi-dress-powder-blue-ditsy-floral' },
      { sku: 'ER-00102', slug: '1970s-striped-maxi-dress-crochet-lace-bib-sheer-sleeves' },
      { sku: 'ER-00101', slug: '1970s-handmade-pinafore-maxi-dress-blue-green-plaid-eyelet-bib' },
      { sku: 'ER-00100', slug: '1980s-90s-laura-ashley-floral-maxi-dress-navy-open-back-oversized-bow' },
      { sku: 'ER-00099', slug: '1980s-laura-ashley-rose-print-dress-burgundy-wool-cotton-twill' },
    ],
    featuredSkus: ['ER-00119', 'ER-00118', 'ER-00100', 'ER-00114', 'ER-00120'],
  },
];

export function findCollectionBySlug(slug: string): CollectionProfile | undefined {
  return COLLECTIONS.find((c) => c.slug === slug);
}

/** All product slugs in the collection, in display order. */
export function collectionProductSlugs(c: CollectionProfile): string[] {
  return c.items.map((i) => i.slug);
}

/** Featured product slugs (homepage strip), in display order. */
export function collectionFeaturedSlugs(c: CollectionProfile): string[] {
  const slugBySku = new Map(c.items.map((i) => [i.sku, i.slug]));
  return c.featuredSkus
    .map((sku) => slugBySku.get(sku))
    .filter((s): s is string => s !== undefined);
}

/**
 * Products belonging to a collection, in the collection's declared order.
 * Products not currently live (or not yet found in the store) are omitted, so
 * the page degrades gracefully before the pieces go live.
 */
export function orderedCollectionProducts(
  products: readonly Product[],
  slugs: readonly string[],
): Product[] {
  const bySlug = new Map(products.map((p) => [p.slug ?? p.id, p]));
  return slugs
    .map((s) => bySlug.get(s))
    .filter((p): p is Product => p !== undefined);
}
